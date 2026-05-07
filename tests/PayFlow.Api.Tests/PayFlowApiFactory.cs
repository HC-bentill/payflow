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
using PayFlow.Application.Common.RateLimiting;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Events;
using PayFlow.Domain.Interfaces;
using PayFlow.Domain.Messages;
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
                ["Kafka:Consumers:Enabled"] = "false",
                ["RateLimiting:Enabled"] = "true",
                ["RateLimiting:FreeTierLimit"] = "100",
                ["RateLimiting:ProTierLimit"] = "1000",
                ["RateLimiting:WindowSeconds"] = "60",
                ["RateLimiting:AuthRegisterLimit"] = "50",
                ["RateLimiting:AuthTokenLimit"] = "100",
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
            services.RemoveAll<IRateLimiter>();

            services.AddDbContext<PayFlowDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddSingleton<IIdempotencyService, TestIdempotencyService>();
            services.AddSingleton<TestRateLimiter>();
            services.AddSingleton<IRateLimiter>(provider => provider.GetRequiredService<TestRateLimiter>());
            services.AddSingleton<TestEventPublisher>();
            services.AddSingleton<IEventPublisher>(provider => provider.GetRequiredService<TestEventPublisher>());
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

        Services.GetRequiredService<TestEventPublisher>().Clear();
        Services.GetRequiredService<TestRateLimiter>().Clear();
    }

    public async Task SeedAsync(Func<PayFlowDbContext, CancellationToken, Task> seed, CancellationToken ct = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();

        await seed(dbContext, ct);
    }

    public IReadOnlyCollection<T> GetPublishedMessages<T>(string topic)
    {
        return Services.GetRequiredService<TestEventPublisher>().GetPublishedMessages<T>(topic);
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

    private sealed class TestEventPublisher(IServiceScopeFactory serviceScopeFactory) : IEventPublisher
    {
        private const string WebhookDeliveryTopic = "webhook.delivery";
        private readonly ConcurrentQueue<PublishedMessage> messages = new();

        public async Task PublishAsync<T>(string topic, T @event, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            messages.Enqueue(new PublishedMessage(topic, @event));

            if (topic == "payment.events" && @event is PaymentProcessedEvent paymentEvent)
            {
                await DispatchPaymentEventAsync(paymentEvent, ct);
            }
        }

        public IReadOnlyCollection<T> GetPublishedMessages<T>(string topic)
        {
            return messages
                .Where(message => message.Topic == topic)
                .Select(message => message.Value)
                .OfType<T>()
                .ToArray();
        }

        public void Clear()
        {
            messages.Clear();
        }

        private async Task DispatchPaymentEventAsync(PaymentProcessedEvent paymentEvent, CancellationToken ct)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var endpointRepository = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
            var deliveryLogRepository = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryLogRepository>();
            var endpoints = await endpointRepository.GetActiveByTenantAsync(paymentEvent.TenantId, ct);
            var payload = JsonSerializer.Serialize(paymentEvent, JsonOptions);
            var eventType = paymentEvent.Status == PaymentStatus.Succeeded
                ? "payment.succeeded"
                : $"payment.{paymentEvent.Status.ToString().ToLowerInvariant()}";
            var now = DateTime.UtcNow;

            foreach (var endpoint in endpoints)
            {
                var log = new WebhookDeliveryLog(
                    Guid.NewGuid(),
                    endpoint.Id,
                    paymentEvent.PaymentId,
                    paymentEvent.TenantId,
                    eventType,
                    payload,
                    WebhookDeliveryStatus.Pending,
                    0,
                    null,
                    null,
                    null,
                    null,
                    now,
                    now);

                await deliveryLogRepository.AddAsync(log, ct);

                var job = new WebhookDeliveryJob(
                    log.Id,
                    endpoint.Id,
                    paymentEvent.TenantId,
                    paymentEvent.PaymentId,
                    endpoint.Url,
                    endpoint.Secret,
                    eventType,
                    payload,
                    1,
                    now);

                messages.Enqueue(new PublishedMessage(WebhookDeliveryTopic, job));
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private sealed record PublishedMessage(string Topic, object? Value);
    }

    private sealed class TestRateLimiter : IRateLimiter
    {
        private readonly ConcurrentDictionary<string, List<long>> requestsByKey = new();

        public Task<RateLimitResult> CheckAsync(string key, RateLimitPolicy policy, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;
            var score = now.ToUnixTimeMilliseconds();
            var windowStart = score - policy.WindowSeconds * 1000L;
            var requests = requestsByKey.GetOrAdd(key, _ => []);
            int count;

            lock (requests)
            {
                requests.RemoveAll(timestamp => timestamp <= windowStart);
                requests.Add(score);
                count = requests.Count;
            }

            var resetAt = GetResetAt(now, policy.WindowSeconds);
            var isAllowed = count <= policy.Limit;
            var remaining = isAllowed
                ? Math.Max(0, policy.Limit - count)
                : 0;
            int? retryAfterSeconds = isAllowed
                ? null
                : Math.Max(1, (int)(resetAt - now).TotalSeconds + 1);

            return Task.FromResult(new RateLimitResult(
                isAllowed,
                policy.Limit,
                remaining,
                resetAt,
                retryAfterSeconds));
        }

        public void Clear()
        {
            requestsByKey.Clear();
        }

        private static DateTimeOffset GetResetAt(DateTimeOffset now, int windowSeconds)
        {
            var resetUnixSeconds = ((now.ToUnixTimeSeconds() / windowSeconds) + 1) * windowSeconds;

            return DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds);
        }
    }
}
