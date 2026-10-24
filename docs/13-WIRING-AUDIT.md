# UI wiring audit

This audit traces the interactive XAML surfaces to their handler/command and
the service boundary they currently use. `WORKING` means the path reaches a
real service/repository operation; `NO-OP` is intentional display/state-only
behavior; `STUB` or `DEAD` identifies work that must not be presented as
finished.

| Control | File | Handler/command | Service/repository | Status |
|---|---|---|---|---|
| Pane toggle | `MainWindow.xaml` | `OnPaneToggleClick` | `MainWindowViewModel.IsPaneOpen` | WORKING |
| Today/previous/next | `MainWindow.xaml` | `OnTodayClick`, `OnPreviousClick`, `OnNextClick` | `CalendarStateViewModel` | WORKING |
| Day/Week/Month/Agenda switcher | `MainWindow.xaml` | `OnViewSwitcherSelectionChanged` | `NavigationService` -> `IShellState` -> view models | WORKING |
| View options: weekends/week numbers/time zone | `MainWindow.xaml` | two-way bindings | `WeekViewModel`, `MonthViewModel`, `SettingsService` | WORKING |
| Search box | `MainWindow.xaml` | `OnSearchTextChanged` | `EventQueryService` / event repository | WORKING |
| Search filters Apply/Clear | `MainWindow.xaml` | `OnApplySearchFiltersClick`, `OnClearSearchFiltersClick` | `EventQueryService`; `IShellState.ActiveFilters` | WORKING |
| Sync | `MainWindow.xaml` | `OnSyncClick` | `SyncCoordinator` and providers | WORKING |
| Export PDF/HTML | `MainWindow.xaml` | export handlers | `ScheduleExportService`, `FileSavePicker` | WORKING |
| Settings | `MainWindow.xaml` | `OnSettingsClick` | `NavigationService` -> `IShellState.ShowSettings` | WORKING |
| New event/reminder/task | `MainWindow.xaml` | menu click handlers | `EventEditorViewModel`, `QuickAddViewModel`, task navigation | WORKING |
| Calendar sidebar rows | `MainWindow.xaml` | `OnSidebarRowClicked` | `CalendarService`, state refresh | WORKING |
| Calendar visibility toggles | `MainWindow.xaml` | two-way item binding | `CalendarService` / calendar repository | WORKING |
| Reminder smart views | `MainWindow.xaml` | `OnSidebarRowClicked` | reminders navigation/state filter | WORKING |
| Task smart views/lists | `MainWindow.xaml` | `OnSidebarRowClicked`, `OnTasksClick` | tasks navigation/state filter | WORKING |
| Invitation Respond | `MainWindow.xaml` | `OnInvitationRespondClick` | `SyncCoordinator`, provider RSVP API | WORKING |
| Agenda completion checkbox | `Views/AgendaView.xaml` | `OnItemCheckBoxClick` | `AgendaViewModel.ToggleCompletedAsync` | WORKING |
| Agenda earlier/later | `Views/AgendaView.xaml` | load handlers | `AgendaViewModel` query service | WORKING |
| Month “more” | `Controls/MonthDayCell.xaml` | `OnMoreClick` | in-memory day items/flyout | WORKING |
| Mini-calendar month paging | `Controls/MiniCalendarView.xaml` | month click handlers | `MiniCalendarViewModel` | WORKING |
| Event editor close/delete/save | `Views/EventEditorView.xaml` | click handlers/commands | `EventEditorViewModel`, `IEventService`, repository/outbox | WORKING |
| Event recurrence weekdays | `Views/EventEditorView.xaml` | two-way properties | `EventEditorViewModel`, recurrence builder | WORKING |
| Event alerts | `Views/EventEditorView.xaml` | add/remove handlers | `EventEditorViewModel`, alert repository | WORKING |
| Quick Add calendar/save/cancel | `Views/QuickAddViewRoot.xaml` | click handlers | `QuickAddViewModel`, `EventService` | WORKING |
| Account Google/Microsoft buttons | `Views/SettingsView.xaml` | generated async commands | `SettingsViewModel`, `SyncCoordinator`, credential store | WORKING |
| Account removal | `Views/SettingsView.xaml` | `OnRemoveAccountClick` | account/calendar/event repositories, secret store | WORKING |
| CalDAV account button | `Views/SettingsView.xaml` | `AddCalDavAccountCommand` | `SettingsViewModel` -> `SyncCoordinator` -> `CalDavProvider` and secret store | WORKING (uses configured CalDAV endpoint/credentials) |
| Calendar create/delete/color | `Views/SettingsView.xaml` | settings handlers | `CalendarService`, calendar repository | WORKING |
| Birthday toggle | `Views/SettingsView.xaml` | `OnBirthdaysToggled` | `SettingsService`, birthday sync | WORKING |
| Appearance/theme controls | `Views/SettingsView.xaml` | settings handlers/bindings | `SettingsService`, `AppearanceService` | WORKING |
| Shortcuts tab | `Views/SettingsView.xaml` | static list | navigation/command palette accelerators | NO-OP (display-only by design) |
| Onboarding skip/next | `Views/OnboardingView.xaml` | click handlers | onboarding settings | WORKING |
| Snackbar action | `Controls/SnackbarView.xaml` | `OnActionClick` | undo/delete service path | WORKING |
| Empty state action | `Controls/EmptyStateView.xaml` | `OnActionClick` | owning view callback | WORKING |
| Keyboard N/R/T/D/W/M/A | `MainWindow.xaml` | accelerator handlers | event/quick-add and `IShellState` navigation | WORKING |
| Keyboard Ctrl+F/K/Z | `MainWindow.xaml` | accelerator handlers | search, command palette, undo | WORKING |
| Keyboard arrows | `MainWindow.xaml` | previous/next handlers | `CalendarStateViewModel` | WORKING |

## Single source of truth

`IShellState` is registered as a singleton and is updated by
`NavigationService` for calendar, todo, search, and settings routes. The
existing WinUI `NavigationService` remains responsible only for materializing
pages; route state is now represented by `IShellState`.

`ISettingsService` exposes the typed persisted settings and observable
properties backed by the existing settings repository. Week and month week
number properties intentionally share the current persisted key; see
[07-KNOWN-GAPS.md](./07-KNOWN-GAPS.md).

## Remaining non-working surface

The CalDAV button uses the configured endpoint/credentials supported by the
provider. An interactive credential-entry form remains separate work; missing
configuration is surfaced by the existing account status/error path.
