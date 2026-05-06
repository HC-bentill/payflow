using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Interfaces;

public interface ILedgerRepository
{
    Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken ct);

    Task<IReadOnlyCollection<LedgerEntry>> GetByPaymentAsync(Guid paymentId, CancellationToken ct);

    Task<decimal> GetBalanceAsync(Guid tenantId, string currency, CancellationToken ct);
}
