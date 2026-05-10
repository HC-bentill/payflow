using PayFlow.Domain.Enums;

namespace PayFlow.Domain.Entities;

public sealed class TopUp(
    Guid id,
    Guid walletId,
    Guid tenantId,
    string idempotencyKey,
    decimal amount,
    string currency,
    TopUpStatus status,
    DateTime createdAt,
    DateTime updatedAt)
{
    private TopUp()
        : this(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            0,
            string.Empty,
            TopUpStatus.Pending,
            DateTime.MinValue,
            DateTime.MinValue)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid WalletId { get; private set; } = walletId;

    public Guid TenantId { get; private set; } = tenantId;

    public string IdempotencyKey { get; private set; } = idempotencyKey;

    public decimal Amount { get; private set; } = amount;

    public string Currency { get; private set; } = currency;

    public TopUpStatus Status { get; private set; } = status;

    public DateTime CreatedAt { get; private set; } = createdAt;

    public DateTime UpdatedAt { get; private set; } = updatedAt;

    public Wallet Wallet { get; private set; } = null!;

    public void MarkCompleted(DateTime updatedAt)
    {
        Status = TopUpStatus.Completed;
        UpdatedAt = updatedAt;
    }

    public void MarkFailed(DateTime updatedAt)
    {
        Status = TopUpStatus.Failed;
        UpdatedAt = updatedAt;
    }
}
