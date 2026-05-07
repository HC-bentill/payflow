using MediatR;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Webhooks.Queries;

public sealed class ListWebhookEndpointsHandler(IWebhookEndpointRepository webhookEndpointRepository)
    : IRequestHandler<ListWebhookEndpointsQuery, IReadOnlyList<WebhookEndpointDto>>
{
    public async Task<IReadOnlyList<WebhookEndpointDto>> Handle(
        ListWebhookEndpointsQuery request,
        CancellationToken cancellationToken)
    {
        var endpoints = await webhookEndpointRepository.GetByTenantAsync(request.TenantId, cancellationToken);

        return endpoints
            .OrderByDescending(endpoint => endpoint.CreatedAt)
            .Select(endpoint => new WebhookEndpointDto(
                endpoint.Id,
                endpoint.Url,
                endpoint.IsActive,
                endpoint.CreatedAt))
            .ToArray();
    }
}
