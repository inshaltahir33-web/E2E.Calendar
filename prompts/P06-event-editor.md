Build the Event editor flyout (right pane) per docs/03-UI-UX.md: title, date/time pickers with all-day toggle, timezone, repeat editor (maps to RRULE incl. End: never/date/after N), multiple alerts, location with map link (https://www.bing.com/maps?q=), meeting link auto-detect (Teams/Zoom/Meet regex in Core) with Join button, notes, calendar + color, URL.
Recurrence edit dialog: "This event / This and following / All events".
Delete -> snackbar with Undo for 10 seconds (Ctrl+Z).
Event templates (2 free). Validation: end >= start, URL scheme http/https only.
Unit-test meeting-link detection and RRULE mapping. Commit "feat: event editor".
