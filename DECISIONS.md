# Decisions

## Architecture
Three projects — **Api** (controllers + Razor Pages), **Core** (entities, the request
state machine, application services), **Infrastructure** (EF Core, migrations,
hashing). Not Clean Architecture/CQRS/MediatR — five entities and ~8 endpoints don't
justify it.

**Rejected:** full ASP.NET Core Identity (two fixed roles, no self-registration/reset —
used only its `PasswordHasher<T>` utility); FluentValidation (DataAnnotations + entity
guard clauses cover every rule here); a repository per entity (`IAppDbContext` is one
seam over EF Core's `DbContext`, already a Unit of Work).

## Tenant isolation — two layers
1. **EF Core global query filters** scope every tenant table to
   `ICurrentUserContext.OrganisationId`, sourced from auth claims, never from request input.
2. **An explicit re-check in `MaintenanceRequestService`** (`EnsureSameTenant`) after
   every fetch, returning 404 (not 403) so a cross-tenant guess doesn't confirm
   existence. Should be unreachable given (1) — it's the backstop for the one place the
   codebase deliberately bypasses filters (login, see Auth).

`ICurrentUserContext` is implemented once, reading claims identical across JWT and
cookie auth, so both front doors get the same guarantee from the same code.

**Verified:** `MaintenanceRequestServiceTenantIsolationTests` against a real (SQLite) EF
Core provider — not mocked — plus live cross-org `curl` calls during manual testing,
returning 404/403 as expected.

## Auth: JWT for the API, cookie for the UI
Both issued from the same `AuthenticationService.ValidateCredentialsAsync` and claim
set, so credentials are checked in one place. Login looks a user up by email with
`.IgnoreQueryFilters()` — the only deliberate use of it in the codebase, since the
caller's org isn't known yet; email is enforced globally unique for this reason.
**Rejected** a single JWT-in-cookie scheme — more moving parts for no benefit here.

## Request lifecycle
`Raised → PendingApproval → Approved → Completed`, `Rejected` reachable from
`PendingApproval`. **Assumption:** `Raised` is never persisted standalone —
`MaintenanceRequest.Raise()` routes to `PendingApproval` or auto-`Approved` in the same
call; a literal persisted `Raised` row would add a write nothing can act on. The state
machine lives entirely on the entity (`Approve/Reject/Complete`), so illegal
transitions are one auditable method, not logic scattered across services.
**Self-approval** is blocked in the entity itself, not just a policy attribute, so it
holds regardless of caller.

## Threshold-based approval
Below threshold → auto-approved, no human approver. At/above → requires an Approver
who isn't the requester. **Assumption — actual cost later exceeds the approved
threshold:** completion is **not** blocked (the threshold is an estimate-time gate, not
a running cap); instead `ExceedsApprovedThreshold` is computed and surfaced (audit
action `CompletedOverThreshold`, UI badge, DTO field). A genuine judgment call I'd
confirm with a product owner in practice.

## Audit trail
Append-only, written in the **same `SaveChangesAsync` call** as the state change it
records — not a separate write that could fail independently. No code path
updates/deletes it. **Audit integrity** beyond that: in production, the app's DB role
would have `UPDATE`/`DELETE` revoked on this table — documented rather than scripted,
since there's no deployment target here. **Rejected** hash-chaining for
tamper-evidence — gold-plating for this scope.

## Spend report
Sums `ActualCost` for `Completed` requests only, by `CompletedAt` — an estimate isn't
spend yet. Approver-only. Aggregation happens client-side after a server-side filter,
not a single `GROUP BY`+`JOIN` — a real SQLite-vs-Postgres provider gap surfaced by the
tests (see AI-LOG.md); fine at this scope's data volumes.

**Frontend scope note:** the brief's minimum is log in/list/create/approve/reject — I
also built a "mark complete" action. Not gold-plating: without it, no request could
ever reach `Completed`, and the spend report (a hard requirement) would have nothing to
show through the UI.

## Database: PostgreSQL
Real constraint/transaction support and proper `timestamptz`/`numeric` types for a
system explicitly "handling client financial data"; Docker makes the 15-minute setup
bar trivial (verified against both a Docker container and a native install). SQLite is
used only as the test double — a deliberate trade-off (test speed over provider
parity), not the production choice.

## Other ambiguities resolved by assumption
- **Who can complete a request:** the original Requester or any Approver — no distinct
  "field worker" role exists in the brief.
- **Visibility:** Requesters see their own requests; Approvers see all requests in
  their org; the spend report is Approver-only.
- **Org threshold:** seeded, not exposed via an endpoint — the brief defines no Admin
  role to gate one behind.
- **Terminal states** (`Completed`/`Rejected`) don't reopen; a corrected request is
  resubmitted as new.

## Explicitly not built
Full ASP.NET Core Identity, FluentValidation, MediatR/CQRS, audit hash-chaining,
DB-per-tenant isolation, an Admin UI for thresholds, and the brief's own out-of-scope
list (deployment, notifications, password reset, background jobs, file uploads,
exhaustive coverage, perf tuning beyond sensible indexing).
