using MediatR;

namespace PayFlow.Application.Wallets.Queries;

public sealed record GetTopUpHistoryQuery(
    Guid TenantId,
    Guid WalletId,
    int Page = 1,
    int PageSize = 20) : IRequest<GetTopUpHistoryResult>;
