using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Health;
using PayFlow.Infrastructure.Persistence;
using StackExchange.Redis;

namespace PayFlow.Infrastructure.Health;

public sealed class HealthProbeService(PayFlowDbContext dbContext, IConnectionMultiplexer redis)
    : IHealthProbeService
{
    public async Task<DependencyHealthChecks> CheckAsync(CancellationToken ct)
    {
        var postgres = await CheckPostgresAsync(ct);
        var redisHealthy = await CheckRedisAsync(ct);

        return new DependencyHealthChecks(postgres, redisHealthy);
    }

    private async Task<bool> CheckPostgresAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(ct);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckRedisAsync(CancellationToken ct)
    {
        try
        {
            await redis.GetDatabase().PingAsync().WaitAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
