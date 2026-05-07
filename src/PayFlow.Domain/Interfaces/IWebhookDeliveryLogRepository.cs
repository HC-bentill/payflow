using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Interfaces;

public interface IWebhookDeliveryLogRepository
{
    Task AddAsync(WebhookDeliveryLog log, CancellationToken ct);

    Task UpdateAsync(WebhookDeliveryLog log, CancellationToken ct);

    Task<WebhookDeliveryLog?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyCollection<WebhookDeliveryLog>> GetByTenantAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<IReadOnlyCollection<WebhookDeliveryLog>> GetFailedByTenantAsync(Guid tenantId, CancellationToken ct);

    Task<IReadOnlyCollection<WebhookDeliveryLog>> GetFailedByTenantAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<int> GetCountByTenantAsync(Guid tenantId, CancellationToken ct);

    Task<int> GetFailedCountByTenantAsync(Guid tenantId, CancellationToken ct);
}
