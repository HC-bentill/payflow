namespace PayFlow.Api.Controllers.Models;

public sealed record RegisterWebhookEndpointRequest(string Url, string Secret);
