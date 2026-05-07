using MediatR;

namespace PayFlow.Application.Webhooks.Queries;

public sealed record ListWebhookEndpointsQuery(Guid TenantId) : IRequest<IReadOnlyList<WebhookEndpointDto>>;
