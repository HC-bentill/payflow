namespace PayFlow.Application.Payments.Queries;

public sealed record GetWalletBalanceResult(
    Guid TenantId,
    string Currency,
    decimal Balance,
    DateTime ComputedAt);
