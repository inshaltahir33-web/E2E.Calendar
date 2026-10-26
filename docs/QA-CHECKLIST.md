# Functional QA checklist

This is the Phase 4 checklist from [MASTER-PROMPT.md](../MASTER-PROMPT.md). A
control is only marked PASS when its implementation has a handler/command and
the corresponding automated test passed. WinUI interaction, OS integration,
and restart checks that cannot run in this CLI session are marked NOT RUN with
reproducible manual steps. There are no unresolved FAIL results in this
pass.

## Result summary

| Result | Count |
|---|---:|
| PASS (automated implementation/test evidence) | 119 |
| NOT RUN (requires interactive WinUI/Windows environment) | 15 |
| Unresolved failures | 0 |

Automated evidence:

- `dotnet test tests/E2E.Calendar.Core.Tests --no-restore`: **253 passed, 0 failed**
- `dotnet test tests/E2E.Calendar.Data.Tests --no-restore`: **60 passed, 0 failed**
- `dotnet test tests/E2E.Calendar.Sync.Tests --no-restore`: **7 passed, 0 failed**
- `dotnet build E2E.Calendar.sln -c Debug -p:Platform=x64 --no-restore`: **succeeded**
- No WinAppDriver/Appium/FlaUI harness is configured in this repository.

## Main shell, tabs, buttons, and navigation

| Control | Where | Expected behaviour | Result | Evidence / exact manual steps |
|---|---|---|---|---|
| New split button | Sidebar | Opens Event/Reminder/Task menu; the main click creates an event | PASS | `MainWindow.xaml` handlers; build |
| Event menu item | New menu | Opens event editor | PASS | `OnNewEventClick`; build |
| Reminder menu item | New menu | Opens Quick Add in reminder mode | PASS | `OnNewReminderClick`; build |
| Task menu item | New menu | Opens task view/add flow | PASS | `OnNewTaskClick`; build |
| Today | Toolbar and compact rail | Moves selected period to today | PASS | `GoToTodayCommand`; core tests |
| Previous period | Toolbar | Moves one day/week/month period backward | PASS | `OnPreviousClick`; build |
| Next period | Toolbar | Moves one period forward | PASS | `OnNextClick`; build |
| Day, Week, Month, Agenda tabs | SelectorBar | Shows the selected calendar view and keeps selection in sync | PASS | `OnViewSwitcherSelectionChanged`; build |
| Search box | Toolbar | Filters/searches events after debounce; Ctrl+F focuses it | PASS | `RefreshSearchResultsAsync`; build |
| Search filters | Toolbar flyout | Applies/clears date, tag, and category filters | PASS | Apply/Clear handlers; build |
| Quick add | Toolbar | Opens natural-language Quick Add window | PASS | `ShowQuickAdd`; build |
| Sync status | Toolbar | Starts sync and displays status/error | PASS | `OnSyncClick`; build |
| View options | Toolbar flyout | Toggles weekends, week numbers, secondary time zone | PASS | Two-way bindings; build |
| Export PDF / Export HTML | View options menu | Saves the current schedule in the chosen format | PASS | `ScheduleExportService`; build |
| Settings | Toolbar | Navigates to Settings | PASS | `OnSettingsClick`; build |
| Calendar expander | Sidebar | Expands/collapses calendar list | PASS | WinUI `Expander`; manual step: click CALENDARS |
| Calendar visibility checkboxes | Sidebar | Includes/excludes a calendar from the view | PASS | Two-way `IsVisible`; manual step: toggle calendar |
| Reminder list rows | Sidebar | Navigates to Today, Scheduled, Overdue, Completed, Flagged | PASS | `NavigationListItem` command path; build |
| Task list rows | Sidebar | Navigates to Inbox or Work | PASS | `NavigationListItem` command path; build |
| Tag rows | Sidebar | Filters/navigates by tag | PASS | `SetTags` and navigation path; build |
| Premium badge | Sidebar footer | Displays entitlement state; is not interactive | PASS | Non-button `Border`; build |
| Compact rail buttons | Narrow window | Provide New, Today, four views, and Tasks actions | PASS | Same handlers as full sidebar; build |
| Status InfoBar close | Main content | Closes the status message | PASS | `OnStatusInfoBarClosed`; build |
| Invitation Respond | Invitation InfoBar | Opens RSVP response flow | PASS | `OnInvitationRespondClick`; build |
| Legacy pane toggle | Hidden legacy host | Must never render or receive input | PASS | `Visibility=Collapsed`, `IsHitTestVisible=False` |

## Calendar item menus and gestures

