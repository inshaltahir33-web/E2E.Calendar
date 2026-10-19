Implement Core/QuickAdd/QuickAddParser per docs/02-ARCHITECTURE.md §6 with an injected IClock.
Output ParsedItem (Event|Reminder, Title, Start, End, Location, Alerts, Rrule, CalendarHint, Priority).
Create a table-driven test with >=25 phrases, e.g.: "Dentist Friday 5pm remind me 1 hour before", "Lunch with Ali tomorrow 1pm at Kohinoor for 1h remind 30 min before", "Call bank at 3pm", "Take medicine every day 9am", "Pay rent every 1st", "in 2 hours stretch", "Gym next Monday 6am #personal !high", "Meeting 10/11 14:30".
Build QuickAddWindow (borderless, centred, live preview card as in docs/03-UI-UX.md) that saves via EventService/ReminderService. Enter=save, Esc=cancel.
Commit "feat: natural language quick add".
