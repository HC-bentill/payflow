namespace PayFlow.Domain.Entities;

public sealed class Wallet(Guid id, Guid tenantId, string currency)
{
    private Wallet()
        : this(Guid.Empty, Guid.Empty, string.Empty)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid TenantId { get; private set; } = tenantId;

    public string Currency { get; private set; } = currency;
}
