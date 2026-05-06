using MediatR;

namespace PayFlow.Application.Payments.Queries;

public sealed record ListPaymentsQuery(Guid TenantId, int Page = 1, int PageSize = 20)
    : IRequest<ListPaymentsResult>;
