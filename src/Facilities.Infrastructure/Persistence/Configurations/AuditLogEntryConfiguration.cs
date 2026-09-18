using Facilities.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facilities.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Comment).HasMaxLength(2000);
        builder.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(a => new { a.OrganisationId, a.EntityType, a.EntityId });
    }
}
