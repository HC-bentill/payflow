using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(PayFlowDbContext dbContext) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == id, ct);
    }

    public Task<Tenant?> GetByApiKeyHashAsync(string hash, CancellationToken ct)
    {
        return dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.ApiKeyHash == hash, ct);
    }

    public Task<bool> ExistsAsync(string name, CancellationToken ct)
    {
        return dbContext.Tenants.AnyAsync(tenant => tenant.Name == name, ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct)
    {
        await dbContext.Tenants.AddAsync(tenant, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
