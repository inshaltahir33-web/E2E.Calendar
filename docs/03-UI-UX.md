# UI / UX Layout

## Design tokens
- Fluent 2 / WinUI 3, Mica backdrop (`MicaBackdrop`), corner radius 8, spacing scale 4/8/12/16/24.
- Use ThemeResource only (Light/Dark/HighContrast). Accent = system accent. Calendar colors from a 12-color accessible palette.
- Typography: Segoe UI Variable; Title 20, Subtitle 16, Body 14, Caption 12.
- Motion: subtle 150ms ease; respect "animation effects off".
- Accessibility: AutomationProperties.Name on all, focus visuals, never colour-only (icons for priority).

## Main window (Week view)
```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│ ☰  Calendar   [◀ Today ▶]  Oct 2026      [Day|Week|Month|Agenda]  🔍 Ctrl+K  ⟳  ⚙ │  <- TitleBar (Mica)
├───────────────┬──────────────────────────────────────────────────────────────────────┤
│ ┌───────────┐ │       Mon 19   Tue 20   Wed 21   Thu 22   Fri 23   Sat 24   Sun 25    │
│ │ Oct 2026  │ │ all-day ▒▒Birthday▒▒                                                  │
│ │ mini-cal  │ │ 09:00 ┆        ┌──────────┐                                          │
│ └───────────┘ │ 10:00 ┆        │Standup   │      ┌───────┐                           │
│ + New ▾       │ 11:00 ┆        └──────────┘      │Dentist│                           │
│ ─ Calendars ─ │ 12:00 ┆   ☐ Call bank (reminder)                                     │
│ ☑ Work   ●    │ 13:00 ┆ ━━━━━━━━━━━━━ 13:42 (red now line) ━━━━━━━━━━━━━             │
│ ☑ Personal ●  │ 14:00 ┆                                                               │
│ ☑ Family  ●   │                                                                       │
│ ─ Reminders ─ │                                                                       │
│  Today (3)    │                                                                       │
│  Overdue (1)  │                                                                       │
│ ─ Tasks ─     │                                                                       │
│  Inbox  Work  │                                                                       │
├───────────────┴──────────────────────────────────────────────────────────────────────┤
│ ✔ Synced 2 min ago · Google, Outlook                              Premium 👑         │  <- status bar
└──────────────────────────────────────────────────────────────────────────────────────┘
```
Sidebar collapses to icons < 900px. Right-side **Event details flyout** (not modal) opens on click.

## Screens
1. **Day/Week**: TimeGrid, all-day strip, red now-line, drag-create, drag-move/resize, secondary time zone column. Reminders as tick-box items.
2. **Month**: 6x7 grid, chips (max 3) + "+N more" popup, heat dots. Click day -> Day view; double-click -> quick add.
3. **Agenda**: grouped by day (sticky headers), events + reminders + tasks, tick-box inline.
4. **Event editor flyout**: Title, date/time pickers (all-day toggle), repeat, alerts (multi), location(map link), meeting link (Join), notes, calendar/color, attendees. Buttons: Save, Delete (undo snackbar 10 s).
5. **Quick Add (Win+Alt+C)**: small centred borderless window over any app:
```
┌───────────────────────────────────────────────┐
│ ＋ Lunch with Ali tomorrow 1pm at Kohinoor…   │
│ ─────────────────────────────────────────────  │
│ 📅 Event · Lunch with Ali                      │  <- live parsed preview
│ 🕐 Wed 21 Oct, 1:00–2:00 PM   📍 Kohinoor      │
│ 🔔 30 min before          ● Personal           │
│                          [Esc Cancel] [Enter ✓]│
└───────────────────────────────────────────────┘
```
6. **Reminders panel**: smart lists (Today, Scheduled, Overdue, Completed, Flagged) as tiles on top + list below.
7. **Alarm popup** (Critical/persistent): centred, always-on-top, big title, sound, buttons **Snooze ▾ (5/10/30/1h/tomorrow/custom) · Done · Open**.
8. **Toast**: title, time, buttons Snooze / Done / Open / Join.
9. **Tray flyout**: next event, today's agenda list, New event, New reminder, Remind me in 10/30/60.
10. **Settings**: General, Accounts, Calendars, Notifications & sounds, Appearance, Shortcuts, Premium, Privacy & data, About.
11. **Onboarding (3 screens)**: Connect account -> Theme -> Enable notifications.
12. **Paywall**: 3 cards (Monthly / Yearly highlighted "Save 44%" / Lifetime), feature list with crown, Restore purchases, Skip.
13. **Command palette (Ctrl+K)**: fuzzy search over actions + events.

## Navigation / state
Single `MainWindow` + `Frame` navigation; `NavigationService` in App; view state (date, view mode) in `CalendarStateViewModel` singleton. Keyboard: N, R, T, D/W/M/A, Ctrl+F, arrows, Ctrl+Z, Ctrl+K.

## Empty/Loading/Error states
Skeleton while loading range; friendly empty state ("Nothing today 🎉 — press N"); inline error bar for sync with Retry.
