using PayFlow.Domain.Entities;

namespace PayFlow.Application.Payments.DTOs;

public sealed record LedgerEntryDto(
    Guid Id,
    Guid WalletId,
    LedgerEntryType Type,
    decimal Amount,
    string Currency,
    DateTime CreatedAt);
