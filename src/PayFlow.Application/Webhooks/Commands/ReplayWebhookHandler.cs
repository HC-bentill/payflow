using MediatR;
using PayFlow.Application.Common.Exceptions;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Interfaces;
using PayFlow.Domain.Messages;

namespace PayFlow.Application.Webhooks.Commands;

public sealed class ReplayWebhookHandler(
    IWebhookDeliveryLogRepository webhookDeliveryLogRepository,
    IWebhookEndpointRepository webhookEndpointRepository,
    IEventPublisher eventPublisher)
    : IRequestHandler<ReplayWebhookCommand, ReplayWebhookResult>
{
    private const string WebhookDeliveryTopic = "webhook.delivery";

    public async Task<ReplayWebhookResult> Handle(ReplayWebhookCommand request, CancellationToken cancellationToken)
    {
        var log = await webhookDeliveryLogRepository.GetByIdAsync(request.DeliveryLogId, cancellationToken);
        if (log is null || log.TenantId != request.TenantId)
        {
            throw new NotFoundException("Webhook delivery log", request.DeliveryLogId);
        }

        if (log.Status is not (WebhookDeliveryStatus.Failed or WebhookDeliveryStatus.PermanentlyFailed))
        {
            throw new InvalidOperationException("Only failed webhook deliveries can be replayed");
        }

        var endpoint = await webhookEndpointRepository.GetByIdAsync(log.WebhookEndpointId, cancellationToken);
        if (endpoint is null || endpoint.TenantId != request.TenantId)
        {
            throw new NotFoundException("Webhook endpoint", log.WebhookEndpointId);
        }

        var now = DateTime.UtcNow;
        log.ResetForReplay(now);
        await webhookDeliveryLogRepository.UpdateAsync(log, cancellationToken);

        var job = new WebhookDeliveryJob(
            log.Id,
            endpoint.Id,
            log.TenantId,
            log.PaymentId,
            endpoint.Url,
            endpoint.Secret,
            log.EventType,
            log.Payload,
            1,
            now);

        await eventPublisher.PublishAsync(WebhookDeliveryTopic, job, cancellationToken);

        return new ReplayWebhookResult(true);
    }
}
