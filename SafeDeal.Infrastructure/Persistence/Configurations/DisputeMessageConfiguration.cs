using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Infrastructure.Persistence.Configurations;

public class DisputeMessageConfiguration : IEntityTypeConfiguration<DisputeMessage>
{
    public void Configure(EntityTypeBuilder<DisputeMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Body).IsRequired().HasMaxLength(2000);

        builder.Property(m => m.Files)
            .HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList())
            // Sans ValueComparer, EF ne détecte pas les mutations de la collection.
            .Metadata.SetValueComparer(new ValueComparer<ICollection<string>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
                v => v.ToList()));

        builder.HasOne(m => m.Dispute)
            .WithMany(d => d.Messages)
            .HasForeignKey(m => m.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Author)
            .WithMany()
            .HasForeignKey(m => m.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.DisputeId);
    }
}
