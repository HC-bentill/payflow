using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Interfaces;

public interface IWebhookEndpointRepository
{
    Task<IReadOnlyCollection<WebhookEndpoint>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct);

    Task<IReadOnlyCollection<WebhookEndpoint>> GetByTenantAsync(Guid tenantId, CancellationToken ct);

    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct);

    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct);

    Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken ct);
}
