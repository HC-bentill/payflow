using MediatR;

namespace PayFlow.Application.Webhooks.Commands;

public sealed record RegisterWebhookEndpointCommand(Guid TenantId, string Url, string Secret)
    : IRequest<RegisterWebhookEndpointResult>;
