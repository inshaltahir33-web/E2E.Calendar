# Fix Plan — E2E Calendar

*Author: Antigravity · Date: 25 Oct 2026*
*Ordered by: blocker → MVP gap → Polish → Phase gates*

---

## Work packages

### WP-01 · Reminder engine (L1/L2/L3) — BLOCKER
**Size:** L (4-6 days) · **Risk:** High (core reliability) · **Deps:** none

Replace the current 30-second polling stub (`ReminderNotificationService`) with the full three-layer engine described in `docs/02-ARCHITECTURE.md §5`:

- **L1** — `ScheduledToastNotification` for next ~200 pending `alert_instance` rows (OS fires even if process dead)
- **L2** — min-heap timer (`PeriodicTimer` / `Task.Delay` to exact `fire_at`); in-process precise fire; alarm-style `AlarmPopupWindow` (always-on-top, persistent)
- **L3** — catch-up on `SystemEvents.PowerModeChanged(Resume)` / `SessionSwitch` / clock-change; query `state='pending' AND fire_at<=now`

Toast buttons: Snooze / Done / Open / Join via `AppNotificationManager` with `action=snooze&id=...&min=10` args.
Snooze: create new `alert_instance` at `now+X`. Done: mark done, cancel scheduled toast by tag.
Target: all 237 existing tests plus new harness (fake clock) demonstrating 99.9 % within 5 s.

---

### WP-02 · OS StartupTask manifest — BLOCKER
**Size:** S (half day) · **Risk:** Low · **Deps:** WP-01

Add `Package.appxmanifest` (or update existing MSIX manifest) with:
```xml
<Extensions>
  <uap3:Extension Category="windows.startupTask" EntryPoint="E2E.Calendar.App.App">
    <uap3:StartupTask TaskId="E2ECalendarStartup" Enabled="false" DisplayName="E2E Calendar" />
  </uap3:Extension>
</Extensions>
```
Add Settings toggle "Start at login" calling `StartupTask.GetAsync()` + `.RequestEnableAsync()`. Verify tray stays alive to keep L2 running.

---

### WP-03 · `IDialogService` + dialog lifecycle — Foundation
**Size:** M (2 days) · **Risk:** Medium (touches many files) · **Deps:** none

Create `IDialogService` in Core.Abstractions:
```csharp
interface IDialogService {
    Task<bool> ConfirmAsync(string title, string body, string yes, string no);
    Task<T?> ShowAsync<T>(Func<T> factory) where T : ContentDialog;
}
```
Replace all 9 raw `new ContentDialog {...}.ShowAsync()` call sites with `IDialogService`. Guard against stacking (track open dialog; if one open, queue or refuse). Fix `RecurrenceEditDialog` XamlRoot assignment. Register `DialogService` in DI.

---

### WP-04 · UI grid fixes (UI-AUDIT #1-12) — Foundation / UI
**Size:** M (2-3 days) · **Risk:** Medium · **Deps:** none

Fix all 12 known visual bugs from `docs/09-UI-AUDIT.md`:
1. Mini-calendar: replace stock `CalendarView` with custom 7-column `Grid` (7×36×36 cells per design tokens).
2. Time labels: add dedicated 56px gutter column; draw labels right-aligned offset -8 on hour lines.
3. Day header alignment: share column definitions between header and scrollable body.
4. Keyboard accelerator "N" badge: set `KeyboardAcceleratorPlacementMode="Hidden"` on all accelerators.
5. Duplicate period title: remove secondary header from grid top.
6. "Weekends" button: change to `ToggleMenuFlyoutItem` inside a "View options ⋯" menu.
7. Auto-scroll on open: `ScrollViewer.ScrollToVerticalOffset(now - 90 min)`, else 07:00.
8. Red now-line: 2px `SystemFillColorCriticalBrush`, 8px dot at gutter, updates every 30s.
9. Today column tint + filled accent circle in header.
10. Vertical day-separator lines (1px `DividerStrokeColorDefaultBrush`).
11. Sidebar scroll + pinned footer (sync status bar not mixed into sidebar).
12. Mica layering: `MicaBackdrop`, content card `Background=LayerFillColorDefaultBrush`, `CornerRadius=8`.

