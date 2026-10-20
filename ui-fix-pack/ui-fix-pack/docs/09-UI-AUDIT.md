# UI Audit — screenshot (Week view, 20 Oct 2026)

## Bugs (seedhe galat)
| # | Problem | Screenshot me kahan | Root cause |
|---|---|---|---|
| 1 | Mini calendar me **Saturday column gayab** (Su..Fr sirf 6) | sidebar | Sidebar width < CalendarView min width, clip ho raha hai |
| 2 | "Mo" aur "We" **do lines me toot** rahe hain | mini calendar header | Narrow cells + default CalendarView styling |
| 3 | **Time labels (00,01,02…) grid ke beech me** (x≈1113) | main grid | Gutter column nahi hai; labels column ke andar draw ho rahe hain |
| 4 | Day headers grid columns se **align nahi** | top of grid | Header aur grid alag layout me, same width source nahi |
| 5 | Random **"N"** badge grid ke beech | x≈937,y≈320 | KeyboardAccelerator tooltip visible; `KeyboardAcceleratorPlacementMode` Hidden nahi |
| 6 | **Date do baar**: toolbar me aur grid ke upar | "19–25 Oct 2026" x2 | Duplicate header |
| 7 | **"Weekends" bada purple primary button** jaisa lagta hai, matlab unclear | grid top-left | Toggle ko Accent button style de diya |
| 8 | Grid **00:00 se shuru**, abhi 19:31 hai, **red now-line nahi** | grid | Auto-scroll to now/working hours + now-line implement nahi |
| 9 | **Aaj (Tue 20) highlighted nahi** | header/grid | Today state missing |
| 10 | Sidebar **cut**: Tasks section adhoora, "Not synced yet" ✓ icon ke saath ajeeb | bottom-left | Sidebar scroll/pin layout nahi, status bar sidebar me ghus gaya |

## Design problems (kyun "vibe-coded" lagta hai)
1. **Koi visual hierarchy nahi**: sab kuch ek hi grey surface pe, na card, na layering, na Mica depth.
2. **Grid lines sirf horizontal**; vertical day separators nahi, isliye columns pehchan me nahi aate.
3. **Title bar generic**: app icon/naam nahi, drag region unclear.
4. **Spacing/typography random**: har section ka alag padding, header sizes mix.
5. **Empty state nahi**: khali screen pe koi hint nahi.
6. **Interaction states nahi**: hover/pressed/selected/focus ke visuals missing.
7. Month/Agenda/Day views aur event chips ka koi **demo data** nahi, isliye UI judge hi nahi ho sakta.

## Spec vs jo dikh raha hai (P04-P06)
| Spec item | Status |
|---|---|
| All-day strip | ❌ |
| Current-time red line | ❌ |
| Today column highlight | ❌ |
| Event chips / overlap layout | ❓ (koi event nahi, verify nahi ho sakta) |
| Drag create/move/resize | ❓ |
| Secondary timezone column | ❌ |
| Sidebar tasks lists | ⚠️ cut |
| Status bar (sync) | ⚠️ sidebar me mix |
| Theme (Mica, rounded content card) | ❌ flat |
| Collapse sidebar < 900px | ❓ |
