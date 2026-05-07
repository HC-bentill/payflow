using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayFlow.Application.Common.Observability;
using PayFlow.Application.Common.RateLimiting;

namespace PayFlow.Infrastructure.Middleware;

public sealed class RateLimitingMiddleware(
    RequestDelegate next,
    IRateLimiter rateLimiter,
    IConfiguration configuration,
    IPayFlowMetrics metrics,
    ILogger<RateLimitingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.Equals(configuration["RateLimiting:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var rateLimitContext = GetRateLimitContext(context);
        if (rateLimitContext is null)
        {
            await next(context);
            return;
        }

        var result = await rateLimiter.CheckAsync(
            rateLimitContext.Key,
            rateLimitContext.Policy,
            context.RequestAborted);

        AddRateLimitHeaders(context, result);

        if (result.IsAllowed)
        {
            await next(context);
            return;
        }

        metrics.RecordRateLimitHit(rateLimitContext.Tier, rateLimitContext.Endpoint);
        logger.LogWarning(
            "Rate limit exceeded for tenant {TenantId}, subject {Subject}, tier {Tier}, endpoint {Endpoint}, limit {Limit}",
            rateLimitContext.TenantId,
            rateLimitContext.Subject,
            rateLimitContext.Tier,
            rateLimitContext.Endpoint,
            result.Limit);

        await WriteRateLimitExceededResponseAsync(context, result);
    }

    private RateLimitContext? GetRateLimitContext(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            var tier = context.User.FindFirst("tier")?.Value;
            var policy = string.Equals(tier, "Pro", StringComparison.OrdinalIgnoreCase)
                ? RateLimitPolicy.Pro(configuration)
                : RateLimitPolicy.Free(configuration);

            return new RateLimitContext(
                $"ratelimit:tenant:{tenantId}",
                policy,
                tenantId,
                string.IsNullOrWhiteSpace(tier) ? "Free" : tier,
                context.Request.Path.Value ?? string.Empty,
                tenantId);
        }

        var path = context.Request.Path.Value ?? string.Empty;
        RateLimitPolicy? authPolicy = path.ToLowerInvariant() switch
        {
            "/v1/auth/register" => RateLimitPolicy.AuthRegister(configuration),
            "/v1/auth/token" => RateLimitPolicy.AuthToken(configuration),
            _ => null
        };

        if (authPolicy is null)
        {
            return null;
        }

        var ipAddress = GetClientIpAddress(context);

        return new RateLimitContext(
            $"ratelimit:ip:{ipAddress}:{path.ToLowerInvariant()}",
            authPolicy,
            ipAddress,
            "anonymous",
            path,
            null);
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor.ToString()))
        {
            return forwardedFor.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
    }

    private static void AddRateLimitHeaders(HttpContext context, RateLimitResult result)
    {
        context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = result.ResetAt.ToUnixTimeSeconds().ToString();
    }

    private static async Task WriteRateLimitExceededResponseAsync(HttpContext context, RateLimitResult result)
    {
        var retryAfterSeconds = result.RetryAfterSeconds ?? 1;

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();

        var body = new
        {
            error = "rate_limit_exceeded",
            message = $"Too many requests. Please retry after {retryAfterSeconds} seconds.",
            retryAfter = retryAfterSeconds,
            limit = result.Limit,
            resetAt = result.ResetAt
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions), context.RequestAborted);
    }

    private sealed record RateLimitContext(
        string Key,
        RateLimitPolicy Policy,
        string Subject,
        string Tier,
        string Endpoint,
        string? TenantId);
}
