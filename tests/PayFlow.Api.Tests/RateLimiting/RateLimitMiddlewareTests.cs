using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;
using PayFlow.Domain.Enums;

namespace PayFlow.Api.Tests.RateLimiting;

public sealed class RateLimitMiddlewareTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task FreeTier_AllowsOneHundredRequestsAndRejectsOneHundredFirst()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        for (var i = 0; i < 100; i++)
        {
            var response = await authenticated.Client.GetAsync("/v1/payments", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            GetHeader(response, "X-RateLimit-Limit").Should().Be("100");
            GetHeader(response, "X-RateLimit-Remaining").Should().Be((99 - i).ToString());

            var reset = long.Parse(GetHeader(response, "X-RateLimit-Reset"));
            reset.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        var rejected = await authenticated.Client.GetAsync("/v1/payments", CancellationToken.None);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.TryGetValues("Retry-After", out var retryAfterValues).Should().BeTrue();
        retryAfterValues.Should().NotBeEmpty();
        GetHeader(rejected, "X-RateLimit-Limit").Should().Be("100");
        GetHeader(rejected, "X-RateLimit-Remaining").Should().Be("0");

        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(CancellationToken.None));
        body.RootElement.GetProperty("error").GetString().Should().Be("rate_limit_exceeded");
    }

    [Fact]
    public async Task FreeTier_ConcurrentRequests_AllowExactlyConfiguredLimit()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 110)
                .Select(_ => authenticated.Client.GetAsync("/v1/payments", CancellationToken.None)));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(100);
        responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests).Should().Be(10);
    }

    [Fact]
    public async Task ProTier_AllowsOneHundredOneRequestsWithProLimitHeader()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var registration = await client.RegisterTenantAsync($"pro-{Guid.NewGuid():N}", "Pro");
        var token = await client.IssueTokenAsync(registration.ApiKey, TenantRole.Developer);
        client.UseBearerToken(token.Token);

        for (var i = 0; i < 101; i++)
        {
            var response = await client.GetAsync("/v1/payments", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            GetHeader(response, "X-RateLimit-Limit").Should().Be("1000");
        }
    }

    [Fact]
    public async Task AuthTokenEndpoint_AddsRateLimitHeaders()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var registration = await client.RegisterTenantAsync($"auth-token-{Guid.NewGuid():N}");

        for (var i = 0; i < 20; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/v1/auth/token",
                new IssueTokenRequest(registration.ApiKey, TenantRole.Developer),
                AuthTestClient.JsonOptions,
                CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            GetHeader(response, "X-RateLimit-Limit").Should().Be("100");
            response.Headers.Contains("X-RateLimit-Remaining").Should().BeTrue();
            response.Headers.Contains("X-RateLimit-Reset").Should().BeTrue();
        }
    }

    [Fact]
    public async Task AuthenticatedResponse_IncludesRateLimitHeadersAndRemainingDecrements()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var first = await authenticated.Client.GetAsync("/v1/payments", CancellationToken.None);
        var second = await authenticated.Client.GetAsync("/v1/payments", CancellationToken.None);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        GetHeader(first, "X-RateLimit-Limit").Should().Be("100");
        GetHeader(first, "X-RateLimit-Remaining").Should().Be("99");
        GetHeader(second, "X-RateLimit-Remaining").Should().Be("98");
        GetHeader(second, "X-RateLimit-Reset").Should().Be(GetHeader(first, "X-RateLimit-Reset"));
    }

    [Fact]
    public async Task UnauthenticatedUnknownPath_SkipsRateLimiting()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/unknown-rate-limit-path", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Contains("X-RateLimit-Limit").Should().BeFalse();
        response.Headers.Contains("X-RateLimit-Remaining").Should().BeFalse();
        response.Headers.Contains("X-RateLimit-Reset").Should().BeFalse();
    }

    [Fact]
    public async Task DisabledRateLimiting_SkipsHeadersAndLimits()
    {
        await factory.ResetDatabaseAsync();
        await using var disabledFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Enabled"] = "false"
                });
            });
        });
        var client = disabledFactory.CreateClient();
        var registration = await client.RegisterTenantAsync($"disabled-{Guid.NewGuid():N}");
        var token = await client.IssueTokenAsync(registration.ApiKey, TenantRole.Developer);
        client.UseBearerToken(token.Token);

        for (var i = 0; i < 105; i++)
        {
            var response = await client.GetAsync("/v1/payments", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.Contains("X-RateLimit-Limit").Should().BeFalse();
            response.Headers.Contains("X-RateLimit-Remaining").Should().BeFalse();
            response.Headers.Contains("X-RateLimit-Reset").Should().BeFalse();
        }
    }

    private static string GetHeader(HttpResponseMessage response, string name)
    {
        response.Headers.TryGetValues(name, out var values).Should().BeTrue();

        return values!.Single();
    }
}
