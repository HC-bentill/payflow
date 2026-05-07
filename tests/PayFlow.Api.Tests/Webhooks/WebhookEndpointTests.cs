using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;

namespace PayFlow.Api.Tests.Webhooks;

public sealed class WebhookEndpointTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task RegisterEndpoint_WithValidHttpsUrl_ReturnsCreated()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.PostAsJsonAsync(
            "/v1/webhooks/endpoints",
            new RegisterWebhookEndpointRequest("https://example.com/webhooks/payflow", "secret-min-16-chars"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterWebhookEndpointResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        body.Should().NotBeNull();
        body!.EndpointId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RegisterEndpoint_WithHttpUrl_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.PostAsJsonAsync(
            "/v1/webhooks/endpoints",
            new RegisterWebhookEndpointRequest("http://example.com/webhooks/payflow", "secret-min-16-chars"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterEndpoint_WithShortSecret_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.PostAsJsonAsync(
            "/v1/webhooks/endpoints",
            new RegisterWebhookEndpointRequest("https://example.com/webhooks/payflow", "short"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListEndpoints_ReturnsEndpointsForTenantOnly()
    {
        await factory.ResetDatabaseAsync();
        var tenantA = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-a-{Guid.NewGuid():N}");
        var tenantB = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-b-{Guid.NewGuid():N}");

        var tenantAResponse = await tenantA.Client.PostAsJsonAsync(
            "/v1/webhooks/endpoints",
            new RegisterWebhookEndpointRequest("https://a.example.com/webhooks/payflow", "tenant-a-secret-16"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        var tenantBResponse = await tenantB.Client.PostAsJsonAsync(
            "/v1/webhooks/endpoints",
            new RegisterWebhookEndpointRequest("https://b.example.com/webhooks/payflow", "tenant-b-secret-16"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        var tenantAEndpoint = await tenantAResponse.Content.ReadFromJsonAsync<RegisterWebhookEndpointResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        var tenantBEndpoint = await tenantBResponse.Content.ReadFromJsonAsync<RegisterWebhookEndpointResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        var response = await tenantA.Client.GetAsync("/v1/webhooks/endpoints", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var endpoints = await response.Content.ReadFromJsonAsync<WebhookEndpointResponse[]>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        endpoints.Should().NotBeNull();
        endpoints!.Select(endpoint => endpoint.WebhookEndpointId).Should().Contain(tenantAEndpoint!.EndpointId);
        endpoints.Select(endpoint => endpoint.WebhookEndpointId).Should().NotContain(tenantBEndpoint!.EndpointId);
    }
}

internal sealed record RegisterWebhookEndpointRequest(string Url, string Secret);

internal sealed record RegisterWebhookEndpointResponse(Guid EndpointId);

internal sealed record WebhookEndpointResponse(
    Guid WebhookEndpointId,
    string Url,
    bool IsActive,
    DateTime CreatedAt);
