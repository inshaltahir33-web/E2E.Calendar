Follow docs/03-UI-UX.md. Build App shell in WinUI 3:
- Custom title bar (Mica) with: Today/prev/next, period label, view switcher (Day|Week|Month|Agenda), search box, sync icon, settings.
- Left sidebar: mini CalendarView, "+ New" split button, calendars list with color + visibility checkboxes, Reminders smart lists, Task lists. Collapses to icons under 900px.
- Status bar. NavigationService + CalendarStateViewModel (current date, view mode) as singleton.
- Keyboard accelerators: N, R, T, D, W, M, A, Ctrl+F, Ctrl+K, arrows, Ctrl+Z.
- Light/Dark/High contrast via ThemeResource only. AutomationProperties.Name everywhere.
- Seed a default local calendars Personal/Work on first run.
Build. Commit "feat(app): shell and navigation".
