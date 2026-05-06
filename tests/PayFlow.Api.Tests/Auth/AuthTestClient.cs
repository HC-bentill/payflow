using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PayFlow.Domain.Enums;

namespace PayFlow.Api.Tests.Auth;

internal static class AuthTestClient
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<RegisterTenantResponse> RegisterTenantAsync(
        this HttpClient client,
        string name,
        string tier = "Free",
        CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync(
            "/v1/auth/register",
            new RegisterTenantRequest(name, tier),
            JsonOptions,
            ct);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions, ct))!;
    }

    public static async Task<IssueTokenResponse> IssueTokenAsync(
        this HttpClient client,
        string apiKey,
        TenantRole role = TenantRole.Developer,
        CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync(
            "/v1/auth/token",
            new IssueTokenRequest(apiKey, role),
            JsonOptions,
            ct);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<IssueTokenResponse>(JsonOptions, ct))!;
    }

    public static void UseBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}

internal sealed record RegisterTenantRequest(string Name, string Tier);

internal sealed record IssueTokenRequest(string ApiKey, TenantRole Role);

internal sealed record RegisterTenantResponse(Guid TenantId, string ApiKey, string Message);

internal sealed record IssueTokenResponse(string Token, DateTime ExpiresAt, Guid TenantId);
