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

        builder.Property(wallet => wallet.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.HasIndex(wallet => new { wallet.TenantId, wallet.Currency })
            .IsUnique();
    }
}
