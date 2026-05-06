using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace PayFlow.Api.Tests.Auth;

public sealed class RegisterTenantTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task Register_WithValidData_ReturnsCreatedWithApiKey()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/auth/register",
            new RegisterTenantRequest($"store-{Guid.NewGuid():N}", "Free"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        body.Should().NotBeNull();
        body!.TenantId.Should().NotBeEmpty();
        body.ApiKey.Should().StartWith("pf_live_");
    }

    [Fact]
    public async Task Register_WithDuplicateName_ReturnsConflict()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var name = $"store-{Guid.NewGuid():N}";

        await client.RegisterTenantAsync(name);
        var response = await client.PostAsJsonAsync(
            "/v1/auth/register",
            new RegisterTenantRequest(name, "Free"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithEmptyName_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/auth/register",
            new RegisterTenantRequest(string.Empty, "Free"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithTooShortName_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/auth/register",
            new RegisterTenantRequest("ab", "Free"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
