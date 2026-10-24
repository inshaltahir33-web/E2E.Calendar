[v1.1 · Premium] Spec 2.1 + 8.1. Prereq: P05 (views), P19 (Feature enum).
1. Add Feature.YearView and Feature.MultiDayView to the Feature enum + limits table (free = No). Enforce in the ViewService (PremiumRequired result), show crown badge on the toolbar items, open paywall on tap. Never hide the items.
2. YearView: 12 mini-months in a responsive grid (3x4 / 4x3 / 2x6 by width). Days with events get a dot, today is accent-filled, click a day -> jumps to Day view, click month header -> Month view. Data comes from ONE grouped COUNT query per year (not 365 queries). Keyboard: arrows move day focus, Enter opens. Shortcut: Y.
3. MultiDayView: user picks 2-14 days (NumberBox in toolbar flyout, remembered in settings). Reuse the Week grid control with a dayCount parameter - do NOT fork the code. Drag-to-create, resize and all-day strip must work. Shortcut: X.
4. Prev/Next arrows move by the chosen range length.
5. Follow docs/08 + docs/11 (tokens only, no hard-coded colours). Narrator labels: "Year 2026, March, 12 events".
Tests: gating matrix, year query perf with 10k events (< 50 ms), multi-day count 2/7/14 layout test. Commit "feat: year and multi-day views".
