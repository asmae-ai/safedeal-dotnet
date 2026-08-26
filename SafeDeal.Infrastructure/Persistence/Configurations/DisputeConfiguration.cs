using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Infrastructure.Persistence.Configurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Category).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Description).IsRequired().HasMaxLength(2000);
        builder.Property(d => d.Status).HasConversion<string>();
        builder.Property(d => d.EvidenceFiles)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            // Sans ValueComparer, EF compare la collection par référence et rate les ajouts.
            .Metadata.SetValueComparer(new ValueComparer<ICollection<string>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
                v => v.ToList()));

        builder.HasOne(d => d.OpenedBy)
            .WithMany()
            .HasForeignKey(d => d.OpenedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Transaction)
            .WithOne(t => t.Dispute)
            .HasForeignKey<Dispute>(d => d.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // La file d'administration filtre par statut et trie par date.
        builder.HasIndex(d => new { d.Status, d.CreatedAt });
        builder.HasIndex(d => d.CreatedAt);
    }
}