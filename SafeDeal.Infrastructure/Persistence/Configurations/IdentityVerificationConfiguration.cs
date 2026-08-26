using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Infrastructure.Persistence.Configurations;

public class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerification>
{
    public void Configure(EntityTypeBuilder<IdentityVerification> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.DocumentType).IsRequired().HasMaxLength(20);
        builder.Property(v => v.DocumentFrontPath).IsRequired();
        builder.Property(v => v.SelfiePath).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>();
        builder.Property(v => v.RejectionReason).HasMaxLength(500);

        builder.HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // File des dossiers a examiner : filtre sur le statut, tri par anciennete.
        builder.HasIndex(v => new { v.Status, v.CreatedAt });
        builder.HasIndex(v => v.UserId);

        // Le webhook du prestataire retrouve le dossier par cet identifiant.
        builder.HasIndex(v => v.SumsubApplicantId);
    }
}