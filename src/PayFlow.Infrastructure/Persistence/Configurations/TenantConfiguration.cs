using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Id)
            .ValueGeneratedNever();

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(tenant => tenant.ApiKeyHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(tenant => tenant.Tier)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(tenant => tenant.CreatedAt)
            .IsRequired();

        builder.Property(tenant => tenant.IsActive)
            .IsRequired();

        builder.HasIndex(tenant => tenant.ApiKeyHash);
    }
}
