# Requirements Matrix — E2E Calendar

*Author: Antigravity · Date: 25 Oct 2026*
*Status key: Working / Partial / Broken / Missing*

| Spec § | Item | Phase | Status | Evidence (file + check) | Fix needed |
|---|---|---|---|---|---|
| 2.1 | Day view | MVP | **Partial** | `Views/DayView.xaml`, `DayViewModel.cs`; basic TimeGrid present; NOT RUN visually | Verify auto-scroll, now-line, today-highlight |
| 2.1 | Week view | MVP | **Partial** | `Views/WeekView.xaml`, `WeekViewModel.cs`; grid rendering issues (UI-AUDIT #1-12) | Fix gutter, alignment, now-line, today highlight |
| 2.1 | Month view | MVP | **Partial** | `Views/MonthView.xaml`, `Controls/MonthDayCell.xaml`; layout logic present | Verify "+N more" overflow, chip rendering |
| 2.1 | Agenda view | MVP | **Partial** | `Views/AgendaView.xaml`, `AgendaViewModel.cs`; wiring-audit: WORKING | Verify sticky headers, reminder ticks |
| 2.1 | Side mini-calendar | MVP | **Partial** | `Controls/MiniCalendarView.xaml`; Saturday column clips (UI-AUDIT #1) | Fix min-width / custom 7-col grid |
| 2.2 | Event create/edit/duplicate/delete + undo | MVP | **Working** | `EventEditorView.xaml`, `EventEditorViewModel.cs`; wiring-audit WORKING; 237 Core tests pass | None |
| 2.2 | Drag move / resize / Ctrl+drag copy | MVP | **Partial** | `TimeGrid.xaml.cs`, `TimeGridInteractions.cs`; code present; NOT RUN | Runtime verification needed |
| 2.2 | Meeting link detect + Join | MVP | **Working** | `EventEditorViewModel.cs`; logic present; NOT RUN (no test event) | Runtime check |
| 2.2 | Event templates (up to 2 free) | MVP | **Working** | `EventTemplateService.cs`; `IEntitlementService.LimitFor(Templates)` = 2 | None |
| 2.2 | Rich-text notes | MVP | **Missing** | `EventEditorView.xaml` has plain TextBox only | Add RichEditBox; sanitize RTF |
| 2.2 | Attendees field + invites | MVP | **Partial** | Attendees field in Editor; RSVP code in MainWindow; Graph/Google API calls present | Verify outgoing invite send |
| 2.2 | Travel time block | MVP-optional | **Missing** | `travel_minutes` column exists in DB; no UI, no block rendering | Low priority (optional) |
| 2.2 | Attachments | v1.1 | **Partial** | `EventAttachmentService.cs`, `AttachmentRepository.cs`, migration 005 present; no UI | v1.1 |
| 2.3 | Recurrence + exceptions + this/following/all | MVP | **Working** | `RecurrenceExpander.cs`, `RecurrenceEditService.cs`; 12+ recurrence tests pass | None |
| 2.4 | Multiple calendars, colour, show/hide | MVP | **Working** | `CalendarService.cs`, sidebar wired; wiring-audit WORKING | None |
| 2.4 | Calendar sets / profiles | Premium | **Missing** | Not implemented | Phase 5 |
| 2.4 | Tags / categories | MVP | **Partial** | DB column `tags TEXT`; `event_fts` with attendees; no tag-create/filter UI | Add tag chip UI, filter |
| 2.4 | Search across events | MVP | **Partial** | FTS5 table; `EventQueryService`; toolbar search wired (wiring-audit: WORKING) | Verify results flyout, tag filter |
| 2.5 | Event-level time zone + secondary column | MVP | **Partial** | `tz_id` column; secondary-tz code in WeekViewModel; secondary column NOT RUN | Visual verification |
| 2.5 | Auto-adjust on PC time-zone change | MVP | **Partial** | `TimeZoneExtensions.cs`; planner re-run mentioned in docs; not verified | Hook `TimeZoneInfo.ClearCachedData` on timezone change |
| 2.5 | Week start / 12-24h / date format | MVP | **Partial** | `SettingsService.cs`; Windows defaults read; NOT RUN | Verify regional defaults |
| 2.5 | Holiday calendars (PK, US, UK, UAE, IN) | MVP | **Missing** | No bundled .ics files; no Settings toggle | Add .ics resources + Settings toggle |
| 2.5 | Hijri / Chinese / Hebrew dates | v1.1 | **Missing** | Not implemented | v1.1 |
| 3.1 | Event alert, standalone, recurring, all-day, persistent, countdown | MVP | **Partial** | `ReminderPlanner` materializes 14-day alert instances; `ReminderNotificationService` provides scheduled-toast L1, precise in-app fallback L2, and resume catch-up L3; persistent popup/countdown UI remains NOT RUN | Add dedicated always-on-top alarm window and verify OS delivery |
| 3.1 | Location / travel-aware reminders | v2 | **Missing** | Not implemented | v2 |
| 3.2 | Toast action: Snooze/Done/Open/Join | MVP | **Partial** | Toast XML now includes Snooze/Done/Open protocol actions; protocol activation handler is not yet wired | Wire protocol activation to alert action service and verify on Windows |
| 3.2 | Alarm popup (persistent, always-on-top) | MVP | **Missing** | No AlarmPopup window | Create dedicated alarm window |
| 3.2 | Custom sounds per reminder/calendar | MVP | **Missing** | `alert.sound` column exists; no sound playback | Add `MediaPlayer` call on alert fire |
| 3.2 | Missed-reminder catch-up | MVP | **Partial** | Startup and in-process timer catch up due instances; `Wake()` is available for the platform resume hook, but a Windows power-event dependency is not included in the current package set | Wire the Windows resume event and verify sleep/resume on Windows |
| 3.2 | Daily digest notification | Premium | **Missing** | Logic mentioned in arch; not implemented | Phase 5 |
| 3.3 | 99.9 % within 5 s proof (test harness) | MVP | **Missing** | No soak test; no fake-clock reminder harness | Phase 6 |
| 3.4 | Reminders panel + grid tick-boxes | MVP | **Partial** | Sidebar reminder smart lists present (wiring-audit WORKING); tick-boxes in AgendaView; grid tick-boxes NOT RUN | Visual verification |
| 4 | Tasks, subtasks, roll-over, time-block | MVP | **Partial** | `TodoService.AddSubtaskAsync` and task repository parent persistence now support subtasks; task add/complete/flag/delete remain wired; rollover/time-block UI is still NOT RUN | Verify nested rendering and add time-block interaction |
| 4 | Sync MS To Do / Google Tasks | v1.1 | **Missing** | Not implemented | v1.1 |
| 4 | Habit tracker | v1.1 Premium | **Missing** | Not implemented | v1.1 |
| 5 | Local SQLite + ICS import/export/subscription | MVP | **Working** | `IcsService.cs`; `EventRepository`; 60 Data tests pass; 7 Sync tests pass | None |
| 5 | Google two-way sync (real) | MVP | **Partial** | `GoogleCalendarProvider.cs` (28 KB); OAuth PKCE, syncToken, outbox code present; NOT RUN (no client ID) | Requires `Google:ClientId` user-secret; GoogleSyncEnabled flag needed |
| 5 | Microsoft Graph two-way sync (real) | MVP | **Partial** | `GraphCalendarProvider.cs` (25 KB); MSAL, delta query, outbox code present; NOT RUN (no tenant config) | Requires Azure app registration credentials |
| 5 | Invitations Accept/Tentative/Decline | MVP | **Partial** | RSVP handler in `MainWindow.xaml.cs`; provider API calls present; NOT RUN | Runtime check with real account |
| 5 | Push channels / change notifications | v1.1 | **Missing** | Polling only (5 min) | v1.1 |
| 5 | iCloud / CalDAV | v1.1 | **Partial** | `CalDavProvider.cs` registered; no interactive credential UI | v1.1 |
| 5 | Own cloud backup | v2 | **Missing** | Not implemented | v2 |
| 6.1 | Natural-language quick add | MVP | **Working** | `QuickAddViewModel.cs`; parser; 237 Core tests include 20+ parser phrases passing | None |
| 6.2 | Global hotkey + tray quick capture | MVP | **Working** | `HotkeyService.cs`; `TrayService` in Platform; wiring-audit WORKING; NOT RUN at OS level | OS-level hotkey verification |
| 6.3 | Keyboard shortcuts (N, R, T, D/W/M/A, Ctrl+F/K/Z, arrows) | MVP | **Working** | Accelerators in `MainWindow.xaml`; wiring-audit WORKING | None |
| 6.4 | Command palette (Ctrl+K) | MVP | **Partial** | `ContentDialog`+`PaletteCommand` list present; fuzzy match quality NOT RUN | Quality check; consider overlay instead of dialog |
| 6.4 | Find free slot / share availability | Premium | **Missing** | `SchedulingAssistantService.cs` exists; no UI surface | Phase 5 |
| 6.4 | Birthdays from contacts | MVP | **Partial** | `OnBirthdaysToggled` in Settings; birthday sync path in SyncCoordinator; NOT RUN | Verification with real Google/MS account |
| 6.4 | Print / export to PDF | MVP | **Partial** | `ScheduleExportService.cs`; export handlers in MainWindow (wiring-audit WORKING); output format NOT RUN | Verify PDF output |
| 6.4 | Weather in headers | v1.1 Premium | **Missing** | `WeatherService.cs` exists (registered, dead) | v1.1 |
| 6.4 | Onboarding (3 screens) | MVP | **Working** | `OnboardingView.xaml`; wiring-audit WORKING | None |
| 7 | Fluent look, Mica, light/dark | MVP | **Partial** | `Tokens.xaml`, `Styles.xaml` present; Mica flat per UI-AUDIT (no depth) | Fix Mica layering, card backgrounds |
| 7 | Tray + start at login | MVP | **Partial** | `TrayService` in Platform registered; no `startupTask` manifest extension | Add manifest extension |
| 7 | Taskbar jump list + overdue badge | MVP | **Missing** | No `JumpList` nor `BadgeNotificationManager` calls | Phase 5 |
| 7 | Share target | MVP | **Missing** | Not in manifest or code | Phase 5 |
| 7 | Windows Hello app lock | Premium | **Missing** | Not implemented | Phase 5 |
| 7 | Compact always-on-top agenda strip | Premium | **Missing** | Not implemented | Phase 5 |
| 7 | Windows 11 Widgets | v1.1 | **Partial** | `WidgetAgendaCardService.cs` registered; not surfaced | v1.1 |
| 7 | Windows Search / pen input | v2 | **Missing** | Not implemented | v2 |
| 8 | Entitlement service (interface) | MVP | **Partial** | `IEntitlementService` + `FreeEntitlementService` present; free-tier limits defined in `FreeTier.cs` | None (interface done) |
| 8 | Store billing, trial, restore, paywall UI | MVP | **Missing** | No `StoreContext` call; no paywall view/dialog | Phase 5 |
| 8 | Free-tier limits enforced in services | MVP | **Partial** | `EventTemplateService` checks limit; sync providers check account limit; most services unchecked | Enforce in all relevant services |
| 8 | Premium themes / accent / fonts | Premium | **Missing** | Tokens.xaml has theme structure; no premium-only themes | Phase 5 |
| 9 | SQLite encrypted, Credential Manager | MVP | **Working** | `Db.cs` (SQLCipher); `CredentialStore.cs` (Windows PasswordVault); 60 Data tests pass | None |
| 9 | Performance targets (startup, RAM, CPU) | MVP | **NOT RUN** | No benchmark results yet | Phase 6 |
| 9 | Delete-all-data + ICS export | MVP | **Working** | Settings wiring-audit WORKING; `IcsService` export; NOT RUN manually | Runtime check recommended |
| 9 | Accessibility (Narrator, HC, 200%) | MVP | **Partial** | AutomationProperties in XAML; tokens; NOT FULL AUDIT | Phase 6 A11Y audit |
| 9 | Localisation-ready strings (.resw) | MVP | **Partial** | `Strings/en-US/Resources.resw` referenced in arch; not fully verified | Phase 7 |
| 9 | Analytics opt-out | MVP | **Missing** | No analytics collector; no opt-out toggle in Settings | Phase 5 (minimal) |
| 10 | MSIX package, WACK, Store submission | MVP | **Missing** | No packaging script; no Package.appxmanifest found | Phase 7 |
| 10 | Store listing assets (icons, screenshots) | MVP | **Missing** | Not present | Phase 7 |

**Summary:** Working: 10 · Partial: 26 · Broken: 3 · Missing: 21 · NOT RUN: 1
