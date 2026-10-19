# Architecture (A to Z)

## 1. Big picture
```
 ┌────────────────────────── E2E.Calendar.App (WinUI 3, MVVM) ──────────────────────────┐
 │ Views(XAML) ─ ViewModels ─ Navigation ─ Theming ─ Command Palette                    │
 └───────────────▲───────────────────────────────────────────────▲──────────────────────┘
                 │ interfaces (DI)                               │
 ┌───────────────┴───────── E2E.Calendar.Core (pure C#) ─────────┴──────────────────────┐
 │ Models · IEventService · IReminderEngine · IRecurrenceExpander · IQuickAddParser     │
 │ ISyncEngine · IClock · IRepository<T>                                                │
 └──▲──────────────▲───────────────────▲───────────────────────▲───────────────────────┘
    │              │                   │                       │
  Data           Sync               Platform                 Tests
 SQLite/Dapper  Google/Graph/     Toast, Tray, Hotkey,     xUnit
 migrations     CalDAV/ICS        CredMgr, StartupTask
```
Rule: dependencies point inward to Core. Core never references Windows or UI.

## 2. Folder structure
```
E2E.Calendar.sln
src/
  E2E.Calendar.Core/
    Models/ (CalendarEvent, Calendar, Reminder, TaskItem, Account, Alert, Occurrence)
    Recurrence/ RecurrenceExpander.cs
    Reminders/  ReminderEngine.cs, ReminderPlanner.cs, SnoozeService.cs
    QuickAdd/   QuickAddParser.cs, Tokenizer.cs, DateTimeResolver.cs
    Sync/       ISyncProvider.cs, SyncEngine.cs, ConflictResolver.cs, OutboxItem.cs
    Services/   EventService.cs, TaskService.cs, SearchService.cs, FreeSlotService.cs
    Abstractions/ IClock.cs, INotifier.cs, ISecretStore.cs
  E2E.Calendar.Data/
    Db.cs, Migrations/001_init.sql, Repos/*.cs
  E2E.Calendar.Sync/
    Google/GoogleCalendarProvider.cs, Microsoft/GraphCalendarProvider.cs, CalDav/, Ics/IcsService.cs
  E2E.Calendar.Platform/
    ToastNotifier.cs, TrayService.cs, HotkeyService.cs, CredentialStore.cs, StartupService.cs, PowerEvents.cs
  E2E.Calendar.App/
    App.xaml(.cs), MainWindow, Views/ (Month, Week, Day, Agenda, Reminders, Tasks, Settings, QuickAdd, EventEditor)
    ViewModels/, Controls/ (TimeGrid, EventChip, MiniCalendar), Themes/, Strings/en-US/Resources.resw
tests/ E2E.Calendar.Core.Tests, E2E.Calendar.Data.Tests
docs/ prompts/ AGENTS.md opencode.json
```
NuGet allowed: CommunityToolkit.Mvvm, Microsoft.WindowsAppSDK, Microsoft.Extensions.*, Microsoft.Data.Sqlite (+ SQLitePCLRaw.bundle_e_sqlcipher), Dapper, Ical.Net, Google.Apis.Calendar.v3, Microsoft.Graph, Microsoft.Identity.Client, Serilog, xUnit, FluentAssertions, NSubstitute.

## 3. Data model (SQLite)
```sql
CREATE TABLE account(id TEXT PK, provider TEXT, display_name TEXT, email TEXT, sync_token TEXT, last_sync_utc TEXT);
CREATE TABLE calendar(id TEXT PK, account_id TEXT NULL, remote_id TEXT, name TEXT, color TEXT, visible INT DEFAULT 1,
                      read_only INT DEFAULT 0, is_local INT, sync_token TEXT);
CREATE TABLE event(
  id TEXT PK, calendar_id TEXT, remote_id TEXT, etag TEXT, title TEXT, notes TEXT, location TEXT, url TEXT,
  start_utc TEXT, end_utc TEXT, all_day INT, tz_id TEXT,
  rrule TEXT NULL, exdates TEXT NULL,           -- RFC5545
  recurrence_master_id TEXT NULL, recurrence_id_utc TEXT NULL,  -- for edited single occurrences (exceptions)
  color TEXT, meeting_url TEXT, travel_minutes INT,
  created_utc TEXT, updated_utc TEXT, deleted INT DEFAULT 0, dirty INT DEFAULT 0);
CREATE INDEX ix_event_range ON event(calendar_id, start_utc, end_utc);
CREATE TABLE alert(id TEXT PK, owner_type TEXT, owner_id TEXT,  -- 'event'|'reminder'|'task'
  trigger_offset_min INT NULL, trigger_at_utc TEXT NULL, priority INT, persistent INT, repeat_every_min INT, sound TEXT);
CREATE TABLE reminder(id TEXT PK, title TEXT, notes TEXT, due_utc TEXT, tz_id TEXT, rrule TEXT, priority INT,
  flagged INT, done_utc TEXT NULL, list_id TEXT NULL, created_utc TEXT, updated_utc TEXT, deleted INT, dirty INT);
CREATE TABLE task_list(id TEXT PK, name TEXT, color TEXT, remote_id TEXT);
CREATE TABLE task(id TEXT PK, list_id TEXT, title TEXT, notes TEXT, due_utc TEXT NULL, has_time INT, priority INT,
  rrule TEXT, done_utc TEXT NULL, parent_id TEXT NULL, linked_event_id TEXT NULL, dirty INT, deleted INT);
-- Materialized upcoming fire times (the heart of reminders)
CREATE TABLE alert_instance(id TEXT PK, alert_id TEXT, owner_type TEXT, owner_id TEXT, occurrence_utc TEXT,
  fire_at_utc TEXT, state TEXT,  -- pending|fired|snoozed|done|missed|dismissed
  snoozed_until_utc TEXT NULL, fired_utc TEXT NULL, repeat_count INT DEFAULT 0);
CREATE INDEX ix_ai_fire ON alert_instance(state, fire_at_utc);
-- Sync
CREATE TABLE outbox(id INTEGER PK AUTOINCREMENT, entity TEXT, entity_id TEXT, op TEXT, payload TEXT, attempts INT, next_try_utc TEXT);
CREATE TABLE settings(key TEXT PK, value TEXT);
CREATE VIRTUAL TABLE event_fts USING fts5(title, notes, location, attendees, content='');
```
Migrations: numbered .sql files applied in order, version in `PRAGMA user_version`. DB mode WAL, `PRAGMA foreign_keys=ON`.

