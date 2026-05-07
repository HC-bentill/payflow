using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryLogConfiguration : IEntityTypeConfiguration<WebhookDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryLog> builder)
    {
        builder.ToTable("webhook_delivery_logs");

        builder.HasKey(log => log.Id);

        builder.Property(log => log.Id)
            .ValueGeneratedNever();

        builder.Property(log => log.EventType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(log => log.Payload)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(log => log.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(log => log.ResponseBody)
            .HasMaxLength(500);

        builder.Property(log => log.CreatedAt)
            .IsRequired();

        builder.Property(log => log.UpdatedAt)
            .IsRequired();

        builder.HasIndex(log => log.TenantId);

        builder.HasIndex(log => new { log.WebhookEndpointId, log.Status });

        builder.HasIndex(log => log.PaymentId);
    }
}
