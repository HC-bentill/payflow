using PayFlow.Application.Common.RateLimiting;
using StackExchange.Redis;

namespace PayFlow.Infrastructure.RateLimiting;

public sealed class RedisRateLimiter(IConnectionMultiplexer redis) : IRateLimiter
{
    private const string SlidingWindowScript = """
local key = KEYS[1]
local window_start = tonumber(ARGV[1])
local score = tonumber(ARGV[2])
local member = ARGV[3]
local limit = tonumber(ARGV[4])
local ttl = tonumber(ARGV[5])

redis.call('ZREMRANGEBYSCORE', key, '-inf', window_start)
redis.call('ZADD', key, score, member)
local count = redis.call('ZCARD', key)
redis.call('EXPIRE', key, ttl)

return count
""";

    public async Task<RateLimitResult> CheckAsync(string key, RateLimitPolicy policy, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var score = now.ToUnixTimeMilliseconds();
        var windowStart = score - policy.WindowSeconds * 1000L;
        var windowKey = now.ToUnixTimeSeconds() / policy.WindowSeconds;
        var redisKey = $"{key}:{windowKey}";
        var ttl = policy.WindowSeconds * 2;

        var countResult = await redis.GetDatabase()
            .ScriptEvaluateAsync(
                SlidingWindowScript,
                keys: [redisKey],
                values:
                [
                    windowStart,
                    score,
                    Guid.NewGuid().ToString(),
                    policy.Limit,
                    ttl
                ])
            .WaitAsync(ct);

        var count = (long)countResult;
        var resetAt = GetResetAt(now, policy.WindowSeconds);
        var isAllowed = count <= policy.Limit;
        var remaining = isAllowed
            ? Math.Max(0, policy.Limit - (int)count)
            : 0;
        int? retryAfterSeconds = isAllowed
            ? null
            : Math.Max(1, (int)(resetAt - now).TotalSeconds + 1);

        return new RateLimitResult(
            isAllowed,
            policy.Limit,
            remaining,
            resetAt,
            retryAfterSeconds);
    }

    private static DateTimeOffset GetResetAt(DateTimeOffset now, int windowSeconds)
    {
        var resetUnixSeconds = ((now.ToUnixTimeSeconds() / windowSeconds) + 1) * windowSeconds;

        return DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds);
    }
}
