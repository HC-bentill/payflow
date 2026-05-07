using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class WebhookDeliveryLogRepository(PayFlowDbContext dbContext) : IWebhookDeliveryLogRepository
{
    public async Task AddAsync(WebhookDeliveryLog log, CancellationToken ct)
    {
        await dbContext.WebhookDeliveryLogs.AddAsync(log, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WebhookDeliveryLog log, CancellationToken ct)
    {
        dbContext.WebhookDeliveryLogs.Update(log);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<WebhookDeliveryLog?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.WebhookDeliveryLogs.FirstOrDefaultAsync(log => log.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<WebhookDeliveryLog>> GetByTenantAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        return await dbContext.WebhookDeliveryLogs
            .Where(log => log.TenantId == tenantId)
            .OrderByDescending(log => log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<WebhookDeliveryLog>> GetFailedByTenantAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        return await GetFailedQuery(tenantId)
            .OrderByDescending(log => log.UpdatedAt)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<WebhookDeliveryLog>> GetFailedByTenantAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        return await GetFailedQuery(tenantId)
            .OrderByDescending(log => log.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);
    }

    public Task<int> GetCountByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return dbContext.WebhookDeliveryLogs.CountAsync(log => log.TenantId == tenantId, ct);
    }

    public Task<int> GetFailedCountByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return GetFailedQuery(tenantId).CountAsync(ct);
    }

    private IQueryable<WebhookDeliveryLog> GetFailedQuery(Guid tenantId)
    {
        return dbContext.WebhookDeliveryLogs.Where(log =>
            log.TenantId == tenantId &&
            (log.Status == WebhookDeliveryStatus.Failed ||
                log.Status == WebhookDeliveryStatus.PermanentlyFailed));
    }
}
