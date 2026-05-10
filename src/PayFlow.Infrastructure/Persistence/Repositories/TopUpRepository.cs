using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public sealed class TopUpRepository(PayFlowDbContext dbContext) : ITopUpRepository
{
    public Task<TopUp?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.TopUps.FirstOrDefaultAsync(topUp => topUp.Id == id, ct);
    }

    public Task<TopUp?> GetByIdempotencyKeyAsync(Guid walletId, string key, CancellationToken ct)
    {
        return dbContext.TopUps.FirstOrDefaultAsync(
            topUp => topUp.WalletId == walletId && topUp.IdempotencyKey == key,
            ct);
    }

    public async Task AddAsync(TopUp topUp, CancellationToken ct)
    {
        await dbContext.TopUps.AddAsync(topUp, ct);
    }

    public async Task<IReadOnlyCollection<TopUp>> GetByWalletAsync(
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        return await dbContext.TopUps
            .Where(topUp => topUp.WalletId == walletId)
            .OrderByDescending(topUp => topUp.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToArrayAsync(ct);
    }

    public Task<int> GetCountByWalletAsync(Guid walletId, CancellationToken ct)
    {
        return dbContext.TopUps.CountAsync(topUp => topUp.WalletId == walletId, ct);
    }
}
