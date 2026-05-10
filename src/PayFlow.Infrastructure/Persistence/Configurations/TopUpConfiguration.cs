using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence.Configurations;

public sealed class TopUpConfiguration : IEntityTypeConfiguration<TopUp>
{
    public void Configure(EntityTypeBuilder<TopUp> builder)
    {
        builder.ToTable("top_ups");

        builder.HasKey(topUp => topUp.Id);

        builder.Property(topUp => topUp.Id)
            .ValueGeneratedNever();

        builder.Property(topUp => topUp.IdempotencyKey)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(topUp => topUp.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(topUp => topUp.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(topUp => topUp.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(topUp => topUp.CreatedAt)
            .IsRequired();

        builder.Property(topUp => topUp.UpdatedAt)
            .IsRequired();

        builder.HasIndex(topUp => new { topUp.WalletId, topUp.IdempotencyKey })
            .IsUnique();

        builder.HasIndex(topUp => topUp.WalletId);
        builder.HasIndex(topUp => topUp.TenantId);

        builder.HasOne(topUp => topUp.Wallet)
            .WithMany()
            .HasForeignKey(topUp => topUp.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
