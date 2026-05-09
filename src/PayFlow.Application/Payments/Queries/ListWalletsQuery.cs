using MediatR;

namespace PayFlow.Application.Payments.Queries;

public sealed record ListWalletsQuery(Guid TenantId) : IRequest<IReadOnlyList<WalletSummaryResult>>;

public sealed record WalletSummaryResult(
    Guid WalletId,
    string Currency,
    decimal Balance,
    DateTime CreatedAt);
