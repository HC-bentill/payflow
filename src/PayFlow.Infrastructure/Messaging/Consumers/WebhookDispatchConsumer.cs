using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Events;
using PayFlow.Domain.Interfaces;
using PayFlow.Domain.Messages;

namespace PayFlow.Infrastructure.Messaging.Consumers;

public sealed class WebhookDispatchConsumer(
    KafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    IEventPublisher eventPublisher,
    IConfiguration configuration,
    ILogger<WebhookDispatchConsumer> logger) : IHostedService
{
    private const string PaymentEventsTopic = "payment.events";
    private const string WebhookDeliveryTopic = "webhook.delivery";
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
        var groupId = configuration["Kafka:ConsumerGroups:WebhookDispatch"] ?? "webhook-dispatch-group";

        using var consumer = consumerFactory.Create(groupId);
        consumer.Subscribe(PaymentEventsTopic);
        logger.LogInformation("Webhook dispatch consumer subscribed to {Topic}", PaymentEventsTopic);

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
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to dispatch webhooks for payment event");
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
        var paymentEvent = JsonSerializer.Deserialize<PaymentProcessedEvent>(message, JsonOptions)
            ?? throw new InvalidOperationException("Payment event payload was empty.");
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = paymentEvent.TenantId,
            ["PaymentId"] = paymentEvent.PaymentId
        });

        await using var serviceScope = serviceScopeFactory.CreateAsyncScope();
        var endpointRepository = serviceScope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
        var deliveryLogRepository = serviceScope.ServiceProvider.GetRequiredService<IWebhookDeliveryLogRepository>();
        var endpoints = await endpointRepository.GetActiveByTenantAsync(paymentEvent.TenantId, ct);

        if (endpoints.Count == 0)
        {
            logger.LogInformation(
                "No active webhook endpoints for tenant {TenantId}",
                paymentEvent.TenantId);
            return;
        }

        var eventType = GetEventType(paymentEvent.Status);
        var now = DateTime.UtcNow;

        foreach (var endpoint in endpoints)
        {
            var log = new WebhookDeliveryLog(
                Guid.NewGuid(),
                endpoint.Id,
                paymentEvent.PaymentId,
                paymentEvent.TenantId,
                eventType,
                message,
                WebhookDeliveryStatus.Pending,
                0,
                null,
                null,
                null,
                null,
                now,
                now);

            await deliveryLogRepository.AddAsync(log, ct);

            var job = new WebhookDeliveryJob(
                log.Id,
                endpoint.Id,
                paymentEvent.TenantId,
                paymentEvent.PaymentId,
                endpoint.Url,
                endpoint.Secret,
                eventType,
                message,
                1,
                now);

            await eventPublisher.PublishAsync(WebhookDeliveryTopic, job, ct);
        }

        logger.LogInformation(
            "Published {WebhookCount} webhook delivery jobs for payment {PaymentId}, tenant {TenantId}",
            endpoints.Count,
            paymentEvent.PaymentId,
            paymentEvent.TenantId);
    }

    private static string GetEventType(PaymentStatus status)
    {
        return status switch
        {
            PaymentStatus.Succeeded => "payment.succeeded",
            PaymentStatus.Failed => "payment.failed",
            PaymentStatus.Processing => "payment.processing",
            _ => "payment.pending"
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
