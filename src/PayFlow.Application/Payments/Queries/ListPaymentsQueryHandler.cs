using MediatR;
using PayFlow.Application.Common;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Payments.Queries;

public sealed class ListPaymentsQueryHandler(IPaymentRepository paymentRepository, ITenantContext tenantContext)
    : IRequestHandler<ListPaymentsQuery, IReadOnlyCollection<PaymentSummary>>
{
    public async Task<IReadOnlyCollection<PaymentSummary>> Handle(
        ListPaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var payments = await paymentRepository.ListByTenantAsync(tenantContext.CurrentTenant.Id, cancellationToken);

        return payments
            .Select(payment => new PaymentSummary(
                payment.Id,
                payment.TenantId,
                payment.Amount,
                payment.Currency,
                payment.Status,
                payment.Description,
                payment.CreatedAt))
            .ToArray();
    }
}