## 4. Recurrence algorithm
- Store master event with RRULE + EXDATE. **Never store every occurrence.**
- `Expand(master, rangeStart, rangeEnd)` using Ical.Net, in the event's own tz (DST-correct), return `Occurrence{EventId, StartUtc, EndUtc, IsException}`.
- Exceptions (edit this one): child row with `recurrence_master_id` + `recurrence_id_utc`; expansion replaces that occurrence.
- "This and following": set master `UNTIL = occurrence - 1s`, create new master from that point with new rule.
- View query: `SELECT non-recurring in range` ∪ `Expand(all recurring masters overlapping range)` — cache expansion per visible range (LRU, invalidated on write).
- Performance: index range + only expand masters where `rrule IS NOT NULL AND start_utc <= rangeEnd AND (until IS NULL OR until >= rangeStart)`.

## 5. Reminder engine (most important) — never miss a reminder
**Goal:** 99.9% within 5 s, even if app closed/sleep/reboot.

### 5.1 Planner (runs on app start, on any event/reminder change, daily at 03:00, on resume)
1. Horizon = now → now+14 days.
2. For each event/reminder/task with alerts: expand occurrences in horizon → compute `fire_at = occurrence_start - offset` (or absolute).
3. Upsert into `alert_instance` (idempotent via unique (alert_id, occurrence_utc)); delete instances of deleted/edited items still `pending`.

### 5.2 Three layers of delivery (defence in depth)
| Layer | Mechanism | Purpose |
|---|---|---|
| L1 OS-scheduled | `ToastNotificationManager.CreateToastNotifier().AddToSchedule(ScheduledToastNotification)` for next ~200 pending instances (OS limit 4096) | Fires even if app process dead |
| L2 In-app timer | One `PeriodicTimer`/single `Timer` set to the *next* `fire_at` (min-heap by fire time, re-armed after each fire); app lives in tray via startup task | Precise firing, actions (snooze/done), persistent alarms, full-screen popup |
| L3 Catch-up | On start / `SystemEvents.PowerModeChanged(Resume)` / `SessionSwitch` / clock-change: query `state='pending' AND fire_at<=now` → fire as "Missed" summary toast | Sleep/off/time-change |

Dedup: toast `Tag = alert_instance.id`; before showing, check state; mark `fired` in a transaction so L1+L2 don't double-notify.

### 5.3 Pseudocode
```
loop:
  next = repo.PeekNextPending()            // ORDER BY fire_at_utc LIMIT 1
  delay = max(0, next.fire_at - clock.UtcNow)
  await Task.WhenAny(Task.Delay(min(delay, 60s)), wakeSignal)   // wake on edits/resume
  foreach due in repo.PendingUpTo(clock.UtcNow):
      if due.fire_at < now - 5min  -> state=missed (goes to catch-up summary)
      else notifier.Show(due); state=fired
      if alert.persistent: schedule repeat: fire_at += repeat_every_min (state=pending, repeat_count++) until Done
```
Snooze: `state=snoozed, snoozed_until=now+X`, create new pending instance at that time.
Mark done: state=done, cancel scheduled toast by tag, stop repeats; for recurring reminder planner creates next.
Clock/timezone change: re-run planner (instances are UTC; floating all-day alerts recomputed from local tz).
All-day/date reminder: fire_at computed in local tz at user-chosen time on day (and day before).
Priority: Critical -> alarm popup window (always-on-top) + sound; honour Focus Assist unless user allowed.
Daily digest: a synthetic alert instance each morning with counts.

