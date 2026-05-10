using PayFlow.Domain.Enums;

namespace PayFlow.Application.Wallets.Queries;

public sealed record GetTopUpHistoryResult(
    IReadOnlyList<TopUpHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record TopUpHistoryItem(
    Guid TopUpId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    TopUpStatus Status,
    DateTime CreatedAt);
