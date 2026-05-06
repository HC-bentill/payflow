namespace PayFlow.Domain.Interfaces;

public interface IIdempotencyService
{
    Task<T?> GetCachedResponseAsync<T>(string tenantId, string idempotencyKey, CancellationToken ct);

    Task CacheResponseAsync<T>(string tenantId, string idempotencyKey, T response, CancellationToken ct);

    Task<bool> AcquireLockAsync(string tenantId, string idempotencyKey, CancellationToken ct);

    Task ReleaseLockAsync(string tenantId, string idempotencyKey, CancellationToken ct);
}
