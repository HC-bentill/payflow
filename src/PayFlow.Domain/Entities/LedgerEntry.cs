namespace PayFlow.Domain.Entities;

public sealed class LedgerEntry(
    Guid id,
    Guid paymentId,
    Guid tenantId,
    LedgerEntryType type,
    decimal amount,
    string currency,
    DateTime createdAt)
{
    private LedgerEntry()
        : this(Guid.Empty, Guid.Empty, Guid.Empty, LedgerEntryType.Debit, 0, string.Empty, DateTime.MinValue)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid PaymentId { get; private set; } = paymentId;

    public Guid TenantId { get; private set; } = tenantId;

    public LedgerEntryType Type { get; private set; } = type;

    public decimal Amount { get; private set; } = amount;

    public string Currency { get; private set; } = currency;

    public DateTime CreatedAt { get; private set; } = createdAt;
}
