using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Application.Health;
using PayFlow.Application.Common;
using PayFlow.Domain.Interfaces;
using PayFlow.Infrastructure.Health;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Persistence.Repositories;
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
        services.AddScoped<IHealthProbeService, HealthProbeService>();
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;

            return ConnectionMultiplexer.Connect(options);
        });

        return services;
    }
}
