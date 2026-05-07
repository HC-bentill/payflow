using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayFlow.Application.Common.Observability;

namespace PayFlow.Infrastructure.Observability;

public sealed class KafkaLagMonitor(
    IPayFlowMetrics metrics,
    IConfiguration configuration,
    ILogger<KafkaLagMonitor> logger) : IHostedService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KafkaRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly KafkaLagTarget[] Targets =
    [
        new("payment.events", "Kafka:ConsumerGroups:WebhookDispatch", "webhook-dispatch-group"),
        new("webhook.delivery", "Kafka:ConsumerGroups:WebhookDelivery", "webhook-delivery-group"),
        new("webhook.dlq", "Kafka:ConsumerGroups:DLQMonitor", "dlq-monitor-group"),
        new("notifications.dispatch", "Kafka:ConsumerGroups:Notification", "notification-group")
    ];

    private CancellationTokenSource? stoppingCts;
    private Task? executingTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Kafka:Consumers:Enabled", true))
        {
            logger.LogInformation("Kafka lag monitor disabled because Kafka consumers are disabled");
            return Task.CompletedTask;
        }

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            logger.LogWarning("Kafka lag monitor disabled because bootstrap servers are not configured");
            return Task.CompletedTask;
        }

        stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executingTask = Task.Run(() => ExecuteAsync(bootstrapServers, stoppingCts.Token), CancellationToken.None);

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

    private async Task ExecuteAsync(string bootstrapServers, CancellationToken stoppingToken)
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        }).Build();
        using var timer = new PeriodicTimer(PollInterval);

        await RecordAllConsumerLagAsync(adminClient, stoppingToken);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RecordAllConsumerLagAsync(adminClient, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RecordAllConsumerLagAsync(IAdminClient adminClient, CancellationToken ct)
    {
        foreach (var target in Targets)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await RecordConsumerLagAsync(adminClient, target, ct);
            }
            catch (Exception exception) when (exception is KafkaException or TimeoutException or InvalidOperationException)
            {
                logger.LogWarning(
                    exception,
                    "Failed to record Kafka consumer lag for topic {Topic} and consumer group {ConsumerGroup}",
                    target.Topic,
                    GetConsumerGroup(target));
            }
        }
    }

    private async Task RecordConsumerLagAsync(IAdminClient adminClient, KafkaLagTarget target, CancellationToken ct)
    {
        var consumerGroup = GetConsumerGroup(target);
        var partitions = GetTopicPartitions(adminClient, target.Topic);
        if (partitions.Count == 0)
        {
            return;
        }

        var committedOffsetResults = await adminClient.ListConsumerGroupOffsetsAsync(
            [new ConsumerGroupTopicPartitions(consumerGroup, partitions)],
            new ListConsumerGroupOffsetsOptions
            {
                RequestTimeout = KafkaRequestTimeout
            });
        var latestOffsetResult = await adminClient.ListOffsetsAsync(
            partitions.Select(partition => new TopicPartitionOffsetSpec
            {
                TopicPartition = partition,
                OffsetSpec = OffsetSpec.Latest()
            }),
            new ListOffsetsOptions
            {
                RequestTimeout = KafkaRequestTimeout
            });

        ct.ThrowIfCancellationRequested();

        var latestOffsets = latestOffsetResult.ResultInfos
            .Where(result => !result.TopicPartitionOffsetError.Error.IsError)
            .ToDictionary(
                result => result.TopicPartitionOffsetError.TopicPartition,
                result => result.TopicPartitionOffsetError.Offset.Value);
        var committedPartitions = committedOffsetResults
            .FirstOrDefault(result => string.Equals(result.Group, consumerGroup, StringComparison.Ordinal))
            ?.Partitions;
        if (committedPartitions is null)
        {
            return;
        }

        var lag = 0L;
        foreach (var committedPartition in committedPartitions)
        {
            if (committedPartition.Error.IsError ||
                !latestOffsets.TryGetValue(committedPartition.TopicPartition, out var latestOffset))
            {
                continue;
            }

            var committedOffset = committedPartition.Offset.Value < 0
                ? 0
                : committedPartition.Offset.Value;
            lag += Math.Max(0, latestOffset - committedOffset);
        }

        metrics.RecordKafkaConsumerLag(target.Topic, consumerGroup, lag);
        logger.LogDebug(
            "Recorded Kafka consumer lag {Lag} for topic {Topic} and consumer group {ConsumerGroup}",
            lag,
            target.Topic,
            consumerGroup);
    }

    private static List<TopicPartition> GetTopicPartitions(IAdminClient adminClient, string topic)
    {
        var metadata = adminClient.GetMetadata(topic, KafkaRequestTimeout);
        var topicMetadata = metadata.Topics.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.Ordinal));
        if (topicMetadata is null || topicMetadata.Error.IsError)
        {
            return [];
        }

        return topicMetadata.Partitions
            .Select(partition => new TopicPartition(topic, new Partition(partition.PartitionId)))
            .ToList();
    }

    private string GetConsumerGroup(KafkaLagTarget target)
    {
        return configuration[target.ConsumerGroupConfigurationKey] ?? target.DefaultConsumerGroup;
    }

    private sealed record KafkaLagTarget(string Topic, string ConsumerGroupConfigurationKey, string DefaultConsumerGroup);
}
