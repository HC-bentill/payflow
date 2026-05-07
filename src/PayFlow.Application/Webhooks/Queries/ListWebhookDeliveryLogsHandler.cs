using MediatR;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Webhooks.Queries;

public sealed class ListWebhookDeliveryLogsHandler(IWebhookDeliveryLogRepository webhookDeliveryLogRepository)
    : IRequestHandler<ListWebhookDeliveryLogsQuery, ListWebhookDeliveryLogsResult>
{
    public async Task<ListWebhookDeliveryLogsResult> Handle(
        ListWebhookDeliveryLogsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var logs = request.FailedOnly
            ? await webhookDeliveryLogRepository.GetFailedByTenantAsync(
                request.TenantId,
                page,
                pageSize,
                cancellationToken)
            : await webhookDeliveryLogRepository.GetByTenantAsync(
                request.TenantId,
                page,
                pageSize,
                cancellationToken);
        var totalCount = request.FailedOnly
            ? await webhookDeliveryLogRepository.GetFailedCountByTenantAsync(request.TenantId, cancellationToken)
            : await webhookDeliveryLogRepository.GetCountByTenantAsync(request.TenantId, cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new ListWebhookDeliveryLogsResult(
            logs
                .Select(log => new WebhookDeliveryLogDto(
                    log.Id,
                    log.WebhookEndpointId,
                    log.PaymentId,
                    log.EventType,
                    log.Status,
                    log.AttemptCount,
                    log.LastAttemptAt,
                    log.NextRetryAt,
                    log.ResponseStatusCode,
                    log.ResponseBody,
                    log.CreatedAt,
                    log.UpdatedAt))
                .ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}
