using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class WalletRepository(PayFlowDbContext dbContext) : IWalletRepository
{
    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.Wallets.FirstOrDefaultAsync(wallet => wallet.Id == id, ct);
    }

    public Task<Wallet?> GetByIdWithLockAsync(Guid id, CancellationToken ct)
    {
        if (!dbContext.Database.IsRelational())
        {
            return GetByIdAsync(id, ct);
        }

        return dbContext.Wallets
            .FromSqlRaw("SELECT * FROM wallets WHERE \"Id\" = {0} FOR UPDATE", id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Wallet?> GetByOwnerAndCurrencyAsync(Guid ownerId, string currency, CancellationToken ct)
    {
        return dbContext.Wallets.FirstOrDefaultAsync(
            wallet => wallet.OwnerId == ownerId && wallet.Currency == currency,
            ct);
    }

    public async Task<Wallet> FindOrCreateAsync(Guid ownerId, string currency, CancellationToken ct)
    {
        var wallet = await GetByOwnerAndCurrencyAsync(ownerId, currency, ct);
        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet(Guid.NewGuid(), ownerId, currency, DateTime.UtcNow);
        await dbContext.Wallets.AddAsync(wallet, ct);
        return wallet;
    }

    public async Task<IReadOnlyCollection<Wallet>> GetByOwnerAsync(Guid ownerId, CancellationToken ct)
    {
        return await dbContext.Wallets
            .Where(wallet => wallet.OwnerId == ownerId)
            .OrderBy(wallet => wallet.CreatedAt)
            .ToArrayAsync(ct);
    }
}
