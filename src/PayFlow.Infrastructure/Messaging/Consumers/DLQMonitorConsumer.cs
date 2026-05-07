using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Interfaces;
using PayFlow.Domain.Messages;

namespace PayFlow.Infrastructure.Messaging.Consumers;

public sealed class DLQMonitorConsumer(
    KafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    IConfiguration configuration,
    ILogger<DLQMonitorConsumer> logger) : IHostedService
{
    private const string WebhookDlqTopic = "webhook.dlq";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
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
        var groupId = configuration["Kafka:ConsumerGroups:DLQMonitor"] ?? "dlq-monitor-group";

        using var consumer = consumerFactory.Create(groupId);
        consumer.Subscribe(WebhookDlqTopic);
        logger.LogInformation("DLQ monitor consumer subscribed to {Topic}", WebhookDlqTopic);

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
                    logger.LogError(exception, "Failed to monitor webhook DLQ message");
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
            ?? throw new InvalidOperationException("Webhook DLQ job payload was empty.");

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var deliveryLogRepository = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryLogRepository>();
        var log = await deliveryLogRepository.GetByIdAsync(job.DeliveryLogId, ct);
        if (log is not null)
        {
            log.MarkPermanentlyFailed(DateTime.UtcNow);
            await deliveryLogRepository.UpdateAsync(log, ct);
        }

        logger.LogError(
            "Webhook permanently failed after 5 attempts for delivery {DeliveryLogId}, tenant {TenantId}, endpoint {EndpointUrl}, payment {PaymentId}",
            job.DeliveryLogId,
            job.TenantId,
            job.EndpointUrl,
            job.PaymentId);
    }

    private void CommitOffset(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
    {
        try
        {
            consumer.Commit(result);
        }
        catch (KafkaException exception)
        {
            logger.LogWarning(exception, "Failed to commit webhook DLQ Kafka offset");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
