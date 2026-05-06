using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id)
            .ValueGeneratedNever();

        builder.Property(payment => payment.IdempotencyKey)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(payment => payment.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(payment => payment.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(payment => payment.Description)
            .HasMaxLength(500);

        builder.Property(payment => payment.Metadata)
            .HasColumnType("jsonb");

        builder.Property(payment => payment.CreatedAt)
            .IsRequired();

        builder.Property(payment => payment.UpdatedAt)
            .IsRequired();

        builder.HasIndex(payment => payment.TenantId);

        builder.HasIndex(payment => new { payment.TenantId, payment.IdempotencyKey })
            .IsUnique();
    }
}
