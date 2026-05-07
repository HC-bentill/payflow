namespace PayFlow.Application.Common.Observability;

public interface IPayFlowMetrics
{
    void RecordPayment(string status, string currency, string tier, decimal amount);

    void RecordWebhookDelivery(string status);

    void RecordWebhookRetry(int attemptNumber);

    void RecordRateLimitHit(string tier, string endpoint);

    void RecordIdempotencyReplay(string tier);

    void SetActiveTenants(int count);

    void IncrementActiveTenants();

    void DecrementActiveTenants();

    void RecordKafkaConsumerLag(string topic, string consumerGroup, long lag);
}
