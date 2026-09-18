using Facilities.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Facilities.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations add` build the model without spinning up the full app/DI.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=facilities;Username=facilities;Password=design-time-only");
        return new AppDbContext(optionsBuilder.Options, new AnonymousCurrentUserContext());
    }
}
