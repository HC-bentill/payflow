using PayFlow.Application.Payments.DTOs;

namespace PayFlow.Application.Payments.Queries;

public sealed record ListPaymentsResult(
    IReadOnlyList<PaymentSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
