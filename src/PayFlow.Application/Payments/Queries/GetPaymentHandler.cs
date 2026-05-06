using MediatR;
using PayFlow.Application.Common.Exceptions;
using PayFlow.Application.Payments.DTOs;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Payments.Queries;

public sealed class GetPaymentHandler(
    IPaymentRepository paymentRepository,
    ILedgerRepository ledgerRepository)
    : IRequestHandler<GetPaymentQuery, GetPaymentResult>
{
    public async Task<GetPaymentResult> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(
            request.TenantId,
            request.PaymentId,
            cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("Payment", request.PaymentId);
        }

        var ledgerEntries = await ledgerRepository.GetByPaymentAsync(payment.Id, cancellationToken);

        return new GetPaymentResult(
            payment.Id,
            payment.IdempotencyKey,
            payment.Amount,
            payment.Currency,
            payment.Status,
            payment.Description,
            payment.Metadata,
            payment.CreatedAt,
            payment.UpdatedAt,
            ledgerEntries
                .OrderBy(entry => entry.CreatedAt)
                .Select(entry => new LedgerEntryDto(
                    entry.Id,
                    entry.Type,
                    entry.Amount,
                    entry.Currency,
                    entry.CreatedAt))
                .ToArray());
    }
}
