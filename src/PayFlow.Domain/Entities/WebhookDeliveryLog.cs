using PayFlow.Domain.Enums;

namespace PayFlow.Domain.Entities;

public sealed class WebhookDeliveryLog(
    Guid id,
    Guid webhookEndpointId,
    Guid paymentId,
    Guid tenantId,
    string eventType,
    string payload,
    WebhookDeliveryStatus status,
    int attemptCount,
    DateTime? lastAttemptAt,
    DateTime? nextRetryAt,
    int? responseStatusCode,
    string? responseBody,
    DateTime createdAt,
    DateTime updatedAt)
{
    private WebhookDeliveryLog()
        : this(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            string.Empty,
            WebhookDeliveryStatus.Pending,
            0,
            null,
            null,
            null,
            null,
            DateTime.MinValue,
            DateTime.MinValue)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid WebhookEndpointId { get; private set; } = webhookEndpointId;

    public Guid PaymentId { get; private set; } = paymentId;

    public Guid TenantId { get; private set; } = tenantId;

    public string EventType { get; private set; } = eventType;

    public string Payload { get; private set; } = payload;

    public WebhookDeliveryStatus Status { get; private set; } = status;

    public int AttemptCount { get; private set; } = attemptCount;

    public DateTime? LastAttemptAt { get; private set; } = lastAttemptAt;

    public DateTime? NextRetryAt { get; private set; } = nextRetryAt;

    public int? ResponseStatusCode { get; private set; } = responseStatusCode;

    public string? ResponseBody { get; private set; } = responseBody;

    public DateTime CreatedAt { get; private set; } = createdAt;

    public DateTime UpdatedAt { get; private set; } = updatedAt;

    public void MarkDelivered(int? responseStatusCode, string? responseBody, DateTime attemptedAt)
    {
        Status = WebhookDeliveryStatus.Delivered;
        AttemptCount += 1;
        LastAttemptAt = attemptedAt;
        NextRetryAt = null;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = TruncateResponseBody(responseBody);
        UpdatedAt = attemptedAt;
    }

    public void MarkFailed(int? responseStatusCode, string? responseBody, DateTime attemptedAt, DateTime? nextRetryAt)
    {
        Status = WebhookDeliveryStatus.Failed;
        AttemptCount += 1;
        LastAttemptAt = attemptedAt;
        NextRetryAt = nextRetryAt;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = TruncateResponseBody(responseBody);
        UpdatedAt = attemptedAt;
    }

    public void MarkPermanentlyFailed(DateTime updatedAt)
    {
        Status = WebhookDeliveryStatus.PermanentlyFailed;
        NextRetryAt = null;
        UpdatedAt = updatedAt;
    }

    public void ResetForReplay(DateTime updatedAt)
    {
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 0;
        LastAttemptAt = null;
        NextRetryAt = null;
        ResponseStatusCode = null;
        ResponseBody = null;
        UpdatedAt = updatedAt;
    }

    private static string? TruncateResponseBody(string? responseBody)
    {
        if (responseBody is null || responseBody.Length <= 500)
        {
            return responseBody;
        }

        return responseBody[..500];
    }
}
