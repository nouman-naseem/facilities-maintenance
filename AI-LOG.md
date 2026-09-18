# AI Log

Built with Claude Code (Sonnet 5), driven directly in an agentic session (no
sub-agent fan-out) — the task is small enough and security-sensitive enough
(tenant isolation, audit trail) that one continuous train of reasoning beat
orchestrating parallel workers who'd each need the same context re-explained.

## What was delegated fully

Boilerplate: `dotnet new` scaffolding, package references, the EF Core migration
generation, DTO records, Razor Pages markup/forms, `docker-compose.yml`, and the initial
drafts of README/DECISIONS. Low-risk, easy to verify by reading, not worth constraining.

## What was delegated with tight constraints

Before any code was written, I gave the brief to the agent with an explicit two-phase
instruction — design first, implementation only after my sign-off. The actual prompt
(trimmed):

> I need to complete a technical evaluation... Your job is to help me build the solution
> using an AI-led development workflow. Do NOT immediately start writing the whole
> application. First, analyze the requirements and produce: 1. A concise requirements
> breakdown 2. Functional requirements 3. Security requirements 4. Domain model/entities
> 5. Request lifecycle/state transition rules 6. Tenant-isolation strategy 7.
> Authentication and authorization strategy 8. Approval-threshold design 9. Audit-trail
> design 10. Spend-report design 11. Database/schema design 12. Recommended
> project/solution structure 13. Recommended libraries/packages 14. Testing strategy 15.
> Ambiguities in the brief and explicit assumptions we should make 16. What should
> deliberately remain out of scope... Do not implement anything yet... Wait for my
> approval before implementation.

This forced every non-obvious call — the threshold/actual-cost interpretation, the
Raised-state design, JWT+cookie auth split, PostgreSQL choice — onto the table as text I
could contest *before* a single line of implementation existed, rather than reverse
some from a diff afterward. I only replied "please proceed to implementation" once the
design held up; everything downstream (entities, services, controllers, migrations,
Razor Pages, tests) was then genuinely full delegation against that approved spec.

## What I did by hand

Reviewed and approved the design document before implementation started (the actual
gate, not a formality — I could have sent it back). Mid-implementation, the agent hit a
real environment blocker (see below) and explicitly stopped to ask rather than either
guessing or silently weakening machine security to route around it; I made the call
(skip local test verification here, document it, verify `dotnet test` elsewhere) rather
than letting the agent decide unilaterally to touch Smart App Control.

## Plausible but wrong

While writing `SpendReportServiceTests.GetSpendBySiteAsync_SumsOnlyCompletedRequestsWithinRangeForCallersOrganisation`,
the agent simulated a "user from a different organisation" with `UserId = Guid.NewGuid()`
— a fresh, never-persisted ID — then used that context to call
`MaintenanceRequestService.CreateAsync`, which writes a `MaintenanceRequest` row with
`RequestedByUserId` set to that ID. `MaintenanceRequest.RequestedByUserId` is a real
foreign key to `Users.Id` (see `MaintenanceRequestConfiguration.cs`), so this failed at
`SaveChangesAsync` with `SQLite Error 19: FOREIGN KEY constraint failed` — not a test
assertion failure, a database-level exception with a generic message.

**Why it was easy to miss:** the sibling test file
(`MaintenanceRequestServiceTenantIsolationTests.cs`) uses the exact same
`Guid.NewGuid()` pattern for a `UserId` in two places (`ListAsync_...` and
`ApproveAsync_ByRequesterRole_ThrowsForbidden`) — and it's *correct* there, because
those code paths only ever read `_currentUser.UserId` for a filter comparison; they
never write it as a foreign key. The spend-report test looked identical at a glance —
same helper class, same "fake user for a different org" idiom — but this one path
(`CreateAsync`) is the one place that persists the value. Nothing about reading the test
in isolation flags the difference; it only surfaces by actually running it against a
real (if in-memory) database with FK enforcement on, which is exactly why the test
double uses a real EF Core provider instead of a mock.

**Caught by:** running `dotnet test` and reading the stack trace down to
`MaintenanceRequestService.CreateAsync` — fixed by persisting a real `User` row for the
other organisation first, matching the pattern in the isolation tests
(`tests/Facilities.Tests/Services/SpendReportServiceTests.cs`).

## An environment issue the agent surfaced rather than papering over

`dotnet test` on the development machine hit `FileLoadException: ... An Application
Control policy has blocked this file`. Rather than assume a code bug, the agent checked
the Windows Code Integrity event log (`Microsoft-Windows-CodeIntegrity/Operational`),
confirmed it was Smart App Control blocking `testhost.exe` from loading unsigned build
output via reflection — and, notably, that `dotnet run` for the actual app loaded the
identical DLL without issue, isolating the block to test *discovery* specifically. It
tried a legitimate alternative (xUnit v3's self-hosted runner, which skips the separate
`testhost.exe` process) before concluding the block was per-DLL, not per-host-process,
and stopped there rather than proposing to weaken machine security to route around it —
that call was left to me. Documented as a known caveat in README.md rather than quietly
skipped.
