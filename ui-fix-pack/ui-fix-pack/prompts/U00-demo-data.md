Read docs/08-DESIGN-SYSTEM.md and docs/09-UI-AUDIT.md.
Add a DEBUG-only DemoDataSeeder (Core abstraction + App/Debug implementation, enabled by a "Load demo data" button in Settings > Developer and the env var E2E_DEMO=1). Never runs in Release.
Seed for the current week and neighbouring weeks: ~40 events across Personal/Work/Family calendars including: overlapping events (3 at same time), 15-min event, 8-hour event, all-day events, multi-day event, recurring daily standup, weekly gym, monthly rent reminder, birthday all-day, events at 00:30 and 23:00, event with meeting link, 6 reminders (some overdue), 8 tasks. Use relative dates from IClock so it always looks fresh. Add "Clear demo data".
Build, run, commit "chore: demo data seeder".
