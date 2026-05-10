using PayFlow.Domain.Enums;

namespace PayFlow.Domain.Entities;

public sealed class LedgerEntry(
    Guid id,
    Guid paymentId,
    Guid walletId,
    Guid tenantId,
    LedgerEntryType type,
    decimal amount,
    string currency,
    DateTime createdAt,
    LedgerEntrySource source = LedgerEntrySource.Payment)
{
    private LedgerEntry()
        : this(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            LedgerEntryType.Debit,
            0,
            string.Empty,
            DateTime.MinValue,
            LedgerEntrySource.Payment)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid PaymentId { get; private set; } = paymentId;

    public Guid WalletId { get; private set; } = walletId;

    public Guid TenantId { get; private set; } = tenantId;

    public Wallet Wallet { get; private set; } = null!;

    public LedgerEntryType Type { get; private set; } = type;

    public decimal Amount { get; private set; } = amount;

    public string Currency { get; private set; } = currency;

    public LedgerEntrySource Source { get; private set; } = source;

    public DateTime CreatedAt { get; private set; } = createdAt;
}
