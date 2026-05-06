using MediatR;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Payments.Queries;

public sealed class GetWalletBalanceHandler(ILedgerRepository ledgerRepository)
    : IRequestHandler<GetWalletBalanceQuery, GetWalletBalanceResult>
{
    public async Task<GetWalletBalanceResult> Handle(
        GetWalletBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var balance = await ledgerRepository.GetBalanceAsync(
            request.TenantId,
            request.Currency,
            cancellationToken);

        return new GetWalletBalanceResult(
            request.TenantId,
            request.Currency,
            balance,
            DateTime.UtcNow);
    }
}
