using MediatR;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Webhooks.Commands;

public sealed class RegisterWebhookEndpointHandler(IWebhookEndpointRepository webhookEndpointRepository)
    : IRequestHandler<RegisterWebhookEndpointCommand, RegisterWebhookEndpointResult>
{
    public async Task<RegisterWebhookEndpointResult> Handle(
        RegisterWebhookEndpointCommand request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Webhook endpoint URL must be a valid HTTPS URL");
        }

        if (request.Secret.Length < 16)
        {
            throw new InvalidOperationException("Webhook endpoint secret must be at least 16 characters");
        }

        var endpoint = new WebhookEndpoint(
            Guid.NewGuid(),
            request.TenantId,
            request.Url,
            request.Secret,
            true,
            DateTime.UtcNow);

        await webhookEndpointRepository.AddAsync(endpoint, cancellationToken);

        return new RegisterWebhookEndpointResult(endpoint.Id);
    }
}
