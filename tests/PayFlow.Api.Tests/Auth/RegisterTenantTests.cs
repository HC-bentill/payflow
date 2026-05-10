using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Payments;

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
    public async Task Register_WithValidData_CreatesDefaultUsdWallet()
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

        var token = await client.IssueTokenAsync(body!.ApiKey);
        client.UseBearerToken(token.Token);

        var walletsResponse = await client.GetAsync("/v1/payments/wallets", CancellationToken.None);
        walletsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var wallets = await walletsResponse.Content.ReadFromJsonAsync<WalletSummaryResponse[]>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        var wallet = wallets.Should().ContainSingle().Subject;
        wallet.Currency.Should().Be("USD");
        wallet.Balance.Should().Be(0.00m);
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
