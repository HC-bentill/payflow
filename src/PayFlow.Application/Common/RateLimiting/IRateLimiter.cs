namespace PayFlow.Application.Common.RateLimiting;

public interface IRateLimiter
{
    Task<RateLimitResult> CheckAsync(string key, RateLimitPolicy policy, CancellationToken ct);
}
