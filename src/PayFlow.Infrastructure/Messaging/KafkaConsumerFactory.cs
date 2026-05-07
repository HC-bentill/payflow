using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace PayFlow.Infrastructure.Messaging;

public sealed class KafkaConsumerFactory(IConfiguration configuration)
{
    public IConsumer<string, string> Create(string groupId)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            throw new InvalidOperationException("Kafka bootstrap servers are required.");
        }

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        return new ConsumerBuilder<string, string>(consumerConfig).Build();
    }
}
