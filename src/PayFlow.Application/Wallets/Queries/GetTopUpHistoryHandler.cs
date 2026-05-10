using MediatR;
using PayFlow.Application.Common.Exceptions;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Wallets.Queries;

public sealed class GetTopUpHistoryHandler(
    IWalletRepository walletRepository,
    ITopUpRepository topUpRepository)
    : IRequestHandler<GetTopUpHistoryQuery, GetTopUpHistoryResult>
{
    public async Task<GetTopUpHistoryResult> Handle(
        GetTopUpHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet is null || wallet.OwnerId != request.TenantId)
        {
            throw new NotFoundException("Wallet", request.WalletId);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = await topUpRepository.GetCountByWalletAsync(request.WalletId, cancellationToken);
        var topUps = await topUpRepository.GetByWalletAsync(request.WalletId, page, pageSize, cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new GetTopUpHistoryResult(
            topUps
                .Select(topUp => new TopUpHistoryItem(
                    topUp.Id,
                    topUp.WalletId,
                    topUp.Amount,
                    topUp.Currency,
                    topUp.Status,
                    topUp.CreatedAt))
                .ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}
