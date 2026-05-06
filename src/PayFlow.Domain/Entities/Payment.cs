namespace PayFlow.Domain.Entities;

public sealed class Payment(
    Guid id,
    Guid tenantId,
    string idempotencyKey,
    decimal amount,
    string currency,
    PaymentStatus status,
    string? description,
    string? metadata,
    DateTime createdAt,
    DateTime updatedAt)
{
    private Payment()
        : this(
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            0,
            string.Empty,
            PaymentStatus.Pending,
            null,
            null,
            DateTime.MinValue,
            DateTime.MinValue)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid TenantId { get; private set; } = tenantId;

    public string IdempotencyKey { get; private set; } = idempotencyKey;

    public decimal Amount { get; private set; } = amount;

    public string Currency { get; private set; } = currency;

    public PaymentStatus Status { get; private set; } = status;

    public string? Description { get; private set; } = description;

    public string? Metadata { get; private set; } = metadata;

    public DateTime CreatedAt { get; private set; } = createdAt;

    public DateTime UpdatedAt { get; private set; } = updatedAt;

    public void MarkUpdated(DateTime updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}
