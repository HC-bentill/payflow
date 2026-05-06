using MediatR;

namespace PayFlow.Application.Payments.Queries;

public sealed record GetPaymentQuery(Guid PaymentId, Guid TenantId) : IRequest<GetPaymentResult>;
