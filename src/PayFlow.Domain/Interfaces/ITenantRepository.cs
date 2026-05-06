using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Tenant?> GetByApiKeyHashAsync(string hash, CancellationToken ct);

    Task AddAsync(Tenant tenant, CancellationToken ct);
}
