namespace PayFlow.Application.Wallets.Commands;

public sealed record TopUpWalletResult(
    Guid TopUpId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    decimal NewBalance,
    bool IsReplay,
    DateTime CreatedAt);
