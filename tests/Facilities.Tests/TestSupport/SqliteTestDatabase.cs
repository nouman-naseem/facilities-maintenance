using Facilities.Core.Abstractions;
using Facilities.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Facilities.Tests.TestSupport;

/// <summary>
/// A shared in-memory SQLite connection backing multiple AppDbContext instances (one
/// per simulated user/tenant), so tests can exercise the real EF global query filters
/// instead of a fake. Deliberately not a full Postgres Testcontainers setup — that
/// would exercise EF against the real production provider too, but at real cost to
/// test speed for what's meant to be "a few tests, not coverage" (see AI-LOG.md /
/// DECISIONS.md); the Postgres-specific SQL (via `dotnet ef database update`) is
/// exercised manually when the app runs.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateContext(new TestCurrentUserContext());
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext(ICurrentUserContext currentUser)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        return new AppDbContext(options, currentUser);
    }

    public void Dispose() => _connection.Dispose();
}
