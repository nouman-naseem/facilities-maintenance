# Decisions

## Architecture

Three projects — **Api** (controllers + Razor Pages), **Core** (domain entities, the
request state machine, application services), **Infrastructure** (EF Core, migrations,
password hashing) — not full Clean Architecture with a domain-event bus, not
CQRS/MediatR. Five entities and ~8 endpoints don't justify that ceremony; a flat,
three-project split is something I can fully explain in the review, which a heavier
pattern chosen "because it's standard" would not be.

**Rejected:** ASP.NET Core Identity (full system) — this app has two fixed roles, no
self-registration, no password reset, so the role/claim/token store Identity brings is
unused weight. Used `Microsoft.AspNetCore.Identity`'s `PasswordHasher<T>` utility class
only, for its PBKDF2 implementation, and skipped everything else. **Rejected** FluentValidation
in favour of DataAnnotations + entity guard clauses — no rule complex enough to earn a
new dependency. **Rejected** a repository interface per entity — `IAppDbContext` is one
seam over EF Core's DbContext (already a Unit of Work), used only so Core doesn't take a
package dependency on Npgsql and so tests can swap in SQLite.

## Tenant isolation — two independent layers

1. **EF Core global query filters** (`AppDbContext.OnModelCreating`) scope every
   tenant-owned table (`Site`, `User`, `MaintenanceRequest`, `AuditLogEntry`) to
   `ICurrentUserContext.OrganisationId`, sourced from the auth ticket's claims — never
   from a route/body value. A cross-tenant ID in a URL simply doesn't match any row.
2. **An explicit re-check in `MaintenanceRequestService`** (`EnsureSameTenant`) after
   every fetch, returning 404 (not 403) on a mismatch, so a guessed ID from another
   tenant doesn't even confirm existence. This should be unreachable given (1) — it's
   there for the day a query legitimately needs `.IgnoreQueryFilters()` (see auth,
   below) and someone adds a similar call elsewhere without thinking about tenancy.

`ICurrentUserContext` is implemented once in Api (`HttpContextCurrentUserContext`),
reading claims that are identical whether the request came in via JWT or the cookie
(see Auth) — so both front doors get the same isolation guarantee from the same code.

**Verification:** `MaintenanceRequestServiceTenantIsolationTests` creates two
organisations against a real (SQLite) EF Core provider and asserts a user from one
cannot fetch, approve, or list the other's requests. Deliberately not mocked — the
global query filter is EF Core configuration, and a mock would just assume it works.

## Auth: JWT for the API, cookie for the UI, one login path

Both schemes are registered; `/api/*` controllers require the JWT bearer scheme, Razor
Pages default to the cookie scheme. Both are issued from the same
`AuthenticationService.ValidateCredentialsAsync` and the same claim set
(`ClaimsFactory`), so there's exactly one place credentials are checked. Login looks a
user up by email with `.IgnoreQueryFilters()` — the only deliberate, commented use of it
in the codebase — because at that point we don't yet know which organisation the caller
belongs to; email is enforced globally unique for this reason.

**Rejected:** a single scheme for both (e.g. JWT-in-cookie) — would have worked, but
mixing "the UI is JWT stored in a cookie" is more moving parts than "the UI uses cookie
auth, the API uses bearer auth," for no benefit at this scope.

## Request lifecycle

`Raised → PendingApproval → Approved → Completed`, with `Rejected` reachable from
`PendingApproval`. **Assumption:** `Raised` is not a state that's ever persisted on its
own — `MaintenanceRequest.Raise()` immediately routes to `PendingApproval` or
`Approved` (auto) in the same call, based on the threshold. I considered persisting a
literal `Raised` row first, but that would mean either a second write immediately after
the first (no real-world value — nothing can act on a request in that split second) or
an artificial delay. The state machine lives entirely on the entity
(`MaintenanceRequest.Approve/Reject/Complete`), so an illegal transition is a single,
easy-to-audit method, not something scattered across services.

**Self-approval** (an approver can't approve their own request) is enforced in the
entity itself, not just at the controller/policy level — so it holds regardless of
which front door or future caller invokes it.

## Threshold-based approval

Estimated cost `< organisation threshold` → auto-approved, no human approver
(`ApprovedByUserId` stays null, `ThresholdAtApproval` is still snapshotted so the audit
trail is consistent either way). At or above → requires an Approver who isn't the
requester.

**Assumption — actual cost later exceeds the approved threshold:** completion is
**not** blocked. The threshold is treated as an estimate-time gate, not a running spend
cap; blocking a Requester from closing out already-completed physical work over a
back-office figure seemed like the wrong trade-off, and re-approval-after-the-fact
wasn't asked for. Instead, `MaintenanceRequest.ExceedsApprovedThreshold` is computed and
surfaced: the audit entry for that completion is tagged `CompletedOverThreshold`, and
the flag is visible in the UI badge and the `MaintenanceRequestDto`. This is a genuine
judgment call I'd confirm with a product owner in a real engagement.

## Audit trail

Append-only `AuditLogEntry`, written in the **same `SaveChangesAsync` call** as the
state change it records (same DbContext instance, same transaction — not a separate
write that could fail independently). No code path calls `Update`/`Remove` on it.
**Audit integrity** beyond "the app doesn't expose a way to edit it": in production,
the app's runtime DB role would have `UPDATE`/`DELETE` revoked on this table at the
database level, so even a fully compromised app credential couldn't rewrite history —
documented here rather than scripted into the migration, since there's no production
deployment target in this exercise to apply it to. **Rejected:** hash-chaining audit
rows for tamper-evidence — gold-plating for what was asked; noted as a natural next step.

## Spend report

Sums `ActualCost` for `Completed` requests only, grouped by site, filtered by
`CompletedAt` — an estimate isn't spend yet. Restricted to the `Approver` role (financial
data). The final grouping/aggregation happens client-side after a server-side filtered
fetch, not as a single SQL `GROUP BY`+`JOIN` — see AI-LOG.md for why (a real
SQLite-vs-Postgres provider gap surfaced by the test suite); at the per-org,
per-date-range volumes this app is scoped for, that's a non-issue, and revisiting it
if/when it isn't would be a one-method change.

## Database: PostgreSQL

Real constraint/transaction support and proper `timestamptz`/`numeric` types for a
system that's explicitly "handling client financial data," and Docker makes the
15-minute setup bar trivial. SQLite is used **only** as the test double (see
`SqliteTestDatabase`) — deliberately not the same provider as production, which is a
trade-off (a provider-specific SQL bug in Postgres wouldn't be caught by these tests)
accepted for test speed and zero extra local infrastructure.

## Other ambiguities resolved by assumption

- **Who can complete a request:** either the original Requester or any Approver in the
  org — the brief doesn't define a "field worker" role distinct from these two.
- **Visibility:** Requesters see only their own requests; Approvers see all requests in
  their org (they need the full picture to approve). The spend report is Approver-only.
- **Org threshold configuration:** seeded, not exposed via an endpoint — the brief
  defines no Admin role to gate such an endpoint behind, and adding one wasn't asked for.
- **Terminal states:** `Completed`/`Rejected` don't reopen; a corrected request is
  resubmitted as new.

## Explicitly not built (and not gold-plated)

Full ASP.NET Core Identity, FluentValidation, MediatR/CQRS, audit hash-chaining,
schema/DB-per-tenant isolation, an Admin UI for threshold management, and anything from
the brief's own out-of-scope list (deployment, notifications, password reset, background
jobs, file uploads, exhaustive test coverage, performance tuning beyond sensible
indexing).
