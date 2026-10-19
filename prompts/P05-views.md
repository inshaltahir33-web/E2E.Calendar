Implement calendar views per docs/03-UI-UX.md using EventQueryService:
1. Month view: 6x7 grid, event chips (max 3) + "+N more" flyout, heat dots, click day->Day view.
2. TimeGrid custom control (Canvas based, virtualised) used by Day and Week (5-day toggle): hour grid, all-day strip, current time red line (updates each minute), overlap layout algorithm (column packing), drag to create, drag to move, edge to resize, Ctrl+drag copy.
3. Agenda view: ItemsRepeater grouped by day with sticky headers; events, reminders and tasks.
Reminders/tasks with time show as small tick-box items.
Keep 60fps with 10k events: only render visible range; no per-event heavy controls.
Add VM unit tests for layout algorithm in Core.Tests (pure function in Core: OverlapLayout.Pack()).
Commit "feat(app): month week day agenda".
