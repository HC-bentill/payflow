using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedNever();

        builder.Property(entry => entry.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entry => entry.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entry => entry.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(entry => entry.Source)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(PayFlow.Domain.Enums.LedgerEntrySource.Payment)
            .IsRequired();

        builder.Property(entry => entry.CreatedAt)
            .IsRequired();

        builder.HasIndex(entry => new { entry.TenantId, entry.Currency });
        builder.HasIndex(entry => entry.TenantId);
        builder.HasIndex(entry => entry.PaymentId);
        builder.HasIndex(entry => entry.WalletId);

        builder.HasOne(entry => entry.Wallet)
            .WithMany(wallet => wallet.LedgerEntries)
            .HasForeignKey(entry => entry.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
