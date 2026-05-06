using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Messaging;

public sealed class KafkaEventPublisher(IProducer<string, string> producer) : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task PublishAsync<T>(string topic, T @event, CancellationToken ct)
    {
        var message = new Message<string, string>
        {
            Key = typeof(T).Name,
            Value = JsonSerializer.Serialize(@event, JsonOptions)
        };

        await producer.ProduceAsync(topic, message, ct);
    }
}
