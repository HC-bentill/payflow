namespace PayFlow.Application.Payments.Queries;

public sealed record GetWalletBalanceResult(
    Guid WalletId,
    string Currency,
    decimal Balance,
    DateTime ComputedAt);
