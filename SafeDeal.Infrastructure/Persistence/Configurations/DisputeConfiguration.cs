using Microsoft.EntityFrameworkCore;
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
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}