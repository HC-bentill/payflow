using MediatR;

namespace PayFlow.Application.Payments.Commands;

public sealed record CreatePaymentCommand(
    Guid TenantId,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    string? Description,
    string? Metadata) : IRequest<CreatePaymentResult>;
