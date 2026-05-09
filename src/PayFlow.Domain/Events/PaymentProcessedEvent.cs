using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Events;

public sealed record PaymentProcessedEvent(
    Guid PaymentId,
    Guid TenantId,
    Guid SenderWalletId,
    Guid ReceiverWalletId,
    Guid SenderTenantId,
    Guid ReceiverTenantId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string IdempotencyKey,
    DateTime OccurredAt);
