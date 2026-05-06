using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<Payment?> GetByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct);

    Task<IReadOnlyCollection<Payment>> ListByTenantAsync(Guid tenantId, CancellationToken ct);

    Task AddAsync(Payment payment, CancellationToken ct);

    Task UpdateAsync(Payment payment, CancellationToken ct);
}
