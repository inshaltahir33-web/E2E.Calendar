Add polish across the app per docs/08 section 8 and 10:
- Hover/pressed/selected/focus visuals on every interactive element; focus rings visible for keyboard.
- Transitions: view switch cross-fade 150 ms, period change slide 8px + fade, sidebar collapse, flyout open (use built-in Fluent transitions; honour system "animation effects" setting).
- Empty states (calendar, reminders list, tasks, search no results) with short text + primary action.
- Loading skeletons while range query runs (>150 ms only).
- Tooltips with shortcuts on toolbar buttons. Snackbar/InfoBar for undo and errors.
- Context menus (right click) on grid slot (New event here, New reminder here), event chip, day cell, sidebar calendar (Color, Show only this, Settings).
- Check Dark and High Contrast themes and text scaling 200%; fix any clipping.
Commit "feat(ui): states, motion, empty, context menus".
