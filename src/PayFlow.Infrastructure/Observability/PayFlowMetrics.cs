using System.Diagnostics;
using System.Diagnostics.Metrics;
using PayFlow.Application.Common.Observability;

namespace PayFlow.Infrastructure.Observability;

public sealed class PayFlowMetrics : IPayFlowMetrics
{
    private static readonly Meter Meter = new("PayFlow", "1.0");

    private static readonly Counter<long> PaymentsCounter = Meter.CreateCounter<long>("payflow.payments.total");
    private static readonly Counter<long> TopUpsCounter = Meter.CreateCounter<long>("payflow.topups.total");
    private static readonly Counter<long> InsufficientFundsCounter = Meter.CreateCounter<long>("payflow.insufficient.funds.total");
    private static readonly Histogram<double> PaymentAmount = Meter.CreateHistogram<double>(
        "payflow.payment.amount",
        unit: "USD",
        description: "Payment amount distribution",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [1, 10, 50, 100, 500, 1000, 5000, 10000]
        });
    private readonly Counter<long> webhookDeliveries = Meter.CreateCounter<long>("payflow.webhook.deliveries.total");
    private readonly Counter<long> webhookRetries = Meter.CreateCounter<long>("payflow.webhook.retry.attempts.total");
    private readonly Counter<long> rateLimitHits = Meter.CreateCounter<long>("payflow.rate.limit.hits.total");
    private readonly Counter<long> idempotencyReplays = Meter.CreateCounter<long>("payflow.idempotency.replays.total");
    private readonly object kafkaLagLock = new();
    private readonly List<Measurement<long>> kafkaLagMeasurements = [];
    private readonly Dictionary<(string Topic, string ConsumerGroup), int> kafkaLagMeasurementIndexes = [];
    private int activeTenantCount;

    public PayFlowMetrics()
    {
        Meter.CreateObservableGauge(
            "payflow.active.tenants",
            observeValue: () => Volatile.Read(ref activeTenantCount));
        Meter.CreateObservableGauge(
            "payflow.kafka.consumer.lag",
            observeValues: ObserveKafkaLagMeasurements);
    }

    public static void EnsureInitialized()
    {
        GC.KeepAlive(PaymentsCounter);
        GC.KeepAlive(TopUpsCounter);
        GC.KeepAlive(InsufficientFundsCounter);
        GC.KeepAlive(PaymentAmount);
    }

    public void RecordPayment(string status, string currency, string tier, decimal amount, string direction = "none")
    {
        var tags = new TagList
        {
            { "status", status },
            { "currency", currency },
            { "tier", tier  },
            { "direction", direction }
        };

        PaymentsCounter.Add(1, tags);
        PaymentAmount.Record(decimal.ToDouble(amount), tags);
    }

    public void RecordTopUp(string currency, string tier)
    {
        var tags = new TagList
        {
            { "currency", currency },
            { "tier", tier }
        };

        TopUpsCounter.Add(1, tags);
    }

    public void RecordInsufficientFunds(string currency, string tier)
    {
        var tags = new TagList
        {
            { "currency", currency },
            { "tier", tier }
        };

        InsufficientFundsCounter.Add(1, tags);
    }

    public void RecordWebhookDelivery(string status)
    {
        var tags = new TagList { { "status", status } };

        webhookDeliveries.Add(1, tags);
    }

    public void RecordWebhookRetry(int attemptNumber)
    {
        var tags = new TagList { { "attempt_number", attemptNumber.ToString() } };

        webhookRetries.Add(1, tags);
    }

    public void RecordRateLimitHit(string tier, string endpoint)
    {
        var tags = new TagList
        {
            { "tier", tier },
            { "endpoint", endpoint }
        };

        rateLimitHits.Add(1, tags);
    }

    public void RecordIdempotencyReplay(string tier)
    {
        var tags = new TagList { { "tier", tier } };

        idempotencyReplays.Add(1, tags);
    }

    public void SetActiveTenants(int count)
    {
        Interlocked.Exchange(ref activeTenantCount, count);
    }

    public void IncrementActiveTenants()
    {
        Interlocked.Increment(ref activeTenantCount);
    }

    public void DecrementActiveTenants()
    {
        Interlocked.Decrement(ref activeTenantCount);
    }

    public void RecordKafkaConsumerLag(string topic, string consumerGroup, long lag)
    {
        var key = (topic, consumerGroup);
        var measurement = new Measurement<long>(
            Math.Max(0, lag),
            new TagList
            {
                { "topic", topic },
                { "consumer_group", consumerGroup }
            });

        lock (kafkaLagLock)
        {
            if (kafkaLagMeasurementIndexes.TryGetValue(key, out var index))
            {
                kafkaLagMeasurements[index] = measurement;
                return;
            }

            kafkaLagMeasurementIndexes[key] = kafkaLagMeasurements.Count;
            kafkaLagMeasurements.Add(measurement);
        }
    }

    private IEnumerable<Measurement<long>> ObserveKafkaLagMeasurements()
    {
        lock (kafkaLagLock)
        {
            return kafkaLagMeasurements.ToArray();
        }
    }
}
