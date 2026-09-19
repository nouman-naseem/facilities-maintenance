# CLAUDE.md

Guidance for an AI agent working in this repository.

## What this is

A facilities-maintenance backend (ASP.NET Core / .NET 10, EF Core, PostgreSQL) for a
technical evaluation. Scope is deliberately small — see DECISIONS.md for what was left
out on purpose. Don't add features, abstractions, or "production hardening" beyond what
DECISIONS.md and the brief call for; if you think something's missing, propose it and
let a human decide rather than adding it speculatively.

## Layout

```
src/Facilities.Api             Controllers (/api/*, JWT), Razor Pages (/, cookie auth), Program.cs wiring
src/Facilities.Core            Entities, the request state machine, application services — no EF/HTTP types beyond IAppDbContext's DbSet<T>
src/Facilities.Infrastructure  AppDbContext, EF configurations, migrations, password hashing
tests/Facilities.Tests         xUnit v3
```

Dependency direction: `Api → Core, Infrastructure`; `Infrastructure → Core`; `Core`
depends on nothing else in this solution. Don't introduce a reference that violates
this (e.g. `Core` referencing `Infrastructure` or `Api`).

## Conventions to follow

- **Business rules live on the domain entity or in a Core service, never in a
  controller/PageModel.** `MaintenanceRequest.Approve/Reject/Complete` enforce the
  state machine; controllers and Razor PageModels only translate HTTP/form input into
  service calls and map results back.
- **A read with no business rule attached** (e.g. listing sites) can query
  `IAppDbContext` directly from a controller — don't wrap trivial CRUD reads in a
  service purely for symmetry.
- **Tenant scoping is two-layered by design** — the EF global query filter
  (`AppDbContext.OnModelCreating`) plus an explicit re-check in
  `MaintenanceRequestService` (`EnsureSameTenant`). Keep both. If you add a new
  tenant-scoped entity or service method, add both layers for it too, and prefer 404
  over 403 for cross-tenant access so a guessed ID doesn't confirm existence.
- **Never trust a client-supplied organisation ID.** `ICurrentUserContext` (sourced
  from auth claims) is the only source of truth for "which org is this."
- **`.IgnoreQueryFilters()` is a red flag anywhere except `AuthenticationService`**
  (login has to look a user up before knowing their org). If you find yourself adding
  it elsewhere, that's very likely a tenant-isolation bug, not a feature.
- **No FluentValidation, no MediatR/CQRS, no full ASP.NET Core Identity** — see
  DECISIONS.md for why. Don't reintroduce them without a real justification the
  existing choice doesn't cover.
- Prefer editing existing files over creating new ones; don't create new top-level
  folders/projects without a reason tied to the dependency rules above.

## Testing

`dotnet test` runs the suite (xUnit v3 on Microsoft.Testing.Platform, opted in via
`global.json` — see AI-LOG.md for why not the classic VSTest pipeline). Tests use a
shared in-memory SQLite connection
(`SqliteTestDatabase`) so multiple `AppDbContext` instances — one per simulated
user/tenant — see the same schema; this exercises the real EF global query filters
rather than a mock of them. When adding a tenant-scoped entity/query, add a test that
constructs it as one tenant and asserts another tenant's context can't see or mutate it,
following the pattern in `MaintenanceRequestServiceTenantIsolationTests`.

Known SQLite-vs-Postgres gaps already worked around (don't "fix" these back to the
naive form): `DateTimeOffset` columns get a `DateTimeOffsetToBinaryConverter` only under
the SQLite provider (`AppDbContext.OnModelCreating`); `SpendReportService` aggregates
client-side after a server-side filter rather than a single `GroupBy`+`Join`, because
the SQLite provider can't translate that shape the way Npgsql can.

## Commits

Commit as you go, one logical layer/feature per commit (see the existing history for
granularity) — not one giant commit at the end. Don't squash.

## Local run

`docker compose up -d` (Postgres) then `dotnet run --project src/Facilities.Api` —
migrations and demo-data seeding happen automatically on startup. See README.md.
