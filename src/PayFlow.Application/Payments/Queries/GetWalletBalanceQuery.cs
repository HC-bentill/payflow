using MediatR;

namespace PayFlow.Application.Payments.Queries;

public sealed record GetWalletBalanceQuery(Guid TenantId, string Currency) : IRequest<GetWalletBalanceResult>;
