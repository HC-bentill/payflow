using PayFlow.Application.Webhooks.Commands;

namespace PayFlow.Api.Controllers.Models;

public sealed record RegisterWebhookEndpointResponse(Guid EndpointId)
{
    public static implicit operator RegisterWebhookEndpointResponse(RegisterWebhookEndpointResult result)
    {
        return new RegisterWebhookEndpointResponse(result.WebhookEndpointId);
    }
}
