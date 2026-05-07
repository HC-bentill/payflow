using MediatR;

namespace PayFlow.Application.Webhooks.Commands;

public sealed record ReplayWebhookCommand(Guid DeliveryLogId, Guid TenantId) : IRequest<ReplayWebhookResult>;
