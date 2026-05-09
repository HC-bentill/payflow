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

        builder.Property(payment => payment.SenderWalletId).IsRequired();
        builder.Property(payment => payment.ReceiverWalletId).IsRequired();
        builder.Property(payment => payment.SenderTenantId).IsRequired();
        builder.Property(payment => payment.ReceiverTenantId).IsRequired();

        builder.HasIndex(payment => payment.TenantId);
        builder.HasIndex(payment => payment.SenderTenantId);
        builder.HasIndex(payment => payment.ReceiverTenantId);

        builder.HasIndex(payment => new { payment.TenantId, payment.IdempotencyKey })
            .IsUnique();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(payment => payment.SenderWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(payment => payment.ReceiverWalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
