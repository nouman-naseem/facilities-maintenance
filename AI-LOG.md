# AI Log

Built with Claude Code (Sonnet 5), driven directly in an agentic session (no
sub-agent fan-out) — small and security-sensitive enough (tenant isolation, audit
trail) that one continuous train of reasoning beat parallel workers needing the same
context re-explained.

## What was delegated fully
Boilerplate: `dotnet new` scaffolding, package references, EF Core migration
generation, DTO records, Razor Pages markup, `docker-compose.yml`. Low-risk, easy to
verify by reading.

## What was delegated with tight constraints
Before any code, I gave the brief with an explicit two-phase instruction — design
first, implementation only after sign-off. The actual prompt (trimmed):

> ...Do NOT immediately start writing the whole application. First, analyze the
> requirements and produce: 1. A concise requirements breakdown 2. Functional
> requirements 3. Security requirements 4. Domain model/entities 5. Request
> lifecycle/state transition rules 6. Tenant-isolation strategy 7. Authentication and
> authorization strategy 8. Approval-threshold design 9. Audit-trail design 10.
> Spend-report design 11. Database/schema design 12. Recommended project structure 13.
> Recommended libraries/packages 14. Testing strategy 15. Ambiguities... 16. What
> should deliberately remain out of scope... Do not implement anything yet... Wait for
> my approval before implementation.

This forced every non-obvious call — threshold/actual-cost handling, the Raised-state
design, JWT+cookie split, PostgreSQL choice — onto the table as text to contest
*before* implementation existed. Only once that held up did "please proceed to
implementation" turn into genuine full delegation for everything downstream.

## What I did by hand
Reviewed and approved the design before implementation. The dev sandbox was unusually
hostile to local .NET work — Smart App Control blocking the test host, no WSL2 (Docker
couldn't start), and the .NET SDK itself vanishing mid-session (likely active endpoint
management). Each time, the agent stopped and asked rather than working around a
security control — it explicitly declined to weaken Smart App Control, correctly
noting it has no per-folder exclusion, only a global off-switch. I made the calls:
native PostgreSQL instead of chasing Docker/WSL, reinstall the SDK when it vanished. I
also gave the Postgres password directly in chat to unblock setup — in hindsight the
agent's initial instinct to avoid that channel entirely was the better one.

## Plausible but wrong
**Broke a real endpoint, not just a test:** `LoginRequest` etc. used
`[property: Required, EmailAddress] string Email` on record types — compiles clean,
looks like plenty of real ASP.NET Core code. Only fails at *runtime*, on the first live
request: ASP.NET Core requires validation attributes directly on a record's
constructor parameter, not via `[property: ...]`. No test caught it — none exercise the
real model-binding pipeline. **Caught by:** manually curling the login endpoint against
a real Postgres instance, not any automated check.

**Caught by the test suite:** a spend-report test simulated another org's user with
`UserId = Guid.NewGuid()`, never persisted, then used it to create a
`MaintenanceRequest` — a real FK to `Users`. Failed with a raw FK constraint error, not
an assertion. Easy to miss because a sibling test uses the identical idiom *correctly*
(there, the ID is only read, never written).

**Found live, after "done":** the `/Reports` page 500'd on "Run". Root cause: a
date-picker value bound as `DateTime.Kind=Unspecified`, then implicitly converted to
`DateTimeOffset` — which for `Unspecified` assumes **server-local time**, silently
shifting the picked date by the server's UTC offset; Npgsql then separately rejected
the resulting non-zero offset outright. Two stacked bugs, neither visible to the
SQLite-backed test suite (SQLite doesn't enforce Npgsql's offset rule). **Caught by:**
a person clicking the button. Fixed by constructing `DateTimeOffset` with an explicit
zero offset at the UI boundary, and normalizing via `.ToUniversalTime()` inside
`SpendReportService` so the API path is protected the same way.

All three say the same thing: a green build and a passing test suite are not the claim
"the feature works" — each gap was in exactly the part neither exercises (real HTTP
model binding, a real database driver's real constraints).

## Environment friction (resolved)
`dotnet test` failed with an Application Control block. Checked the Windows Code
Integrity event log directly rather than guessing — Smart App Control was blocking
`testhost.exe` from loading unsigned output during test *discovery* only (`dotnet run`
loaded the same DLL fine). Migrated to `xunit.v3` + Microsoft.Testing.Platform (worth
doing anyway, and sidesteps the separate `testhost.exe` process), then added a
`global.json` opting into .NET 10's new `dotnet test` MTP mode. All 14 tests now pass
via plain `dotnet test`.
