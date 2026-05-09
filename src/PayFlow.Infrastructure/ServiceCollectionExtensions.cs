using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Application.Common;
using PayFlow.Application.Common.Observability;
using PayFlow.Application.Common.RateLimiting;
using PayFlow.Application.Health;
using PayFlow.Domain.Interfaces;
using PayFlow.Infrastructure.Health;
using PayFlow.Infrastructure.Messaging;
using PayFlow.Infrastructure.Messaging.Consumers;
using PayFlow.Infrastructure.Observability;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Persistence.Repositories;
using PayFlow.Infrastructure.RateLimiting;
using PayFlow.Infrastructure.Services;
using StackExchange.Redis;

namespace PayFlow.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(postgresConnectionString))
        {
            throw new InvalidOperationException("Connection string 'Postgres' is required.");
        }

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Connection string 'Redis' is required.");
        }

        services.AddDbContext<PayFlowDbContext>(options => options.UseNpgsql(postgresConnectionString));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
        services.AddScoped<IWebhookDeliveryLogRepository, WebhookDeliveryLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IHealthProbeService, HealthProbeService>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<IPayFlowMetrics, PayFlowMetrics>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<KafkaConsumerFactory>();
        services.AddHttpClient("webhook-delivery", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;

            return ConnectionMultiplexer.Connect(options);
        });

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            Acks = Acks.All,
            EnableIdempotence = true
        };

        services.AddSingleton<IProducer<string, string>>(
            _ => new ProducerBuilder<string, string>(producerConfig).Build());

        if (configuration.GetValue("Kafka:Consumers:Enabled", true))
        {
            services.AddHostedService<WebhookDispatchConsumer>();
            services.AddHostedService<WebhookDeliveryWorker>();
            services.AddHostedService<DLQMonitorConsumer>();
            services.AddHostedService<NotificationConsumer>();
        }

        services.AddHostedService<KafkaLagMonitor>();

        return services;
    }
}
