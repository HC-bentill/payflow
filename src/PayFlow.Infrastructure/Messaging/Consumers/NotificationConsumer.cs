using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Messages;

namespace PayFlow.Infrastructure.Messaging.Consumers;

public sealed class NotificationConsumer(
    KafkaConsumerFactory consumerFactory,
    IConfiguration configuration,
    ILogger<NotificationConsumer> logger) : IHostedService
{
    private const string NotificationsTopic = "notifications.dispatch";
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

    private Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var groupId = configuration["Kafka:ConsumerGroups:Notification"] ?? "notification-group";

        using var consumer = consumerFactory.Create(groupId);
        consumer.Subscribe(NotificationsTopic);
        logger.LogInformation("Notification consumer subscribed to {Topic}", NotificationsTopic);

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

                    Process(result.Message.Value);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to dispatch notification");
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

        return Task.CompletedTask;
    }

    private void Process(string message)
    {
        var job = JsonSerializer.Deserialize<NotificationJob>(message, JsonOptions)
            ?? throw new InvalidOperationException("Notification job payload was empty.");

        // TODO Phase 6: integrate real email/SMS provider (SendGrid, Twilio)
        logger.LogInformation(
            "Notification dispatched for notification {NotificationId}, tenant {TenantId}, type {Type}, recipient {Recipient}",
            job.NotificationId,
            job.TenantId,
            job.Type,
            job.Recipient);
    }

    private void CommitOffset(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
    {
        try
        {
            consumer.Commit(result);
        }
        catch (KafkaException exception)
        {
            logger.LogWarning(exception, "Failed to commit notification Kafka offset");
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
