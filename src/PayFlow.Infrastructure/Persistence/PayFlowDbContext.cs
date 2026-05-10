using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence;

public sealed class PayFlowDbContext(DbContextOptions<PayFlowDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<Wallet> Wallets => Set<Wallet>();

    public DbSet<TopUp> TopUps => Set<TopUp>();

    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();

    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayFlowDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Payment>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.MarkUpdated(now);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
