Follow AGENTS.md. In E2E.Calendar.Core create models and abstractions from docs/02-ARCHITECTURE.md §3 and §1:
CalendarEvent, Calendar, Account, Alert, AlertInstance, Reminder, TaskItem, TaskList, Occurrence, enums (Priority, AlertState, Provider).
Abstractions: IClock (UtcNow, LocalTimeZone), IEventRepository, IReminderRepository, ITaskRepository, IAlertRepository, INotifier, ISecretStore, IEntitlementService.
Use records/immutable where sensible, UTC DateTimeOffset, tz id strings.
Add EventService (create/update/delete soft/undo/duplicate) that writes dirty flag + outbox entry via an IOutbox interface.
Write unit tests with NSubstitute. Build + test. Commit "feat(core): models and services".
