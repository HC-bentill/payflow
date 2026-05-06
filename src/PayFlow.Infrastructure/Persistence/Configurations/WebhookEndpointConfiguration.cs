using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence.Configurations;

public sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("webhook_endpoints");

        builder.HasKey(endpoint => endpoint.Id);

        builder.Property(endpoint => endpoint.Id)
            .ValueGeneratedNever();

        builder.Property(endpoint => endpoint.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(endpoint => endpoint.Secret)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(endpoint => endpoint.IsActive)
            .IsRequired();

        builder.Property(endpoint => endpoint.CreatedAt)
            .IsRequired();

        builder.HasIndex(endpoint => endpoint.TenantId);
    }
}
