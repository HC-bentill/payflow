using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class LedgerRepository(PayFlowDbContext dbContext) : ILedgerRepository
{
    public async Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken ct)
    {
        await dbContext.LedgerEntries.AddRangeAsync(entries, ct);
    }

    public async Task<IReadOnlyCollection<LedgerEntry>> GetByPaymentAsync(Guid paymentId, CancellationToken ct)
    {
        return await dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == paymentId)
            .OrderBy(entry => entry.CreatedAt)
            .ToArrayAsync(ct);
    }

    public async Task<decimal> GetBalanceAsync(Guid tenantId, string currency, CancellationToken ct)
    {
        var credits = await dbContext.LedgerEntries
            .Where(entry =>
                entry.TenantId == tenantId &&
                entry.Currency == currency &&
                entry.Type == LedgerEntryType.Credit)
            .SumAsync(entry => entry.Amount, ct);

        var debits = await dbContext.LedgerEntries
            .Where(entry =>
                entry.TenantId == tenantId &&
                entry.Currency == currency &&
                entry.Type == LedgerEntryType.Debit)
            .SumAsync(entry => entry.Amount, ct);

        return credits - debits;
    }
}
