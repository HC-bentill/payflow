namespace PayFlow.Domain.Events;

public sealed record WalletToppedUpEvent(
    Guid TopUpId,
    Guid WalletId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    decimal NewBalance,
    DateTime OccurredAt);
