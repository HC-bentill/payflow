using MediatR;

namespace PayFlow.Application.Payments.Queries;

public sealed record GetWalletBalanceQuery(Guid TenantId, Guid WalletId) : IRequest<GetWalletBalanceResult>;
