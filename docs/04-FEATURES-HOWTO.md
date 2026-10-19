# Features: Kya, Kaise, Kahan, Done Kab

Legend: D1 = aaj (Day 1), later = baad me.

| # | Feature | Kahan banega | Kaise (short) | Done criteria | When |
|---|---|---|---|---|---|
| 1 | Local calendars & events CRUD | Core/Services/EventService, Data/Repos | Repo + service + VM; soft delete + undo 10s | Create/edit/dup/delete, undo works | D1 |
| 2 | Day/Week/Month/Agenda | App/Views, Controls/TimeGrid | Range query -> occurrences -> layout engine (overlap columns) | Smooth with 10k events | D1 |
| 3 | Mini calendar, today, shortcuts | App/Controls | CalendarView + KeyboardAccelerators | N,R,T,D,W,M,A work | D1 |
| 4 | Recurrence + exceptions | Core/Recurrence | Ical.Net RRULE, EXDATE, split series | 12 unit tests incl. DST | D1 |
| 5 | Reminders (event alert, standalone, recurring, all-day, countdown) | Core/Reminders | Planner -> alert_instance (see arch §5) | Fire with window closed | D1 |
| 6 | Persistent alarm, snooze, done | Core/Reminders + Platform/ToastNotifier | Repeat instances every N min until done | Toast buttons work | D1 |
| 7 | Missed catch-up | Platform/PowerEvents | Resume hook -> catch-up summary | Sleep test passes | D1 |
| 8 | Quick add NLP | Core/QuickAdd | Pipeline of extractors | 20 phrases pass | D1 |
| 9 | Global hotkey + tray | Platform/HotkeyService, TrayService | RegisterHotKey Win32 + H.NotifyIcon.WinUI | Works over any app | D1 |
| 10 | Tasks (basic local) | Core/Services/TaskService | Lists, due, subtasks, roll-over job | Tasks in all-day strip | D1 (basic) |
| 11 | ICS import/export/subscription | Sync/Ics | Ical.Net parse; subscription = HTTP GET w/ ETag, read-only | Round-trip identical | D1 |
| 12 | Time zones | Core | Store tz_id; convert at render; secondary column | PC tz change updates view | D1 basic |
| 13 | Holidays by country | Sync/Ics | Bundled .ics per country (PK, US, UK, UAE, IN) | Toggle in settings | D2 |
| 14 | Search | Data (FTS5) | event_fts + SearchService | <100ms on 10k | D2 |
| 15 | Google two-way sync | Sync/Google | OAuth PKCE, syncToken, outbox | Edit offline -> syncs | D2 |
| 16 | Microsoft Graph sync | Sync/Microsoft | MSAL + delta query | Same | D3 |
| 17 | Invitations RSVP | Sync + EventService | Provider API respond | Accept/Tentative/Decline | D3 |
| 18 | Premium + paywall + billing | Core/Entitlements, Platform/Store | StoreContext, gating in services | Buy/restore/expiry tested | D4 |
| 19 | Calendar sets/profiles | Core + Settings | Saved visibility presets | Premium | D4 |
| 20 | Find free slot / share availability | Core/FreeSlotService | Merge busy intervals, invert within work hours | Copy text works | D4 |
| 21 | Widgets, jump list, Hello lock, compact strip | Platform | Windows App SDK widgets, JumpList, UserConsentVerifier | per spec | v1.1 |
| 22 | iCloud/CalDAV | Sync/CalDav | PROPFIND/REPORT, app-specific pw | Two-way | v1.1 |
| 23 | Year/multi-day view, Hijri dates, weather, habit | App | HijriCalendar class, weather API | | v1.1 |
| 24 | Location & travel-aware reminders, pen, WinSearch | Platform | Geolocator, routing API | | v2 |
| 25 | Localization (Urdu RTL etc.) | App/Strings | .resw per language, FlowDirection | | later |

## Kaise apply karna (har feature ke liye standard flow)
1. Core me interface + model + unit test (red).
2. Implementation (Data/Sync/Platform) (green).
3. DI registration in `App.xaml.cs` (`Host.CreateDefaultBuilder`).
4. ViewModel (CommunityToolkit `[ObservableProperty]`, `[RelayCommand]`).
5. XAML view bound to VM; check light/dark/high-contrast.
6. Manual test checklist; commit; push.

## Notification kaise kaam karta hai (short)
Change hota hai -> Planner `alert_instance` rows banata hai -> L1 OS scheduled toast + L2 in-app timer + L3 catch-up. Details docs/02 §5.

## Backend kya hai?
Is app ka **koi apna server nahi** (privacy by default). "Backend" = local services layer (Core+Data) + provider APIs (Google/Microsoft/CalDAV). Optional cloud backup (v2) baad me alag service hogi.

## QA Checklist (MVP)
- [ ] 10k events: scroll 60fps  - [ ] Reminder fires: window closed / after sleep / after reboot
- [ ] Snooze/Done/Join from toast - [ ] 20 quick-add phrases - [ ] Hotkey over other apps
- [ ] DST change test - [ ] Narrator/keyboard/high-contrast - [ ] Store WACK pass
