using MediatR;
using PayFlow.Application.Payments.DTOs;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Payments.Queries;

public sealed class ListPaymentsQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<ListPaymentsQuery, ListPaymentsResult>
{
    public async Task<ListPaymentsResult> Handle(
        ListPaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = await paymentRepository.GetCountByTenantAsync(request.TenantId, cancellationToken);
        var payments = await paymentRepository.GetByTenantAsync(
            request.TenantId,
            page,
            pageSize,
            cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new ListPaymentsResult(
            payments
            .Select(payment => new PaymentSummaryDto(
                payment.Id,
                payment.Amount,
                payment.Currency,
                payment.Status,
                payment.CreatedAt))
            .ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}
