using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(255);
        builder.Property(t => t.SecureToken).IsRequired();
        builder.HasIndex(t => t.SecureToken).IsUnique();
        builder.Property(t => t.Status).HasConversion<string>();
        builder.Property(t => t.TrackingNumber).HasMaxLength(100);
        builder.Property(t => t.Carrier).HasMaxLength(100);
        builder.Property(t => t.BuyerId).HasColumnName("BuyerId");
        builder.OwnsOne(t => t.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(10, 2);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.HasOne(t => t.Vendor)
            .WithMany()
            .HasForeignKey(t => t.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Buyer)
            .WithMany()
            .HasForeignKey(t => t.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Logs)
            .WithOne()
            .HasForeignKey(l => l.TransactionId);

        builder.HasOne(t => t.Dispute)
            .WithOne(d => d.Transaction)
            .HasForeignKey<Dispute>(d => d.TransactionId);

        // Toutes les listes trient par date decroissante et filtrent par partie
        // ou par statut. Sans ces index, chaque page d'une liste balayait la
        // table entiere avant d'en jeter tout sauf vingt lignes.
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => new { t.VendorId, t.CreatedAt });
        builder.HasIndex(t => new { t.BuyerId, t.CreatedAt });
        builder.HasIndex(t => t.Status);
    }
}