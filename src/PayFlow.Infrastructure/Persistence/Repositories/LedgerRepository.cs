using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class LedgerRepository(PayFlowDbContext dbContext) : ILedgerRepository
{
    public async Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken ct)
    {
        await dbContext.LedgerEntries.AddRangeAsync(entries, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<decimal> GetBalanceAsync(Guid tenantId, string currency, CancellationToken ct)
    {
        return dbContext.LedgerEntries
            .Where(entry => entry.TenantId == tenantId && entry.Currency == currency)
            .SumAsync(
                entry => entry.Type == LedgerEntryType.Credit ? entry.Amount : -entry.Amount,
                ct);
    }
}
