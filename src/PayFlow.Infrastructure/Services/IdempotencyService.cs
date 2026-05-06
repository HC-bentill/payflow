using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PayFlow.Domain.Interfaces;
using StackExchange.Redis;

namespace PayFlow.Infrastructure.Services;

public sealed class IdempotencyService(IConnectionMultiplexer redis) : IIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockPollInterval = TimeSpan.FromMilliseconds(100);

    public async Task<T?> GetCachedResponseAsync<T>(string tenantId, string idempotencyKey, CancellationToken ct)
    {
        var value = await redis.GetDatabase()
            .StringGetAsync(GetCacheKey(tenantId, idempotencyKey))
            .WaitAsync(ct);

        return value.IsNull
            ? default
            : JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
    }

    public async Task CacheResponseAsync<T>(
        string tenantId,
        string idempotencyKey,
        T response,
        CancellationToken ct)
    {
        var serialized = JsonSerializer.Serialize(response, JsonOptions);

        await redis.GetDatabase()
            .StringSetAsync(GetCacheKey(tenantId, idempotencyKey), serialized, CacheTtl)
            .WaitAsync(ct);
    }

    public async Task<bool> AcquireLockAsync(string tenantId, string idempotencyKey, CancellationToken ct)
    {
        var database = redis.GetDatabase();
        var lockKey = GetLockKey(tenantId, idempotencyKey);
        var lockValue = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < LockWaitTimeout)
        {
            var acquired = await database
                .StringSetAsync(lockKey, lockValue, LockTtl, When.NotExists)
                .WaitAsync(ct);

            if (acquired)
            {
                return true;
            }

            var remaining = LockWaitTimeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < LockPollInterval ? remaining : LockPollInterval, ct);
        }

        return false;
    }

    public async Task ReleaseLockAsync(string tenantId, string idempotencyKey, CancellationToken ct)
    {
        await redis.GetDatabase()
            .KeyDeleteAsync(GetLockKey(tenantId, idempotencyKey))
            .WaitAsync(ct);
    }

    private static string GetCacheKey(string tenantId, string idempotencyKey)
    {
        return $"idempotency:{tenantId}:{idempotencyKey}";
    }

    private static string GetLockKey(string tenantId, string idempotencyKey)
    {
        return $"lock:{tenantId}:{idempotencyKey}";
    }
}
