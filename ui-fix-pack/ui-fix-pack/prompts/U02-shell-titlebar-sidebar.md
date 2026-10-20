Rebuild the shell per docs/08-DESIGN-SYSTEM.md sections 1, 3, 4. Fix audit items #5, #6, #7, #10 in docs/09-UI-AUDIT.md.
- ExtendsContentIntoTitleBar + SetTitleBar with correct drag region and right padding for caption buttons; left: hamburger, app icon, "Calendar".
- Toolbar: Today, prev/next, range title ONCE, view switcher (SelectorBar) Day/Week/Month/Agenda, search AutoSuggestBox, sync button with tooltip, settings. Remove the duplicate date title from the grid area. Replace the purple "Weekends" button with a "View options" (⋯) menu containing ToggleMenuFlyoutItems: Show weekends, Show week numbers, Secondary time zone.
- Content area is a rounded card on Mica (section 1). Sidebar width 272, collapsed 48, auto-collapse below 900px with smooth 167ms animation.
- Sidebar: scrollable middle area (Calendars Expander, Reminders smart lists with count badges, Tasks lists), PINNED footer with sync status ("Synced 2 min ago" / "Add account") and a subtle Premium chip. Selected row style with 3px accent bar.
Build and run. Commit "feat(ui): shell, titlebar, sidebar".
