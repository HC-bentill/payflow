using PayFlow.Application.Payments.Commands;
using PayFlow.Domain.Entities;

namespace PayFlow.Api.Controllers.Models;

public sealed record PaymentResponse(
    Guid PaymentId,
    Guid SenderWalletId,
    Guid ReceiverWalletId,
    Guid SenderTenantId,
    Guid ReceiverTenantId,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string? Description,
    DateTime CreatedAt)
{
    public static implicit operator PaymentResponse(CreatePaymentResult result)
    {
        return new PaymentResponse(
            result.PaymentId,
            result.SenderWalletId,
            result.ReceiverWalletId,
            result.SenderTenantId,
            result.ReceiverTenantId,
            result.IdempotencyKey,
            result.Amount,
            result.Currency,
            result.Status,
            result.Description,
            result.CreatedAt);
    }
}