---

### WP-05 · `GoogleSyncEnabled` build flag — Ground rule
**Size:** XS (30 min) · **Risk:** Low · **Deps:** none

Wrap `GoogleCalendarProvider` registration in:
```csharp
#if GoogleSyncEnabled
services.AddSingleton<GoogleCalendarProvider>();
...
#endif
```
Add `<DefineConstants>$(DefineConstants);GoogleSyncEnabled</DefineConstants>` as a separate build configuration or csproj property (default `false`). Document in README.

---

### WP-06 · Tags / categories UI — MVP gap
**Size:** M (2 days) · **Risk:** Low · **Deps:** none

- Tag chip input in `EventEditorView` (comma-separated, auto-complete from existing tags).
- Tag filter in search flyout.
- `SearchService.FilterByTagsAsync()` using FTS5 `event_fts`.

---

### WP-07 · Holiday calendar bundles — MVP gap
**Size:** S (1 day) · **Risk:** Low · **Deps:** IcsService (working)

- Bundle `holidays_PK.ics`, `holidays_US.ics`, `holidays_UK.ics`, `holidays_UAE.ics`, `holidays_IN.ics` as embedded resources.
- Add country toggles in Settings → Calendars.
- Create read-only local calendar per country on enable; delete on disable.
- Source: public domain ICS files (e.g. holidays.js project or government sources).

---

### WP-08 · Store billing / paywall — MVP (revenue path)
**Size:** L (4-5 days) · **Risk:** High (external service) · **Deps:** WP-03 (IDialogService)

- Implement `StoreEntitlementService : IEntitlementService` using `Windows.Services.Store.StoreContext.GetAppLicenseAsync()`.
- `PaywallView` (3 cards: Monthly / Yearly highlighted / Lifetime; feature list; Restore; Skip).
- Wire all `IEntitlementService.Can()` check failures to navigate to paywall.
- Debug-only `IEntitlementService` fake-premium toggle.
- Enforce free-tier limits in all services (accounts: 1, persistent alarms: 3, task lists: 1, templates: 2).

---

### WP-09 · Taskbar jump list + overdue badge — MVP Windows integration
**Size:** S (1 day) · **Risk:** Low · **Deps:** none

- `JumpList.LoadCurrentAsync()` → add items: New event, New reminder, Today.
- `BadgeNotificationManager.CreateBadgeUpdaterForApplication()` → overdue task/reminder count.

---

### WP-10 · Share target — MVP Windows integration
**Size:** S (1 day) · **Risk:** Low · **Deps:** none

- Add `<uap:Extension Category="windows.shareTarget">` to manifest.
- Handle `ShareOperation` activation in `App.OnActivated`; pre-fill QuickAdd with shared text/URL.

---

### WP-11 · CalDAV credential UI — MVP (partial feature completion)
**Size:** S (1 day) · **Risk:** Low · **Deps:** CalDavProvider (already DI-registered)

- Add "Add CalDAV / iCloud" dialog: server URL, username, app-specific password fields.
- Pass credentials to `CalDavProvider`; verified connection; store in Credential Manager.

---

### WP-12 · Rich-text notes — MVP event extras
**Size:** S (1 day) · **Risk:** Low · **Deps:** WP-03

- Replace plain `TextBox` in `EventEditorView` with `RichEditBox`.
- Sanitise on save (strip scripts; store as sanitised HTML or Markdown).

---

### WP-13 · Reminder panel + alarm popup — MVP (tied to WP-01)
**Size:** M (2 days) · **Risk:** Medium · **Deps:** WP-01

