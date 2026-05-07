namespace PayFlow.Application.Webhooks.Queries;

public sealed record WebhookEndpointDto(
    Guid WebhookEndpointId,
    string Url,
    bool IsActive,
    DateTime CreatedAt);
