The New event pane looks unfinished (see screenshots). Rebuild it per docs/11-THEME.md (CardElevated, radius 12, shadow) and docs/08:
- Width 400px, right side; header "New event" with close; NO "Unsaved changes" label until the user actually edits something.
- Title: large borderless input (18px) with placeholder "Add title". Templates (Focus block, Quick standup) as chips under the title, not a floating dropdown over it.
- When: two rows with DatePicker + real TimePicker (12/24h by setting) side by side — never bare numbers "3 | 00"; dates must not truncate (format "Fri, 23 Oct"); "All day" toggle with label fully visible; time zone as a quiet link-style row ("Asia/Karachi") that expands.
- Repeat: ComboBox + readable summary ("Every week on Mon, Wed · until 30 Nov"), opens custom dialog for complex rules.
- Alerts: chips ("5 min before ✕") + "Add alert" button, not duplicated text and combobox.
- Where: one location field with map icon button. Meeting link: ONE field "Add Teams/Zoom/Meet link" (remove the duplicated "Link (https://)" field), Join button when valid.
- Calendar: ComboBox with colour dot; colour = 12 swatch grid from palette names (no hex text, no tiny arrows).
- Footer pinned: Delete (danger text button, left); Cancel (secondary) and Save (primary Accent) right. Save must look enabled when valid, disabled state must still be readable (TextTertiary on Divider, not invisible). Ctrl+Enter saves, Esc closes with unsaved prompt only if dirty.
- Scroll only the body; footer always visible. Test at 900px window and 150% scale.
Commit "feat(ui): event editor rebuild".
