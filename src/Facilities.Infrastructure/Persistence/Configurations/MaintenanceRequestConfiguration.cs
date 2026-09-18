using Facilities.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facilities.Infrastructure.Persistence.Configurations;

public class MaintenanceRequestConfiguration : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequest> builder)
    {
        builder.ToTable("MaintenanceRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Description).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.EstimatedCost).HasPrecision(12, 2);
        builder.Property(r => r.ActualCost).HasPrecision(12, 2);
        builder.Property(r => r.ThresholdAtApproval).HasPrecision(12, 2);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Organisation>().WithMany().HasForeignKey(r => r.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Site>().WithMany().HasForeignKey(r => r.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Approval queue lookups and the tenant filter.
        builder.HasIndex(r => new { r.OrganisationId, r.Status });
        // Spend report: per-site totals over a date range, scoped to the org.
        builder.HasIndex(r => new { r.OrganisationId, r.SiteId, r.Status, r.CompletedAt });
    }
}
