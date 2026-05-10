using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Wallet?> GetByIdWithLockAsync(Guid id, CancellationToken ct);

    Task<Wallet?> GetByOwnerAndCurrencyAsync(Guid ownerId, string currency, CancellationToken ct);

    Task<Wallet> FindOrCreateAsync(Guid ownerId, string currency, CancellationToken ct);

    Task<IReadOnlyCollection<Wallet>> GetByOwnerAsync(Guid ownerId, CancellationToken ct);
}
