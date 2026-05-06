using PayFlow.Domain.Entities;

namespace PayFlow.Application.Payments.DTOs;

public sealed record PaymentSummaryDto(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTime CreatedAt);
