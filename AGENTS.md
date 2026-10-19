# AGENTS.md — E2E Calendar

## Project
Windows 10/11 calendar + reminders + tasks app. Offline-first, fast (<1s cold start), reminders must never be missed.
Spec source of truth: docs/*.md. Read docs/02-ARCHITECTURE.md before writing code.

## Stack
- .NET 8, C# 12, WinUI 3 (Windows App SDK 1.5+), MVVM via CommunityToolkit.Mvvm
- DI: Microsoft.Extensions.DependencyInjection / Hosting
- DB: SQLite via Microsoft.Data.Sqlite + Dapper (SQLCipher for encryption). No heavy ORM on hot paths.
- Recurrence/iCal: Ical.Net (RFC 5545)
- Tests: xUnit + FluentAssertions
- Packaging: MSIX, publisher "E2E Dev"

## Solution layout (do not change without asking)
src/E2E.Calendar.Core        -> domain models, interfaces, recurrence, NLP parser, scheduler logic (NO Windows/UI refs)
src/E2E.Calendar.Data        -> SQLite repos, migrations
src/E2E.Calendar.Sync        -> Google, Graph, CalDAV, ICS providers
src/E2E.Calendar.Platform    -> Windows-only: toasts, tray, hotkey, credential manager, startup task
src/E2E.Calendar.App         -> WinUI 3 views, viewmodels, resources
tests/*                      -> unit/integration tests

## Rules
1. Core must stay pure & testable: no `Windows.*`, no WinUI types.
2. All times stored as UTC (`*_utc` columns, ISO-8601) + IANA tz id string. Never store local time only.
3. Every feature = interface in Core, impl in Data/Sync/Platform, VM in App. Register in DI.
4. No blocking on UI thread. All IO is async with CancellationToken.
5. Never store secrets/tokens in files or DB. Use Windows Credential Manager (Platform).
6. Never commit secrets, client IDs with secrets, .pfx, .db files.
7. Each task: write/adjust unit tests, run `dotnet build` and `dotnet test` before saying done.
8. Small commits, Conventional Commits (feat:, fix:, chore:, test:, docs:).
9. UI follows docs/03-UI-UX.md: Fluent, Mica, theme resources only (no hard-coded colors), keyboard accessible, AutomationProperties.Name on interactive controls.
10. Don't add NuGet packages not listed in docs/02-ARCHITECTURE.md without asking.
11. If a requirement is unclear, ask one short question, otherwise proceed with the doc.

## Commands
- Build: `dotnet build E2E.Calendar.sln -c Debug -p:Platform=x64`
- Test: `dotnet test tests/E2E.Calendar.Core.Tests`
- Run app: open in Visual Studio F5 (WinUI 3 needs VS/MSBuild for XAML); CLI: `dotnet build` then launch from bin.

## Definition of Done (per task)
Builds with 0 errors, tests pass, no TODO left silently, docs updated if behavior changed, committed.
