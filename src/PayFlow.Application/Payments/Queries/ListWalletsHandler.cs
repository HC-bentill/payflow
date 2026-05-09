using MediatR;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Payments.Queries;

public sealed class ListWalletsHandler(
    IWalletRepository walletRepository,
    ILedgerRepository ledgerRepository)
    : IRequestHandler<ListWalletsQuery, IReadOnlyList<WalletSummaryResult>>
{
    public async Task<IReadOnlyList<WalletSummaryResult>> Handle(ListWalletsQuery request, CancellationToken cancellationToken)
    {
        var wallets = await walletRepository.GetByOwnerAsync(request.TenantId, cancellationToken);
        var results = new List<WalletSummaryResult>(wallets.Count);

        foreach (var wallet in wallets.OrderBy(wallet => wallet.CreatedAt))
        {
            var balance = await ledgerRepository.GetWalletBalanceAsync(wallet.Id, cancellationToken);
            results.Add(new WalletSummaryResult(wallet.Id, wallet.Currency, balance, wallet.CreatedAt));
        }

        return results;
    }
}