- `AlarmPopupWindow`: always-on-top, centred, big title, sound (`MediaPlayer`), Snooze▾/Done/Open buttons.
- Custom sounds: `alert.sound` column → `MediaPlayer.Source`.
- Missed-reminder summary toast: `SystemEvents.PowerModeChanged`.

---

### WP-14 · Analytics opt-out — MVP (minimal)
**Size:** XS (0.5 day) · **Risk:** None · **Deps:** none

- Add Settings toggle "Anonymous analytics" (default off).
- Persist to `settings` table. No analytics collector needs to be built now — stub `IAnalyticsService` that checks the flag before emitting.

---

### WP-15 · `InMemorySecretStore` to test project — Hygiene
**Size:** XS (< 1 hr) · **Risk:** None · **Deps:** none

Move `src/E2E.Calendar.Sync/InMemorySecretStore.cs` to `tests/E2E.Calendar.Sync.Tests/`.

---

### WP-16 · Dead service cleanup — Hygiene
**Size:** XS (1 hr) · **Risk:** Low · **Deps:** none

- Remove `WeatherService` and `ProductivityAnalyticsService` DI registrations until feature is surfaced (re-add in v1.1 weather work).
- Leave code; just don't register. Or gate behind `#if` / feature flag.

---

### WP-17 · Performance verification — Phase 6 gate
**Size:** M (2 days) · **Risk:** Low · **Deps:** app running

- Cold-start stopwatch (via ETW or manual timer before `OnLaunched` → first frame).
- Run existing 10,000-event Data benchmark; record numbers in `docs/PERF.md`.
- Memory profile (VMMap or dotMemory).

---

### WP-18 · Accessibility full audit — Phase 6 gate
**Size:** M (2 days) · **Risk:** Low · **Deps:** app running

- Narrator walk-through of all interactive controls.
- Keyboard-only navigation test (tab order, focus visuals).
- High-contrast mode visual check.
- 200% text scaling layout test.
- Record results in `docs/A11Y.md`.

---

### WP-19 · MSIX packaging script + WACK — Phase 7
**Size:** M (2 days) · **Risk:** Medium (signing cert) · **Deps:** all prior WPs

- `scripts/build-msix.ps1`: restore → build → test → publish x64+ARM64 → create `.msixbundle` → sign with test cert.
- `docs/PACKAGING.md`: install test cert, sideload, Partner Center identity placeholders.
- WACK run (or mark NOT RUN with command).

---

### WP-20 · `docs/ACCEPTANCE.md` — Phase 7 gate
**Size:** S (0.5 day) · **Risk:** None · **Deps:** all WPs done

- One-checkbox-per-criterion from spec §10 with PASS / FAIL / NOT RUN and evidence.

---

## Execution order

```
Phase 2 (Foundation):
  WP-15 → WP-16 → WP-05 → WP-03 → WP-04

Phase 3 (UI rebuild):
  WP-04 (complete all 12 grid fixes) → WP-12

Phase 4 (Functional QA):
  WP-01 → WP-02 → WP-13 (reminder reliability)
  WP-06 → WP-07 → WP-09 → WP-10 → WP-11

Phase 5 (Missing features):
  WP-08 (billing / paywall)
  WP-14 (analytics stub)
  WP-03 (IDialogService if not done)

Phase 6 (Quality gates):
  WP-17 (perf) → WP-18 (a11y)

Phase 7 (Packaging):
  WP-19 → WP-20
```

---

## Risk summary

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Google OAuth app not approved | High | High | Keep behind `GoogleSyncEnabled=false`; test with Microsoft only for MVP |
| `StoreContext` billing requires Partner Center app | High | High | Use fake `IEntitlementService` for dev; paywall UI testable with debug toggle |
| WACK failure (manifest/API restrictions) | Medium | High | Run WACK early in Phase 7; fix issues before submission |
| Reminder L1 OS limit (4096 scheduled toasts) | Low | Medium | Schedule next ~200 only; re-schedule on fire |
| 10,000-event performance target | Low | Medium | LRU cache + canvas virtualisation already in code; measure first |
