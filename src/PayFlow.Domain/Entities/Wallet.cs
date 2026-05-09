namespace PayFlow.Domain.Entities;

public sealed class Wallet(Guid id, Guid ownerId, string currency, DateTime createdAt)
{
    private readonly List<LedgerEntry> ledgerEntries = [];

    private Wallet()
        : this(Guid.Empty, Guid.Empty, string.Empty, DateTime.MinValue)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid OwnerId { get; private set; } = ownerId;

    public string Currency { get; private set; } = currency;

    public DateTime CreatedAt { get; private set; } = createdAt;

    public IReadOnlyCollection<LedgerEntry> LedgerEntries => ledgerEntries;
}
