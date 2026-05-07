namespace PayFlow.Application.Webhooks.Queries;

public sealed record ListWebhookDeliveryLogsResult(
    IReadOnlyList<WebhookDeliveryLogDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
