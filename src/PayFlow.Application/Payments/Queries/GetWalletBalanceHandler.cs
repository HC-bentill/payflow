using MediatR;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Payments.Queries;

public sealed class GetWalletBalanceHandler(
    IWalletRepository walletRepository,
    ILedgerRepository ledgerRepository)
    : IRequestHandler<GetWalletBalanceQuery, GetWalletBalanceResult>
{
    public async Task<GetWalletBalanceResult> Handle(
        GetWalletBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var ownedWallets = await walletRepository.GetByOwnerAsync(request.TenantId, cancellationToken);
        var targetWallet = ownedWallets.FirstOrDefault(wallet => wallet.Id == request.WalletId);
        if (targetWallet is null)
        {
            throw new UnauthorizedAccessException("You do not have access to this wallet");
        }
        var balance = await ledgerRepository.GetWalletBalanceAsync(request.WalletId, cancellationToken);

        return new GetWalletBalanceResult(
            request.WalletId,
            targetWallet.Currency,
            balance,
            DateTime.UtcNow);
    }
}
