using MediatR;

namespace PayFlow.Application.Wallets.Commands;

public sealed record TopUpWalletCommand(
    Guid TenantId,
    Guid WalletId,
    string IdempotencyKey,
    decimal Amount,
    string Currency) : IRequest<TopUpWalletResult>;
