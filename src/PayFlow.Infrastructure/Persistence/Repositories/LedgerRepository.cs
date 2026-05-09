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

    public async Task<decimal> GetWalletBalanceAsync(Guid walletId, CancellationToken ct)
    {
        var credits = await dbContext.LedgerEntries
            .Where(entry => entry.WalletId == walletId && entry.Type == LedgerEntryType.Credit)
            .SumAsync(entry => entry.Amount, ct);

        var debits = await dbContext.LedgerEntries
            .Where(entry => entry.WalletId == walletId && entry.Type == LedgerEntryType.Debit)
            .SumAsync(entry => entry.Amount, ct);

        return credits - debits;
    }

    public async Task<decimal> GetBalanceAsync(Guid tenantId, string currency, CancellationToken ct)
    {
        var wallet = await dbContext.Wallets
            .FirstOrDefaultAsync(w => w.OwnerId == tenantId && w.Currency == currency, ct);

        if (wallet is null)
        {
            return 0;
        }

        return await GetWalletBalanceAsync(wallet.Id, ct);
    }

    public async Task<IReadOnlyCollection<LedgerEntry>> GetByWalletAsync(Guid walletId, int page, int pageSize, CancellationToken ct)
    {
        return await dbContext.LedgerEntries
            .Where(entry => entry.WalletId == walletId)
            .OrderByDescending(entry => entry.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 200))
            .Take(Math.Clamp(pageSize, 1, 200))
            .ToArrayAsync(ct);
    }
}
