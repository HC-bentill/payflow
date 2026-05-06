using PayFlow.Application.Payments.DTOs;
using PayFlow.Domain.Entities;

namespace PayFlow.Application.Payments.Queries;

public sealed record GetPaymentResult(
    Guid PaymentId,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string? Description,
    string? Metadata,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<LedgerEntryDto> LedgerEntries);
