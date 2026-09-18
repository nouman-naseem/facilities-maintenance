using Facilities.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Facilities.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Organisation>().WithMany().HasForeignKey(u => u.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.OrganisationId);

        // Login identifies a user by email alone (no org context yet), so email must
        // be unique across the whole system, not just within an organisation.
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
