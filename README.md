# Facilities Maintenance

Backend (+ minimal server-rendered UI) for tracking maintenance requests, threshold-based
approval, and per-site spend across client organisations. See [DECISIONS.md](DECISIONS.md)
for the reasoning behind the architecture and [AI-LOG.md](AI-LOG.md) for how this was built.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL)

## Running it (should take under 5 minutes)

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Local config — not committed (see "Secrets" below)
cp src/Facilities.Api/appsettings.Development.json.example src/Facilities.Api/appsettings.Development.json

# 3. Run the app — migrations apply and demo data seeds automatically on startup
dotnet run --project src/Facilities.Api
```

Then open **http://localhost:5129** (or `https://localhost:7034` — the first HTTPS run
will prompt to trust the ASP.NET Core dev certificate; if you'd rather skip that, use
the http URL instead).

- The root URL is the minimal Razor Pages UI: log in, raise a request, approve/reject,
  view the spend report.
- The JSON API lives under `/api` (`/api/auth/login`, `/api/requests`, `/api/reports/spend`, …).
- `/openapi/v1.json` (Development only) has the OpenAPI document if you want to explore
  the API directly.

### Demo accounts

Seeded automatically on first run (password `Password123!` for all):

| Organisation | Email | Role |
|---|---|---|
| Acme Facilities Ltd (threshold £500) | `requester@acme.test` | Requester |
| Acme Facilities Ltd | `approver@acme.test` | Approver |
| Northwind Property Group (threshold £1000) | `requester@northwind.test` | Requester |
| Northwind Property Group | `approver@northwind.test` | Approver |

Log in as both organisations' users side by side (e.g. two browser profiles) to see
tenant isolation directly — Acme's users can never see Northwind's requests or sites.

## Running the tests

```bash
dotnet test
```

> **Known environment caveat:** on a machine with Windows *Smart App Control* enabled,
> the test host can be blocked from loading freshly built, unsigned DLLs via reflection
> (Code Integrity policy) — this affects test discovery specifically, not the app itself
> (`dotnet run` is unaffected). This was hit during development; see AI-LOG.md. If you
> hit it, `dotnet test` will fail with `FileLoadException: ... Application Control policy
> ...` rather than a test failure — that's this, not a bug in the code.

## Secrets

`src/Facilities.Api/appsettings.Development.json` is git-ignored. The `.example` copy
it's cloned from contains **local-only, non-sensitive placeholder values** (a dev JWT
signing key, a Postgres password matching `docker-compose.yml`) — safe to have visible
in a repo because they only ever apply to a throwaway local database. In production,
the connection string and JWT signing key would come from environment variables or a
secrets manager (Azure Key Vault, AWS Secrets Manager, etc.), never from a checked-in
file — see DECISIONS.md.

## Project layout

```
src/
  Facilities.Api             ASP.NET Core Web API (controllers under /api) + Razor Pages UI
  Facilities.Core            Domain entities, request-lifecycle state machine, application services
  Facilities.Infrastructure  EF Core DbContext (tenant query filters), migrations, password hashing
tests/
  Facilities.Tests           Domain unit tests + service-level tenant-isolation/report tests
```

## Database migrations

```bash
dotnet tool install --global dotnet-ef   # once
dotnet ef migrations add <Name> --project src/Facilities.Infrastructure --startup-project src/Facilities.Infrastructure -o Persistence/Migrations
```

Migrations apply automatically on app startup (see `Program.cs`) — there's no separate
manual `database update` step for local dev.
