namespace PayFlow.Domain.Entities;

public sealed class WebhookEndpoint(
    Guid id,
    Guid tenantId,
    string url,
    string secret,
    bool isActive,
    DateTime createdAt)
{
    private WebhookEndpoint()
        : this(Guid.Empty, Guid.Empty, string.Empty, string.Empty, false, DateTime.MinValue)
    {
    }

    public Guid Id { get; private set; } = id;

    public Guid TenantId { get; private set; } = tenantId;

    public string Url { get; private set; } = url;

    public string Secret { get; private set; } = secret;

    public bool IsActive { get; private set; } = isActive;

    public DateTime CreatedAt { get; private set; } = createdAt;
}
