using PayFlow.Domain.Entities;

namespace PayFlow.Application.Payments.Queries;

public sealed record PaymentSummary(
    Guid Id,
    Guid TenantId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string? Description,
    DateTime CreatedAt);
