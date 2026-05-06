using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using PayFlow.Domain.Enums;

namespace PayFlow.Api.Tests.Auth;

public sealed class IssueTokenTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task Token_WithValidApiKey_ReturnsJwt()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var registration = await client.RegisterTenantAsync($"store-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync(
            "/v1/auth/token",
            new IssueTokenRequest(registration.ApiKey, TenantRole.Developer),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueTokenResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.TenantId.Should().Be(registration.TenantId);
    }

    [Fact]
    public async Task Token_WithInvalidApiKey_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/auth/token",
            new IssueTokenRequest("pf_live_invalid", TenantRole.Developer),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_WithMalformedApiKey_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/auth/token",
            new IssueTokenRequest("invalid", TenantRole.Developer),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_ContainsTenantRoleTierAndJtiClaims()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var registration = await client.RegisterTenantAsync($"store-{Guid.NewGuid():N}", "Pro");

        var token = await client.IssueTokenAsync(registration.ApiKey, TenantRole.Admin);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);
        var claims = jwt.Claims.ToDictionary(claim => claim.Type, claim => claim.Value);

        claims["tenant_id"].Should().Be(registration.TenantId.ToString());
        claims["role"].Should().Be("Admin");
        claims["tier"].Should().Be("Pro");
        claims.Should().ContainKey(JwtRegisteredClaimNames.Jti);
        claims.Should().ContainKey(JwtRegisteredClaimNames.Exp);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/payments", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var token = CreateExpiredToken(Guid.NewGuid());

        client.UseBearerToken(token);
        var response = await client.GetAsync("/v1/payments", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string CreateExpiredToken(Guid tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-minimum-32-chars-long"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: "payflow",
            audience: "payflow-api",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, tenantId.ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("role", "Developer"),
                new Claim("tier", "Free"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now.AddMinutes(-30),
            expires: now.AddMinutes(-1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
