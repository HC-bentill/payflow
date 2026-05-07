using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Application.Common.Observability;
using PayFlow.Domain.Interfaces;
using PayFlow.Domain.Messages;
using PayFlow.Infrastructure.Services;

namespace PayFlow.Infrastructure.Messaging.Consumers;

public sealed class WebhookDeliveryWorker(
    KafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    IEventPublisher eventPublisher,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IPayFlowMetrics metrics,
    ILogger<WebhookDeliveryWorker> logger) : IHostedService
{
    private const string WebhookDeliveryTopic = "webhook.delivery";
    private const string WebhookDlqTopic = "webhook.dlq";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan MaxScheduleWait = TimeSpan.FromSeconds(600);
    private static readonly int[] RetryDelaysSeconds = [3, 10, 60, 600];
    private CancellationTokenSource? stoppingCts;
    private Task? executingTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executingTask = Task.Run(() => ExecuteAsync(stoppingCts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (executingTask is null || stoppingCts is null)
        {
            return;
        }

        await stoppingCts.CancelAsync();
        await Task.WhenAny(executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var groupId = configuration["Kafka:ConsumerGroups:WebhookDelivery"] ?? "webhook-delivery-group";

        using var consumer = consumerFactory.Create(groupId);
        consumer.Subscribe(WebhookDeliveryTopic);
        logger.LogInformation("Webhook delivery worker subscribed to {Topic}", WebhookDeliveryTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = consumer.Consume(stoppingToken);
                    if (result is null)
                    {
                        continue;
                    }

                    await ProcessAsync(result.Message.Value, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Unhandled webhook delivery worker error");
                }
                finally
                {
                    if (result is not null)
                    {
                        CommitOffset(consumer, result);
                    }
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessAsync(string message, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<WebhookDeliveryJob>(message, JsonOptions)
            ?? throw new InvalidOperationException("Webhook delivery job payload was empty.");
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = job.TenantId,
            ["PaymentId"] = job.PaymentId,
            ["DeliveryLogId"] = job.DeliveryLogId
        });

        await WaitUntilScheduledAsync(job.ScheduledAt, ct);

        DeliveryAttemptResult attemptResult;
        try
        {
            attemptResult = await SendWebhookAsync(job, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            attemptResult = new DeliveryAttemptResult(false, null, exception.Message);
        }

        await using var serviceScope = serviceScopeFactory.CreateAsyncScope();
        var deliveryLogRepository = serviceScope.ServiceProvider.GetRequiredService<IWebhookDeliveryLogRepository>();
        var log = await deliveryLogRepository.GetByIdAsync(job.DeliveryLogId, ct);
        if (log is null)
        {
            logger.LogError(
                "Webhook delivery log {DeliveryLogId} was not found for payment {PaymentId}",
                job.DeliveryLogId,
                job.PaymentId);
            return;
        }

        var attemptedAt = DateTime.UtcNow;
        if (attemptResult.Succeeded)
        {
            log.MarkDelivered((int?)attemptResult.StatusCode, attemptResult.ResponseBody, attemptedAt);
            await deliveryLogRepository.UpdateAsync(log, ct);
            metrics.RecordWebhookDelivery("delivered");
            logger.LogInformation(
                "Webhook delivered for payment {PaymentId}, tenant {TenantId}, endpoint {EndpointUrl}",
                job.PaymentId,
                job.TenantId,
                job.EndpointUrl);
            return;
        }

        var nextRetryAt = GetNextRetryAt(log.AttemptCount + 1, attemptedAt);
        log.MarkFailed((int?)attemptResult.StatusCode, attemptResult.ResponseBody, attemptedAt, nextRetryAt);

        if (log.AttemptCount < 5 && nextRetryAt is not null)
        {
            await deliveryLogRepository.UpdateAsync(log, ct);
            var retryJob = job with
            {
                AttemptNumber = log.AttemptCount + 1,
                ScheduledAt = nextRetryAt.Value
            };
            metrics.RecordWebhookDelivery("failed");
            metrics.RecordWebhookRetry(retryJob.AttemptNumber);
            logger.LogWarning(
                "Webhook delivery failed for payment {PaymentId}, tenant {TenantId}, endpoint {EndpointUrl}, status {StatusCode}, attempt {AttemptCount}",
                job.PaymentId,
                job.TenantId,
                job.EndpointUrl,
                attemptResult.StatusCode,
                log.AttemptCount);
            await eventPublisher.PublishAsync(WebhookDeliveryTopic, retryJob, ct);
            return;
        }

        log.MarkPermanentlyFailed(attemptedAt);
        await deliveryLogRepository.UpdateAsync(log, ct);
        await eventPublisher.PublishAsync(WebhookDlqTopic, job with { AttemptNumber = log.AttemptCount }, ct);
        metrics.RecordWebhookDelivery("permanently_failed");
        logger.LogError(
            "Webhook permanently failed for payment {PaymentId}, tenant {TenantId}, endpoint {EndpointUrl}, status {StatusCode}, attempt {AttemptCount}",
            job.PaymentId,
            job.TenantId,
            job.EndpointUrl,
            attemptResult.StatusCode,
            log.AttemptCount);
    }

    private async Task<DeliveryAttemptResult> SendWebhookAsync(WebhookDeliveryJob job, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, job.EndpointUrl)
        {
            Content = new StringContent(job.Payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-PayFlow-Event", job.EventType);
        request.Headers.Add("X-PayFlow-Delivery", job.DeliveryLogId.ToString());
        request.Headers.Add(
            "X-PayFlow-Signature",
            $"sha256={WebhookSignatureService.ComputeSignature(job.Payload, job.Secret)}");

        var client = httpClientFactory.CreateClient("webhook-delivery");
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        return new DeliveryAttemptResult(
            response.IsSuccessStatusCode,
            response.StatusCode,
            TruncateResponseBody(body));
    }

    private static DateTime? GetNextRetryAt(int attemptCount, DateTime attemptedAt)
    {
        return attemptCount < 5
            ? attemptedAt.AddSeconds(RetryDelaysSeconds[attemptCount - 1])
            : null;
    }

    private static async Task WaitUntilScheduledAsync(DateTime scheduledAt, CancellationToken ct)
    {
        var delay = scheduledAt - DateTime.UtcNow;
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(delay > MaxScheduleWait ? MaxScheduleWait : delay, ct);
    }

    private void CommitOffset(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
    {
        try
        {
            consumer.Commit(result);
        }
        catch (KafkaException exception)
        {
            logger.LogWarning(exception, "Failed to commit webhook delivery Kafka offset");
        }
    }

    private static string? TruncateResponseBody(string? responseBody)
    {
        if (responseBody is null || responseBody.Length <= 500)
        {
            return responseBody;
        }

        return responseBody[..500];
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };
    }

    private sealed record DeliveryAttemptResult(bool Succeeded, HttpStatusCode? StatusCode, string? ResponseBody);
}
