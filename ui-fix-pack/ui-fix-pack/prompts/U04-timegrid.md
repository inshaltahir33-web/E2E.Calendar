Rebuild the Day/Week TimeGrid per docs/08 section 5 (fixes audit #3, #4, #8, #9). Reuse OverlapLayout from Core.
Required:
1. ONE Grid with shared column definitions (56 gutter | * x N) for header, all-day strip and scrollable body, so everything aligns.
2. Time labels in the gutter, right-aligned, centred on the hour line, 12/24h per setting.
3. Horizontal hour lines + faint half-hour lines + vertical day separators. Weekend tint. Today column tint + accent circle date in header.
4. Now line (red, 2px, dot) on today, updated every 30 s.
5. Auto-scroll on load to now-90min (today) or 07:00; scroll position kept when switching Day<->Week.
6. All-day strip with multi-day spanning bars and "+N more"; expands up to 3 rows.
7. Event chips per spec (left color bar, tinted fill, title/time, min height 22, overlap columns, hover/selected states, tooltip with full details, context menu: Edit, Duplicate, Delete, Change calendar/color).
8. Drag-create with ghost + live time label, snap 15 min; drag-move; resize from edges; Ctrl+drag copy; Esc cancels drag. 
9. Secondary time zone column (optional, from View options).
10. Empty-range hint pill.
Virtualize: render only visible days/hours. Add a perf test/logging for 10k events.
Build, run with demo data (U00), screenshot. Commit "feat(ui): time grid rebuild".
