# Design System (exact numbers) — isko follow karo, andaza mat lagao

Reference look: **Windows 11 Settings / Clock / Outlook (new) / Fantastical**. Pehle WinUI 3 Gallery app dekho (Microsoft Store pe free) aur controls wahi use karo.

## 1. Surfaces (depth)
- Window backdrop: **Mica** (MicaBackdrop). Title bar + sidebar Mica pe transparent rahenge.
- Main content = **rounded card**: `Background=LayerFillColorDefaultBrush`, `CornerRadius=8,0,0,0` (top-left round), 1px border `CardStrokeColorDefaultBrush`, margin-top 0, margin-left 0.
- Popups/flyouts: `AcrylicBackdrop`, radius 8, shadow (ThemeShadow).
- Sirf `ThemeResource` brushes. Hard-coded hex sirf calendar colors palette me.

## 2. Spacing / size tokens (Tokens.xaml)
| Token | Value |
|---|---|
| Space.XS/S/M/L/XL | 4 / 8 / 12 / 16 / 24 |
| Radius.Control / Card / Chip | 4 / 8 / 6 |
| TitleBar.Height | 48 |
| Sidebar.Width | 272 (collapsed 48) |
| Toolbar.Height | 48 |
| Grid.HourHeight | 56 (user setting 40-96) |
| Grid.GutterWidth | 56 |
| Grid.DayHeaderHeight | 60 |
| AllDay.RowHeight | 24 (strip min 32, expands) |
| Chip.MinHeight | 22 |
Font: Segoe UI Variable. Caption 12 / Body 14 / BodyStrong 14 semibold / Subtitle 20 semibold / Title 28.

## 3. Title bar
`ExtendsContentIntoTitleBar=true`, `SetTitleBar(dragRegion)`. Left: hamburger (pane toggle), app icon 16px, text "Calendar" (Caption, secondary). Center-left: [Today] [‹][›] **range title once** (Subtitle). Center: view switcher (SelectorBar/segmented: Day | Week | Month | Agenda — selected has accent underline pill). Right: search (AutoSuggestBox ~280, Ctrl+F), sync button (icon + tooltip), settings. Leave 138px right padding for caption buttons. **Range title sirf yahin, grid ke upar dubara nahi.**
"Weekends" = ek item inside a "View options ⋯" menu (ToggleMenuFlyoutItem: Show weekends, Show week numbers, Secondary time zone). Primary button kabhi nahi.

## 4. Sidebar (272px, Mica)
Order: Mini calendar → **+ New** (split button) → scrollable area: *Calendars* (Expander), *Reminders* (smart lists with counts), *Tasks* (lists) → **pinned footer**: sync status ("Synced 2 min ago" / "Not signed in — Add account") + Premium 👑 chip.
Rows: height 36, radius 4, hover `SubtleFillColorSecondary`, selected `SubtleFillColorTertiary` + 3px accent bar left. Section headers Caption uppercase secondary, 12px top margin.

### Mini calendar (custom control, NOT stock CalendarView)
`Grid` 7 equal columns, cell 36x36 (total 252 + 10 padding), header letters **single/2-char 12px** (S M T W T F S, week start follows setting), days 13px, today = filled accent circle 28px, selected week = subtle rounded row highlight, days with events = 4px dot under number, other-month days 40% opacity. Header: "October 2026" + ‹ › buttons. Wheel/arrow to change month.

## 5. Time grid (Day/Week)
```
         gutter(56)   Mon        Tue(today)   Wed ...
 header     [tz]     MON         TUE  ◉20     WED
                      19                      21
 all-day    ...      [Birthday▓▓▓▓]
 07:00  ──────────── ───────────────────────────
```
- Layout: ONE `Grid`; columns: `56 | * x N`. Header, all-day strip, scrollable body **share the same column definitions** (so alignment guaranteed). Body = ScrollViewer with Canvas height = 24*HourHeight.
- Time labels: in gutter, right-aligned (padding-right 8), Caption secondary, vertically centered **on the hour line** (offset -8). Skip label "00:00" at top, format by 12/24h setting.
- Lines: horizontal 1px `DividerStrokeColorDefaultBrush` per hour; faint 50% line per half-hour (dotted/lower opacity); **vertical 1px line between days**.
- Day header: weekday Caption uppercase secondary, date Subtitle; **today = date inside accent filled circle 32px + column tint accent @6%**; weekend columns tint 3% neutral.
- **Now line**: 2px `SystemFillColorCriticalBrush`, 8px dot at gutter edge, only on today column, updates every 30s.
- **Initial scroll**: day = today → scroll to `now - 90min`, else 07:00. Remember per session.
- Events: radius 6, **left 3px bar in calendar color**, fill = calendar color @ 18% (dark: 28%), title 12 semibold primary, time 11 secondary, padding 6/4, min height 22, 1px gap between overlapping neighbours, hover raise shadow, selected = 2px accent border. Short (<30min) events single line. Overlap = column packing (side by side), max 4 then "+N".
- Reminder items: small checkbox + title on grid at time (no fill).
- Drag-create: ghost chip with live time label "10:00 – 11:00". Snap 15 min (Shift = 5).
- Empty: no overlay on grid (keep clean) but if whole visible range empty, show subtle centered hint pill: "Nothing scheduled. Double-click or press N."

## 6. Month view
7 cols equal, row height fills; day number top-left (today = accent circle), chips 20px high max 3 + "+N more", other-month 40%, hover cell subtle fill, click -> selects, double-click -> new event. Week numbers optional.

## 7. Agenda
Sticky day headers ("Today · Tue 20 Oct", Caption + Subtitle), rows 48px: time column 72px | colour dot | title + location | calendar name. Reminders with checkbox. Empty state with illustration-free text.

## 8. States & motion (sab controls pe)
Rest / Hover / Pressed / Selected / Focus (2px `FocusStrokeColorOuter`) / Disabled. Transitions 120-167ms (`ControlFastOutSlowInKeySpline`). Respect reduced animations. View switch = cross-fade 150ms; period change = slide 8px + fade.

## 9. Keyboard hints
`KeyboardAcceleratorPlacementMode="Hidden"` har accelerator pe (isse stray "N" badge jata hai). Shortcuts tooltips sirf buttons ke Tooltip me.

## 10. Accessibility
Contrast >=4.5:1, high-contrast brushes (SystemColor*), AutomationProperties.Name/ItemStatus on chips ("Standup, 10 to 10:30, Work calendar"), text scale 200% pe layout na toote, focus order logical.
