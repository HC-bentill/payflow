using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Interfaces;

public interface ITopUpRepository
{
    Task<TopUp?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<TopUp?> GetByIdempotencyKeyAsync(Guid walletId, string key, CancellationToken ct);

    Task AddAsync(TopUp topUp, CancellationToken ct);

    Task<IReadOnlyCollection<TopUp>> GetByWalletAsync(Guid walletId, int page, int pageSize, CancellationToken ct);

    Task<int> GetCountByWalletAsync(Guid walletId, CancellationToken ct);
}
