namespace PayFlow.Domain.Messages;

public sealed record WebhookDeliveryJob(
    Guid DeliveryLogId,
    Guid WebhookEndpointId,
    Guid TenantId,
    Guid PaymentId,
    string EndpointUrl,
    string Secret,
    string EventType,
    string Payload,
    int AttemptNumber,
    DateTime ScheduledAt);
