namespace PayFlow.Domain.Entities;

public sealed class Tenant(
    Guid id,
    string name,
    string apiKeyHash,
    TenantTier tier,
    DateTime createdAt,
    bool isActive)
{
    private Tenant()
        : this(Guid.Empty, string.Empty, string.Empty, TenantTier.Free, DateTime.MinValue, false)
    {
    }

    public Guid Id { get; private set; } = id;

    public string Name { get; private set; } = name;

    public string ApiKeyHash { get; private set; } = apiKeyHash;

    public TenantTier Tier { get; private set; } = tier;

    public DateTime CreatedAt { get; private set; } = createdAt;

    public bool IsActive { get; private set; } = isActive;
}