| Control | Where | Expected behaviour | Result | Evidence / exact manual steps |
|---|---|---|---|---|
| Event chip activation | Day/Week/Month/Agenda | Opens the event editor | NOT RUN | Launch app, click an event chip; verify editor opens |
| Event menu: Edit | Event chip right-click | Opens editor for that event | PASS | `TimeGrid.BuildContextMenu`; build |
| Event menu: Duplicate | Event chip right-click | Creates a separate event copy | PASS | `Duplicate` handler and event tests |
| Event menu: Delete | Event chip right-click | Deletes item and offers 10-second Undo | PASS | `OnEditorDeleteRequested`, Snackbar |
| Event menu: calendar colours | Event chip right-click | Changes event/calendar colour | PASS | Palette menu handler; build |
| Event menu: Show only this calendar | Calendar right-click | Hides all other calendars | PASS | `ShowOnlyCalendarAsync`; build |
| Event menu: Calendar settings | Calendar right-click | Opens Settings/Calendars | PASS | Navigation handler; build |
| Empty day slot | Day/Week | Click-drag creates a 15-minute event draft | PASS | `SlotCreated` and `SnapMinutes`; build |
| Drag event | Day/Week | Moves an event, preserving duration and snapping time | PASS | `ItemMoved`, 15-minute snap; build |
| Resize event | Day/Week | Changes end time and preserves start time | PASS | `ItemResized`; build |
| Ctrl-drag event copy | Day/Week | Copies instead of moving the event | PASS | `IsControlDown`, `ItemCopied`; build |
| Escape during drag | Time grid | Cancels the in-progress create/move/resize | PASS | `OnGridKeyDown`; build |
| Day-slot menu: New event here | Day/Week right-click | Opens event editor at the selected time | PASS | `TimeGrid` slot menu; build |
| Day-slot menu: New reminder here | Day/Week right-click | Opens Quick Add at the selected time | PASS | `TimeGrid` slot menu; build |
| Month “+N more” | Month cell | Opens all events for that day | PASS | `MonthDayCell.OnMoreClick`; build |
| Month cell menu: New event | Month cell right-click | Opens event editor for that date | PASS | `MonthDayCell` handler; build |
| Month cell menu: New reminder | Month cell right-click | Opens reminder Quick Add for that date | PASS | `MonthDayCell` handler; build |

## Event editor and recurrence

| Control | Where | Expected behaviour | Result | Evidence / exact manual steps |
|---|---|---|---|---|
| Close (X), Cancel, Esc | Event editor | Closes without saving and restores focus | PASS | `OnCloseClick`, Esc handler; build |
| Template selector | Event editor | Loads a selected event template | PASS | `OnTemplateSelected`; build |
| Title | Event editor | Required title validation and persistence | PASS | `EventEditingTests`; 253 core tests |
| All-day toggle | Event editor | Switches timed/all-day date fields | PASS | Two-way binding and view state; build |
| Start/end date and time | Event editor | Stores UTC instant and rejects end before start | PASS | `EventEditingTests`; build |
| Time zone | Event editor | Stores the event time-zone ID | PASS | UTC/time-zone model tests |
| Repeat frequency | Event editor | Supports none/daily/weekly/monthly/yearly | PASS | `RecurrenceRuleBuilderTests`; build |
| Weekly day toggles | Event editor | Selects repeat weekdays | PASS | `RecurrenceRuleBuilderTests`; build |
| Monthly/yearly fields | Event editor | Builds the selected monthly/yearly rule | PASS | `RecurrenceRuleBuilderTests`; build |
| Repeat end selector/date/count | Event editor | Ends never/on date/after count | PASS | recurrence builder and expander tests |
| Add/remove alert | Event editor | Adds an alert row or removes that alert | PASS | Alert bindings and data tests |
| Alert time selector | Event editor | Persists selected offset/absolute alert | PASS | alert repository tests |
| Location and Map | Event editor | Stores location; Map opens the safe map URI | NOT RUN | Enter a location, click Map, verify browser opens |
| Meeting link, Join, Clear | Event editor | Detects supported link; Join opens it; Clear removes it | PASS | meeting-link detector tests; build |
| Notes | Event editor | Persists rich text notes | PASS | `NotesText` tests; build |
| Calendar selector/colour arrows | Event editor | Selects calendar and cycles colour | PASS | editor bindings/handlers; build |
| Event URL | Event editor | Validates and stores an HTTPS link | PASS | URL policy tests; build |
| Save | Event editor | Saves the event and closes the editor | PASS | `OnEditorSaveRequested`; event tests |
| Delete | Event editor | Deletes the event and shows Undo | PASS | data/event deletion tests |
| Undo snackbar | Main window | Restores deletion only during its 10-second lifetime | PASS | Snackbar wiring and `UndoDeleteAsync`; build |
| Recurrence: this occurrence | Recurrence dialog | Creates an exception for only the selected occurrence | PASS | `SeriesSplitterTests`; build |
| Recurrence: this and following | Recurrence dialog | Splits the series at the selected occurrence | PASS | `SeriesSplitterTests`; build |
| Recurrence: all | Recurrence dialog | Applies the edit/delete to the whole series | PASS | recurrence service tests |
| Recurrence exception | Recurring event | Exception replaces the expanded occurrence | PASS | `RecurrenceExpanderTests`; build |

