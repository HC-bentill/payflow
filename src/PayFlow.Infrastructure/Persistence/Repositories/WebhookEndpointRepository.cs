using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class WebhookEndpointRepository(PayFlowDbContext dbContext) : IWebhookEndpointRepository
{
    public async Task<IReadOnlyCollection<WebhookEndpoint>> GetActiveByTenantAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        return await dbContext.WebhookEndpoints
            .Where(endpoint => endpoint.TenantId == tenantId && endpoint.IsActive)
            .OrderBy(endpoint => endpoint.CreatedAt)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<WebhookEndpoint>> GetByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return await dbContext.WebhookEndpoints
            .Where(endpoint => endpoint.TenantId == tenantId)
            .OrderByDescending(endpoint => endpoint.CreatedAt)
            .ToArrayAsync(ct);
    }

    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct)
    {
        await dbContext.WebhookEndpoints.AddAsync(endpoint, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct)
    {
        dbContext.WebhookEndpoints.Update(endpoint);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.WebhookEndpoints.FirstOrDefaultAsync(endpoint => endpoint.Id == id, ct);
    }
}
