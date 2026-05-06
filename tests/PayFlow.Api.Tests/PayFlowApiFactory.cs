using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PayFlow.Domain.Interfaces;
using PayFlow.Infrastructure.Persistence;

namespace PayFlow.Api.Tests;

public sealed class PayFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName =
        Environment.GetEnvironmentVariable("PAYFLOW_TEST_DATABASE") ?? $"payflow-tests-{Guid.NewGuid()}";

    public PayFlowApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Postgres",
            "Host=localhost;Port=5433;Database=payflow_tests;Username=payflow;Password=devpassword");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
        Environment.SetEnvironmentVariable("Jwt__Key", "test-secret-key-minimum-32-chars-long");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "payflow");
        Environment.SetEnvironmentVariable("Jwt__Audience", "payflow-api");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=payflow_tests;Username=payflow;Password=devpassword",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Jwt:Key"] = "test-secret-key-minimum-32-chars-long",
                ["Jwt:Issuer"] = "payflow",
                ["Jwt:Audience"] = "payflow-api"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<PayFlowDbContext>();
            services.RemoveAll<IDatabaseProvider>();
            services.RemoveAll<IDbContextOptionsConfiguration<PayFlowDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<PayFlowDbContext>>();
            services.RemoveAll<IIdempotencyService>();
            services.RemoveAll<IEventPublisher>();

            services.AddDbContext<PayFlowDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddSingleton<IIdempotencyService, TestIdempotencyService>();
            services.AddSingleton<IEventPublisher, TestEventPublisher>();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        if (scope.ServiceProvider.GetService<IIdempotencyService>() is TestIdempotencyService idempotencyService)
        {
            idempotencyService.Clear();
        }
    }

    public async Task SeedAsync(Func<PayFlowDbContext, CancellationToken, Task> seed, CancellationToken ct = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();

        await seed(dbContext, ct);
    }

    private sealed class TestIdempotencyService : IIdempotencyService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly ConcurrentDictionary<string, string> cache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

        public Task<T?> GetCachedResponseAsync<T>(string tenantId, string idempotencyKey, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return Task.FromResult(
                cache.TryGetValue(GetCacheKey(tenantId, idempotencyKey), out var value)
                    ? JsonSerializer.Deserialize<T>(value, JsonOptions)
                    : default);
        }

        public Task CacheResponseAsync<T>(
            string tenantId,
            string idempotencyKey,
            T response,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            cache[GetCacheKey(tenantId, idempotencyKey)] = JsonSerializer.Serialize(response, JsonOptions);

            return Task.CompletedTask;
        }

        public async Task<bool> AcquireLockAsync(string tenantId, string idempotencyKey, CancellationToken ct)
        {
            var semaphore = locks.GetOrAdd(GetLockKey(tenantId, idempotencyKey), _ => new SemaphoreSlim(1, 1));

            return await semaphore.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }

        public Task ReleaseLockAsync(string tenantId, string idempotencyKey, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (locks.TryGetValue(GetLockKey(tenantId, idempotencyKey), out var semaphore))
            {
                semaphore.Release();
            }

            return Task.CompletedTask;
        }

        public void Clear()
        {
            cache.Clear();
            locks.Clear();
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

    private sealed class TestEventPublisher : IEventPublisher
    {
        public Task PublishAsync<T>(string topic, T @event, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }
}