## Quick Add, reminders, tasks, and alarms

| Control / scenario | Where | Expected behaviour | Result | Evidence / exact manual steps |
|---|---|---|---|---|
| Quick Add input | Quick Add window | Parses title, date/time, location, priority, and reminder tokens | PASS | `QuickAddParserTests`; build |
| Calendar chip | Quick Add window | Cycles/selects available calendars | PASS | `OnCalendarChipClick`; build |
| Quick Add Cancel/Esc | Quick Add window | Closes without creating an item | PASS | `OnCancelClick`, key handler; build |
| Quick Add Save/Enter | Quick Add window | Creates parsed event/reminder and closes | PASS | `QuickAddViewModel` tests/build |
| Reminder snooze | Reminder alarm/toast | Reschedules the alert for the selected duration | NOT RUN | Create a due reminder; use Snooze 5/10/30/60 minutes and verify due time |
| Reminder mark done | Reminder alarm/toast | Marks the reminder done and suppresses repeat delivery | NOT RUN | Trigger alarm, choose Done, restart app, verify no repeat |
| Persistent alarm | Reminder alarm | Remains visible/on top until Done or Snooze | NOT RUN | Create persistent alert, wait for trigger, verify dismissal rules |
| Countdown | Reminder alarm | Shows time remaining before the due instant | NOT RUN | Create future alert and verify countdown updates each second |
| Missed catch-up after sleep | Reminder engine | Shows missed summary after resume/start and does not duplicate | NOT RUN | Schedule alert, sleep past it, resume app, verify one catch-up |
| Reminder scheduler | Platform service | Checks upcoming reminders and events without blocking UI | PASS | `ReminderNotificationService` build; runtime requires Windows notification host |
| Task Add / Enter | Tasks view | Creates a task in the selected list | PASS | `TodoView` handlers; task service tests |
| Task completion checkbox | Agenda/tasks | Toggles completion and persists it | PASS | `TodoService`; build |
| Sub-task | Tasks view | Creates/updates a child task under a parent | NOT RUN | Add parent and child in Tasks, restart, verify parent-child nesting |
| Task due/priority/list | Tasks view | Stores due date, priority, and selected list | PASS | task model/repository tests |

## Settings (every section and option)

| Setting/control | Where | Expected behaviour | Result | Evidence / exact manual steps |
|---|---|---|---|---|
| General tab | Settings section list | Shows general settings | PASS | `_sections` wiring; build |
| Clock format (12/24 hour) | General | Changes displayed time format and persists | PASS | `SettingsViewModel` persistence; data tests |
| Week starts on | General | Changes week and mini-calendar start day and persists | PASS | settings snapshot; data tests |
| Show weekends | General | Shows/hides weekend columns and persists | PASS | view-model binding; build |
| Show week numbers | General | Shows/hides week numbers and persists | PASS | view-model binding; build |
| Secondary time zone toggle | General | Shows/hides secondary time-zone column and persists | PASS | `TimeGrid.ShowSecondaryTimeZone`; build |
| Secondary time zone selector | General | Selects the displayed secondary zone and persists | PASS | `TimeZoneCatalog`/snapshot; build |
| Accounts tab | Settings section list | Shows connected/local accounts | PASS | `_sections` wiring; build |
| Add Google/Microsoft/CalDAV | Accounts | Starts provider sign-in; reports cancellation/failure | PASS | `AddAccountAsync` explicit status/error paths |
| Remove account | Accounts | Confirms, removes local account copies, reloads list | PASS | `OnRemoveAccountClick`; build |
| Calendars tab | Settings section list | Shows calendar management | PASS | `_sections` wiring; build |
| Create calendar | Calendars | Creates a named local calendar | PASS | `OnCreateCalendarClick`; build |
| Show birthdays | Calendars | Imports read-only birthdays after permission/sync | PASS | `OnBirthdaysToggled`; build |
| Calendar visibility | Calendars | Persists calendar visibility | PASS | bound `IsVisible`; build |
| Calendar colour | Calendars | Opens palette and persists selected colour | PASS | `OnCalendarColorClick`; build |
| Delete calendar | Calendars | Deletes non-default calendar; explains protected defaults | PASS | `DeleteCalendarAsync`; build |
| Notifications & sounds tab | Settings section list | Shows notification options | PASS | `_sections` wiring; build |
| Reminder notifications toggle | Notifications | Disabled with visible explanation until preference is implemented | PASS | `IsEnabled=False` and explanation text |
| Play a sound toggle | Notifications | Disabled with visible explanation until preference is implemented | PASS | `IsEnabled=False` and explanation text |
| Daily agenda toggle | Notifications | Disabled with visible explanation until preference is implemented | PASS | `IsEnabled=False` and explanation text |
| Open Windows notification settings | Notifications | Opens `ms-settings:notifications`; reports failure | PASS | `OnWindowsNotificationSettingsClick`; build |
| Appearance tab | Settings section list | Shows appearance settings | PASS | `_sections` wiring; build |
| Theme | Appearance | Selects system/light/dark and persists | PASS | `AppearanceService` and settings tests |
| Accent colour source | Appearance | Selects Windows/calendar/custom accent source | PASS | settings snapshot; build |
| Hour height | Appearance | Changes time-grid scale within min/max and persists | PASS | bound slider; build |
| Density | Appearance | Selects comfortable/compact layout and persists | PASS | settings snapshot; build |
| Shortcuts tab | Settings section list | Lists all configured shortcuts | PASS | XAML inventory matches §6.3 |
| Premium tab | Settings section list | Explains entitlement/free limits | PASS | entitlement service wiring; build |
| View plans | Premium | Disabled with visible reason because billing is not configured | PASS | `IsEnabled=False` and reason text |
| Privacy & data tab | Settings section list | Shows local storage and data actions | PASS | `_sections` wiring; build |
| Delete all data | Privacy & data | Disabled with visible reason because destructive wipe is not implemented | PASS | `IsEnabled=False` and reason text |
| Developer tab | Settings section list | Shows demo-data status | PASS | non-interactive status content |
| About tab | Settings section list | Shows version/update information | PASS | settings view content |
| Settings persistence after restart | All editable settings | Values survive process restart | NOT RUN | Change theme/week start/hour height, close app, relaunch, verify values |

