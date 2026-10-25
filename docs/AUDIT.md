# Audit Report — E2E Calendar

*Author: Antigravity · Date: 25 Oct 2026*
*Build: `dotnet build E2E.Calendar.sln -c Debug -p:Platform=x64` → **Build succeeded, 0 errors, 0 warnings***
*Tests: Core 237/237 ✅ · Data 60/60 ✅ · Sync 7/7 ✅ · Total **304 passed, 0 failed***
*App launch: NOT RUN (WinUI 3 requires VS/MSBuild F5; no GUI available in CLI session)*

---

## 1. Architecture and project structure

**Positive:**
- Clean layered architecture follows the spec perfectly: Core (pure C#) → Data (SQLite/Dapper) → Sync (Google/Graph/CalDAV/ICS) → Platform (toast/tray/hotkey) → App (WinUI 3/MVVM).
- Single DI container (`Microsoft.Extensions.Hosting`) in `App.xaml.cs`. All services registered as singletons with correct interface/implementation pairs.
- `IClock`, `ISecretStore`, `IEntitlementService`, `ISettingsService`, `IShellState`, `IEventQueryCache` — central service interfaces present.
- SQLite WAL mode, proper migrations (5 numbered .sql files), PRAGMA foreign_keys=ON.
- No circular dependencies found. Core references no Windows/UI types.

**Issues:**
- **No `IDialogService`** — `ContentDialog` is instantiated directly in 9 different call sites (MainWindow.xaml.cs ×4, SettingsView.xaml.cs ×4, MiniCalendarView.xaml.cs ×1, OnboardingView.xaml.cs ×1, RecurrenceEditDialog.cs ×1, ReminderQuickAdd.cs ×1). This violates the "one way to do dialogs" rule and makes dialog lifecycle untestable. Risk: stacked dialogs, orphan overlays on navigation.
- **No `INavigationService` interface** — `NavigationService` is a concrete class; ViewModels reference it directly rather than through an interface.
- **`IShellState` / `NavigationService` are separate** — navigation route state (IShellState) and view materialisation (NavigationService) are intentionally split per KNOWN-GAPS.md, but this layering is undocumented in code and will confuse maintainers.
- **No `Package.appxmanifest`** — the project builds as an unpackaged MSIX (sparse/self-contained). There is no `<Extensions><uap3:Extension Category="windows.startupTask"/>` manifest entry. The `StartupTask` (background launch at login) required for reliable reminders is therefore **not registered with the OS**.
- **`GoogleSyncEnabled` build flag is missing** — the MASTER-PROMPT ground rule requires Google sync to stay behind `GoogleSyncEnabled` flag (default false) until OAuth app is approved. Currently `GoogleCalendarProvider` is always registered and active.

---

## 2. Duplicate or conflicting classes/styles/services

- Theme files `Tokens.xaml`, `Styles.xaml`, `Layout.xaml` follow the design token system. No competing `.xaml` dictionaries found. Single theme file set — **no duplication**.
- `WeatherService` registered in DI but no UI surface exposes weather (v1.1 feature). Service exists but is unreachable — **dead registration**.
- `DemoDataSeeder` is correctly gated behind `#if DEBUG && E2E_DEMO=1`. Not a production concern.
- `FreeEntitlementService.Can(Feature f)` returns `true` only for `CalendarTemplates`. All other premium features return `false`. This is intentional for the free-tier stub, but the Store billing implementation (`StoreContext`) is **entirely absent** — no paywall UI, no purchase flow.

---

## 3. Dead code

| Item | File | Notes |
|---|---|---|
| `WeatherService` | App/Services/WeatherService.cs | Registered in DI; no ViewModel or View consumes it |
| `ProductivityAnalyticsService` | Core/Services/ProductivityAnalyticsService.cs | Registered in DI; no call site found |
| `WidgetAgendaCardService` | Platform/WidgetAgendaCardService.cs | Registered; Windows 11 Widgets not wired in App |
| `DemoSchedule.cs` | Core/Services/DemoSchedule.cs | Helper for DemoDataSeeder; only used indirectly via `DemoDataSeeder` which is `#if DEBUG` |
| `InMemorySecretStore.cs` | Sync/InMemorySecretStore.cs | Test stub kept in production project; should be in test project (or Sync.Tests) |

---

## 4. Stub / fake implementations

| Item | Evidence | Impact |
|---|---|---|
| **Store billing / paywall** | `FreeEntitlementService` always returns free-tier; no `StoreContext` call anywhere | Premium features unreachable; revenue path missing entirely |
| **Reminder engine (L1 + L2 + L3)** | `ReminderNotificationService` shows a basic toast via `ToastNotificationManager.CreateToastNotifier().Show(...)` on a 30-second poll; no `ScheduledToastNotification`, no `alert_instance` table queries, no snooze/done action handling, no wake-from-sleep hook, no persistent/alarm-style popup | Core reliability spec (99.9 % within 5 s) cannot be met |
| **OS startup task** | No `startupTask` extension in manifest; no code to register startup | Reminders will not fire when app window is closed and app not in tray |
| **Paywall dialog** | No PaywallView, PaywallDialog, or paywall navigation route | Free-tier limit prompts have nowhere to go |
| **Windows Hello lock** | Mentioned in spec; `UserConsentVerifier` not referenced anywhere | Premium feature missing |
| **Compact always-on-top strip** | Not present | Premium feature missing |
| **Taskbar jump list / badge** | `JumpList` / `BadgeNotificationManager` not referenced | MVP Windows integration missing |
| **Share target** | Not in manifest or code | MVP Windows integration missing |
| **ICS holiday calendars** | `IcsService` exists; no bundled `.ics` files for PK/US/UK/UAE/IN found; no toggle in Settings | MVP feature missing |
| **Search UI** | FTS5 `event_fts` table exists; `EventQueryService` exists; toolbar search box is wired up per wiring-audit (WORKING); search filter flyout also present | Likely working; NOT RUN manually |
| **Command palette** | `Ctrl+K` accelerator handler exists; PaletteCommand list and ContentDialog shown | Present but dialog-based (not overlay); fuzzy matching quality NOT RUN |
| **Tags / categories** | `tags` column in `event` table; no tag-creation UI found | Partial — DB ready, UI missing |
| **CalDAV credential UI** | `CalDavProvider` DI-registered; Settings "Add CalDAV" button wired but uses pre-configured endpoint — no interactive credential-entry form | Partial |

---

## 5. UI inconsistencies

| # | Issue | Source |
|---|---|---|
| 1 | Mini-calendar Saturday column clips if sidebar is too narrow | docs/09-UI-AUDIT.md — known |
| 2 | Day header abbreviations ("Mo", "We") wrap to two lines | docs/09-UI-AUDIT.md — known |
| 3 | Time labels draw inside grid columns instead of a dedicated gutter | docs/09-UI-AUDIT.md — known |
| 4 | Day header row misaligns with grid columns | docs/09-UI-AUDIT.md — known |
| 5 | Stray "N" badge appears in grid centre (KeyboardAccelerator tooltip visible) | docs/09-UI-AUDIT.md — known |
| 6 | Period title appears twice (toolbar + grid header) | docs/09-UI-AUDIT.md — known |
| 7 | "Weekends" rendered as a wide primary accent button | docs/09-UI-AUDIT.md — known |
| 8 | Grid starts at 00:00; no auto-scroll to current time; no red now-line | docs/09-UI-AUDIT.md — known |
| 9 | Today column not highlighted | docs/09-UI-AUDIT.md — known |
| 10 | Sidebar cuts off at bottom; status bar bleeds into sidebar | docs/09-UI-AUDIT.md — known |
| 11 | No vertical day-separator lines in grid | docs/09-UI-AUDIT.md — known |
| 12 | No visual hierarchy (all surfaces same flat grey; no Mica depth) | docs/09-UI-AUDIT.md — known |

---

## 6. Navigation and dialog/flyout lifecycle issues

- **ContentDialog created inline** (9 call sites) rather than through `IDialogService`. Risk of stacked dialogs if a slow async operation is awaited while user double-clicks.
- **RecurrenceEditDialog.cs** creates a `ContentDialog` with no `XamlRoot` assignment visible at the call-site level — must be verified it gets `XamlRoot` set before `ShowAsync()` or it will throw at runtime.
- **No guard against multiple simultaneous dialogs** — e.g., clicking "Delete" in EventEditor while a recurrence-edit dialog is open.
- **QuickAdd window** (`QuickAddView`) is kept as a singleton window reference in `App`; if the hotkey is pressed while it is already open, `Activate()` is called correctly. The close path sets `_quickAddWindow = null`. Appears correct.

---

## 7. Threading and performance risks

- `EventQueryService` uses an LRU cache (`IEventQueryCache`). Cache invalidation on write is implemented. **Performance claim: NOT VERIFIED** (10,000-event benchmark test noted in `Data.Tests` — present but result NOT RUN in this session).
- `ReminderNotificationService` uses `PeriodicTimer(30s)` polling. This is a shallow fallback — not the L1/L2/L3 multi-layer design specified.
- `SyncCoordinator` runs periodic sync. Exponential backoff and `Retry-After` handling present in both Google and Graph providers. **Sync correctness NOT RUN** (no real OAuth credentials in environment).
- No `ConfigureAwait(false)` enforcement noted; Core lib is pure C# and tests pass, so this is low risk but should be reviewed in hot paths.

---

## 8. Security issues

- `InMemorySecretStore.cs` lives in the production `E2E.Calendar.Sync` project — it stores secrets in a plain `Dictionary<string, string>`. This is meant for tests. If ever mistakenly registered in production DI it would expose tokens. Should be moved to `Sync.Tests`.
- `App.xaml.cs` registers `ISecretStore` → `CredentialStore` (Windows Credential Manager). Correct.
- No `.pfx`, `.db`, or committed secret/token files found in git history scan scope (working tree only — history scan requires `git log` which was not run; mark **NOT RUN**).
- Logs configured with Serilog, rolling 7-day. No event bodies or tokens should be logged — NOT VERIFIED by code grep; Serilog config looks correct but individual log calls not reviewed exhaustively.

---

## 9. Missing MVP items not yet in code

(Detailed in REQUIREMENTS-MATRIX.md; summarised here by severity)

**Blocker — cannot ship without:**
1. Reminder engine L1 (ScheduledToastNotification) + L2 (min-heap timer) + L3 (wake catch-up) — current impl is primitive polling.
2. OS `startupTask` manifest extension — reminders die when app closes.
3. Store billing / paywall / `IEntitlementService` → `StoreContext` implementation.
4. Free-tier limit enforcement in services (currently all premium features allowed).

**High — major spec gap:**
5. Tags/categories UI.
6. Holiday calendar bundles (.ics files + Settings toggle).
7. Taskbar jump list + overdue badge.
8. Share target manifest entry.
9. Command palette quality (fuzzy match depth).

**Medium — needed before WACK:**
10. MSIX packaging script + test certificate.
11. Windows App Certification Kit run.
12. Store listing assets (icons, screenshots, description).