### 5.4 Background
Packaged WinUI app registers `StartupTask` (windows.startupTask manifest ext) -> launches minimised to tray. Tray = keeps L2 alive. Toast activation uses `AppNotificationManager` (Windows App SDK) so clicking buttons can relaunch app and handle args (`action=snooze&id=...&min=10`).

## 6. Natural-language quick add
Pipeline: `Tokenize -> Extract(tags/priority) -> Extract(reminder phrase) -> Extract(recurrence) -> Extract(duration) -> Extract(date/time) -> Extract(location "at X") -> remaining = title`.
- Extract in this order so patterns don't overlap; each extractor removes the matched span from the text.
- Rules: `#work`, `!high`, `remind( me)? (\d+) (min|hour)s? before`, `every (mon|tue..|day|week)`, `for (\d+)\s?(h|m|min|hours)`, `in (\d+) (min|hours)`, `today|tomorrow|next <weekday>|<weekday>|dd/mm|Jan 5`, `(\d{1,2})(:\d{2})?\s?(am|pm)`, `at <Place>` (after time extraction).
- Output `ParsedItem{Kind=Event|Reminder, Title, Start, End, Location, Alerts[], Rrule, CalendarHint, Priority, Confidence}`.
- Default kind: has "remind me" without duration -> Reminder; otherwise Event. Missing time -> all-day (event) / 9:00 (reminder).
- Fully unit-tested with a table of ≥20 phrases. `IClock` injected for deterministic tests.

## 7. Sync engine (offline-first)
- Local DB is the source of truth for UI. Every local change sets `dirty=1` and writes an `outbox` row.
- **Push:** worker drains outbox per account (exponential backoff on failure, idempotent by remote_id/etag).
- **Pull (incremental):** Google `syncToken`; Graph `deltaLink`; CalDAV `sync-collection`/ctag+etag. On 410 Gone -> full resync.
- Triggers: app start, every 5 min, push notification (Google watch/Graph subscription) when available, manual refresh, network regained (`NetworkChange`).
- **Conflict rule:** compare `etag/updated`; if both changed -> last-edit-wins, store loser as "(conflict copy)" event.
- Provider interface: `Task<PullResult> PullAsync(Calendar, token)`, `Task PushAsync(OutboxItem)`.
- OAuth: PKCE + loopback redirect (`http://127.0.0.1:<port>`) via system browser; refresh tokens only in Credential Manager.
- Invitations: RSVP via provider API; send invites through connected account.

## 8. Security
| Area | Decision |
|---|---|
| Tokens | Windows Credential Manager (`PasswordVault`/`CredWrite`), never DB/files |
| DB at rest | SQLCipher; key = random 256-bit generated once, wrapped with DPAPI (`ProtectedData`, CurrentUser) and stored in Credential Manager |
| OAuth | PKCE, minimal scopes (Calendar read/write only), no client secret in desktop app |
| Network | HTTPS only, certificate default validation, no custom trust |
| Input | Parameterised SQL only (Dapper params); sanitize rich text notes (strip scripts), validate URLs (http/https only) before launching |
| Logs | Serilog to local file; never log tokens, event bodies, emails; rolling 7 days |
| Privacy | No account needed; analytics opt-out & anonymous; "Delete all data" wipes DB + credentials |
| Lock | Optional Windows Hello (`UserConsentVerifier`) on launch (premium) |
| Supply chain | Pin NuGet versions, `dotnet list package --vulnerable` in CI |
| Billing | Verify license via `StoreContext.GetAppLicenseAsync` — premium gate in one `IEntitlementService`; never trust local flag only |

## 9. Premium gating
Single `IEntitlementService.Can(Feature f)`; features enum mirrors spec table 8.1; free limits (1 account, 3 alarms, 1 task list, 2 templates) enforced in Services layer (not only UI). UI shows crown badge, tapping opens paywall.

## 10. Performance
- Cold start <1s: lazy-load Sync/Settings, no sync on startup path, show last-viewed range from DB, `ReadyToRun` + trimming off for WinUI (use R2R).
- Virtualized lists (ItemsRepeater) for Agenda; custom Canvas-based TimeGrid for Day/Week.
- Query by range with indexes; expansion cache; batch DB writes in transactions.
- 10,000 events benchmark test in Data.Tests.

## 11. Error handling & logging
Result types for expected failures (network/auth), exceptions for bugs; global unhandled handler logs + friendly dialog; sync errors show in status icon tooltip, retried automatically.

## 12. Testing strategy
Unit: recurrence (DST, EXDATE, this-and-following), planner, snooze, parser (20+ phrases), conflict resolver. Integration: SQLite in-memory/temp file. Manual QA checklist: sleep/wake, reboot, DND, high-DPI, Narrator.