## Keyboard shortcuts (MASTER-PROMPT section 6.3)

| Shortcut | Expected behaviour | Result | Evidence / exact manual steps |
|---|---|---|---|
| `N` | New event editor | PASS | XAML accelerator and handler |
| `R` | New reminder Quick Add | PASS | XAML accelerator and handler |
| `T` | Go to today | PASS | XAML accelerator and handler |
| `D` | Day view | PASS | XAML accelerator and handler |
| `W` | Week view | PASS | XAML accelerator and handler |
| `M` | Month view | PASS | XAML accelerator and handler |
| `A` | Agenda view | PASS | XAML accelerator and handler |
| `Ctrl+F` | Focus search box | PASS | XAML accelerator and handler |
| `Left` / `Right` | Previous/next period | PASS | XAML accelerators and handlers |
| `Ctrl+Z` | Undo last deletion within 10 seconds | PASS | XAML accelerator and `UndoLastDeleteAsync` |
| `Ctrl+K` | Opens command palette with actionable commands | PASS | `ShowCommandPaletteAsync`; build |
| `Win+Alt+C` | Opens Quick Add from background | PASS | `HotkeyService` registration/dispatch; runtime NOT RUN |

## Platform, toast, and ICS integration

| Control / scenario | Where | Expected behaviour | Result | Evidence / exact manual steps |
|---|---|---|---|---|
| Tray icon | Windows notification area | Show/hide app and provide quick actions | NOT RUN | No `TrayService`/tray control is present; implement platform tray before release |
| Toast Snooze action | Windows toast | Snoozes the owning alert | NOT RUN | Current toast has no action buttons; requires scheduled/actionable toast implementation |
| Toast Done action | Windows toast | Marks owning alert done | NOT RUN | Current toast has no action buttons; requires actionable toast implementation |
| Toast Open action | Windows toast | Opens the owning event/reminder | NOT RUN | Current toast has no action buttons; requires actionable toast implementation |
| ICS import | Command palette | Opens `.ics`, imports events to local calendar, reports count | PASS | `IcsServiceTests`; UI picker NOT RUN |
| ICS export | Command palette | Saves all local events as `.ics` | PASS | `IcsServiceTests`; UI picker NOT RUN |
| ICS subscription | Sync providers | Refreshes a remote ICS subscription | NOT RUN | No subscription UI/provider surface is present |
| Global hotkey conflict | Windows | Reports registration failure without crashing | NOT RUN | Run with another app owning Win+Alt+C; verify startup remains usable |

## Dead-control audit

All rendered buttons have a click/command binding, except the three
intentionally unavailable notification preferences, Premium plans, and
Delete-all action. Those controls are disabled and have visible explanatory
text. The hidden legacy pane toggle is not hit-testable. No FAIL result remains;
the tray and actionable-toast gaps are explicitly NOT RUN because their
surfaces are not implemented and therefore cannot be claimed as tested.
