(Attach the latest screenshots.) Fix these defects in order, one commit each, add tests where logic is involved:
1. MONTH VIEW shows a "Coming soon" placeholder. Implement it fully per docs/08 section 6 and docs/11 (chips with Cal tokens, +N more flyout, today accent circle, hover, double-click new event, drag between days).
2. "Daily standup" appears twice at 09:30 (Agenda, Day, Week). Find root cause (duplicate seed vs recurrence expansion returning master + occurrence) and fix with a regression test in Core.Tests.
3. In Day view the toolbar title still says "19 – 25 Oct 2026". Title must follow the view: Day "Fri 23 Oct 2026", Week range, Month "October 2026", Agenda "Upcoming". Mini calendar month and selected date must stay in sync with the main view; changing the mini calendar must not desync the title.
4. Mini calendar weekday letters misaligned/clipped ("M" cut off): header and day cells must share the same 7 column definitions.
5. Overlapping events: Day view shows two events as huge side-by-side blocks. Max chip width = 100%/columns but never wider than 360px in Day view; truncated chips need tooltip. Week: min chip text 12px, "Quick sync" and "Daily standup" must not overlap unreadably (use column packing + "+N").
6. Agenda: restrict content width to 720px centred, sticky day headers with count, reminders and events share one row layout (time | marker | title | calendar tag right-aligned), completed reminders strike-through + TextTertiary, remove odd times like 00:19 from seeder (use realistic times).
7. Sidebar: Tasks section shows its lists; pinned footer ("Not synced yet" left, Premium chip right) never clipped, 12px padding, 1px top divider. Title bar: 12px gap between app label and Today.
Build, run, commit each fix.
