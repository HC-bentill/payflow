using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            services.AddDbContext<PayFlowDbContext>(options => options.UseInMemoryDatabase(databaseName));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task SeedAsync(Func<PayFlowDbContext, CancellationToken, Task> seed, CancellationToken ct = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();

        await seed(dbContext, ct);
    }
}
