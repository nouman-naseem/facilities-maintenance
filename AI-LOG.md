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
gate, not a formality — I could have sent it back). The dev sandbox turned out to be
unusually hostile to local .NET work — Smart App Control blocking the test host, no
WSL2 (so Docker/Postgres couldn't start), and the .NET SDK itself getting removed
mid-session by what looks like active endpoint management. At each point the agent
stopped and asked rather than quietly working around a security control (e.g. it
explicitly refused to weaken Smart App Control itself, correctly pointing out it has no
per-folder exclusion, only a global off-switch). I made the calls: install PostgreSQL
natively instead of chasing Docker/WSL, and reinstall the SDK when it vanished. I
provided the Postgres superuser password directly in chat to unblock setup — in
hindsight the agent was right to initially decline to ask for it and offer a
password-free path instead; a real handoff should avoid that channel entirely.

## Plausible but wrong

**The one that actually broke a real endpoint, not just a test:** `LoginRequest`,
`CreateRequestDto`, etc. were written as records with validation attributes like
`[property: Required, EmailAddress] string Email`. This compiles cleanly, looks correct,
and is a pattern that shows up in plenty of real ASP.NET Core codebases and tutorials.
It only failed at *runtime*, on the very first live login attempt during manual
verification: `InvalidOperationException: Record type 'LoginRequest' has validation
metadata defined on property 'Password' that will be ignored... validation metadata
must be associated with the constructor parameter.` ASP.NET Core's model validation
requires the attribute on the record's primary-constructor parameter directly, not via
`[property: ...]`, and will only tell you at request time, not at compile time or via
`dotnet build`. No unit or service-level test caught this, because none of them go
through ASP.NET Core's model-binding/validation pipeline — that pipeline only runs for
real HTTP requests. **Caught by:** manually exercising the login endpoint with `curl`
after standing up a real Postgres instance, not by any automated check. Fixed by
dropping `property:` so the attributes target the constructor parameter
(`src/Facilities.Api/Contracts/AuthContracts.cs`, `RequestContracts.cs`). This is the
strongest argument in this whole exercise for actually running the app rather than
trusting a green build.

**A smaller one, caught by the test suite itself:** in
`SpendReportServiceTests`, the agent simulated a "user from a different organisation"
with `UserId = Guid.NewGuid()` — never persisted — then used that context to call
`MaintenanceRequestService.CreateAsync`, which writes a `MaintenanceRequest` row with
that ID as a real foreign key to `Users`. Failed at `SaveChangesAsync` with a generic
`FOREIGN KEY constraint failed`, not an assertion. Easy to miss because the sibling
isolation-test file uses the identical `Guid.NewGuid()` idiom correctly — there, the
fake ID is only ever read for a filter comparison, never persisted. Fixed by seeding a
real `User` row for the other organisation first.

## Environment friction (resolved, not swept under the rug)

`dotnet test` initially failed with `FileLoadException: ... An Application Control
policy has blocked this file`. Checked the Windows Code Integrity event log directly
rather than guessing — confirmed Smart App Control was blocking `testhost.exe` from
loading unsigned build output via reflection, and specifically only during test
*discovery* (`dotnet run` loaded the identical DLL fine). Migrated the test project from
`xunit` + VSTest to `xunit.v3` + Microsoft.Testing.Platform, which was going to be worth
doing anyway (it's the modern, actively-developed path) and incidentally avoids the
separate `testhost.exe` process. Later, .NET 10's `dotnet test` needed one more nudge —
a `global.json` with `"test": {"runner": "Microsoft.Testing.Platform"}` — since .NET 10
dropped implicit VSTest-mode support for MTP projects. All 14 tests now pass via plain
`dotnet test`.
