using PayFlow.Domain.Entities;

namespace PayFlow.Application.Payments.Commands;

public sealed record CreatePaymentResult(
    Guid PaymentId,
    Guid SenderWalletId,
    Guid ReceiverWalletId,
    Guid SenderTenantId,
    Guid ReceiverTenantId,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTime CreatedAt,
    bool IsReplay)
{
    public string? Description { get; init; }
}
