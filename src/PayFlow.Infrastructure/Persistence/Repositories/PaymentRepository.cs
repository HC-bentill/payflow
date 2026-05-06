using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PayFlowDbContext dbContext) : IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.Payments.FirstOrDefaultAsync(payment => payment.Id == id, ct);
    }

    public Task<Payment?> GetByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct)
    {
        return dbContext.Payments.FirstOrDefaultAsync(
            payment => payment.TenantId == tenantId && payment.IdempotencyKey == key,
            ct);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        await dbContext.Payments.AddAsync(payment, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct)
    {
        dbContext.Payments.Update(payment);
        await dbContext.SaveChangesAsync(ct);
    }
}
