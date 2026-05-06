using MediatR;

namespace PayFlow.Application.Payments.Queries;

public sealed record ListPaymentsQuery : IRequest<IReadOnlyCollection<PaymentSummary>>;
