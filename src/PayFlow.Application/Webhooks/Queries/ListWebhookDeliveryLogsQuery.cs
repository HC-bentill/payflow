using MediatR;

namespace PayFlow.Application.Webhooks.Queries;

public sealed record ListWebhookDeliveryLogsQuery(
    Guid TenantId,
    int Page = 1,
    int PageSize = 20,
    bool FailedOnly = false) : IRequest<ListWebhookDeliveryLogsResult>;
