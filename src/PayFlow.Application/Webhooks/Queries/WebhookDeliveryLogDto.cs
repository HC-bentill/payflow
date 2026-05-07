using PayFlow.Domain.Enums;

namespace PayFlow.Application.Webhooks.Queries;

public sealed record WebhookDeliveryLogDto(
    Guid DeliveryLogId,
    Guid WebhookEndpointId,
    Guid PaymentId,
    string EventType,
    WebhookDeliveryStatus Status,
    int AttemptCount,
    DateTime? LastAttemptAt,
    DateTime? NextRetryAt,
    int? ResponseStatusCode,
    string? ResponseBody,
    DateTime CreatedAt,
    DateTime UpdatedAt);
