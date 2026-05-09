using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallets");

        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.Id)
            .ValueGeneratedNever();

        builder.Property(wallet => wallet.OwnerId)
            .IsRequired();

        builder.Property(wallet => wallet.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(wallet => wallet.CreatedAt)
            .IsRequired();

        builder.HasIndex(wallet => new { wallet.OwnerId, wallet.Currency })
            .IsUnique();

        builder.HasIndex(wallet => wallet.OwnerId);

        builder.HasMany(wallet => wallet.LedgerEntries)
            .WithOne(entry => entry.Wallet)
            .HasForeignKey(entry => entry.WalletId);
    }
}
