Implement the reminder system EXACTLY as docs/02-ARCHITECTURE.md §5.
Core: ReminderPlanner (builds alert_instance for 14-day horizon, idempotent), ReminderEngine (min-heap/next-pending loop with wake signal), SnoozeService, catch-up logic, persistent repeat, daily digest instance.
Platform: ToastNotifier using Windows App SDK AppNotificationManager + ScheduledToastNotification for the next 200 pending instances (tag = instance id, dedupe), buttons Snooze(5/10/30/60/tomorrow/custom)/Done/Open/Join, activation handler parsing args. PowerEvents hook (resume/clock/timezone change) -> planner + catch-up. Alarm popup window (always-on-top, sound) for Critical/persistent. Respect Focus Assist unless Critical allowed.
Add StartupTask manifest extension and "start minimised to tray".
Tests (Core.Tests with fake IClock): planner offsets, recurring, all-day day-before alert, snooze, missed after 6 minutes, persistent repeat until done, no double fire. Commit "feat: reminder engine".
