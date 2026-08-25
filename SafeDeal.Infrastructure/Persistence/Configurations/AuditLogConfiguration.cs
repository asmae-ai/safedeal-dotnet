using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeDeal.Domain.Entities;

namespace SafeDeal.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(60).IsRequired();
        builder.Property(a => a.Subject).HasMaxLength(255);
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.FailureReason).HasMaxLength(500);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(512);
        builder.Property(a => a.Metadata).HasMaxLength(2000);

        // Aucune relation vers User : le journal doit survivre a la suppression
        // d'un compte, sinon la trace disparait avec ce qu'elle documente.
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => new { a.Action, a.CreatedAt });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
