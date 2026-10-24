# Theme: "Ink Indigo" — premium calendar look

## Premium ka matlab kya hai (Fantastical / Notion Calendar / new Outlook jaisa)
1. **Ek hi confident accent** (indigo), baaki sab neutral. Pastel wash nahi.
2. **Opaque, saaf surfaces**: sidebar thora grey, content card pure white (dark me near-black). Wallpaper ka rang andar nahi aana chahiye.
3. **Hierarchy**: bara date number, chhota caps weekday, secondary text halka, primary text gehra.
4. **Calm density**: patli (1px) lines, zyada white-space, koi boxed/flat mix nahi.
5. **Events = calendar ka saturated bar + halka tint + readable gehra text.**

## Pink/lavender wash kyun aa raha tha
Mica backdrop **user ke wallpaper ka rang** utha leta hai. Aapke wallpaper se gulabi wash aaya. Fix: content card aur sidebar ko opaque token brushes do (`App.CardBackground`, `App.SidebarBackground`). Mica sirf title bar ke peeche optional rakho. Isi liye app ka look har PC par ek jaisa rahega.

## Palette
| Token | Light | Dark |
|---|---|---|
| App.WindowBackground / Sidebar | #F3F4F8 | #0F1115 / #12151B |
| App.CardBackground (content) | #FFFFFF | #181B22 |
| App.CardElevated (flyouts, editor) | #FFFFFF + shadow | #1F232C |
| TextPrimary / Secondary / Tertiary | #14171F / #5B6272 / #8A90A0 | #ECEEF4 / #A5ABBA / #7B8192 |
| Divider / DividerStrong | #E6E8EF / #D3D7E2 | #272B35 / #343947 |
| Accent / Hover / Pressed | #4F5BD5 / #444FC0 / #3A44A8 | #8B95FF / #A1A9FF / #7580F0 |
| AccentSubtle (selected bg) | #EEF0FD | #232748 |
| TodayTint / WeekendTint | #F4F5FD / #FAFAFC | #1C2030 / #161920 |
| NowLine | #E5484D | #FF6369 |
Contrast: accent on white = 5.5:1, TextPrimary on card > 15:1, TextSecondary on card > 5.5:1 (AA).

## Calendar colours (12) — har ek ke 3 tokens: Bar, Fill, Text
Indigo #4F5BD5 · Blue #2B7DE9 · Teal #0E9F9A · Green #2E9E5B · Lime #7FA52A · Amber #D98A00 · Orange #E8590C · Red #E5484D · Pink #D6409F · Purple #8E4EC6 · Slate #5F6B7A · Brown #8B6B4A
Default calendars: Personal=Blue, Work=Indigo, Family=Pink. Naye calendar ko agla unused colour milta hai (grey kabhi nahi: "Grocery run" wala grey fallback bug band).

## Kahan kya use hota hai
| Element | Token |
|---|---|
| Window/title bar | WindowBackground (title bar transparent, ya Mica sirf yahan) |
| Sidebar | SidebarBackground; rows hover=Hover, selected=Selected + 3px Accent bar |
| Content area | CardBackground, radius 12 top-left, 1px Divider border |
| Grid lines | Divider (hour), Divider@50% (half-hour), vertical = Divider |
| Today column | TodayTint; header date = Accent filled circle + OnAccent text |
| Weekend column | WeekendTint |
| Event chip | Fill bg, 3px Bar left, title Text semibold 12, time Text@80% 11 |
| Selected chip | 2px Accent border + shadow |
| Now line | NowLine 2px + 8px dot |
| Primary button | Accent / OnAccent; secondary = transparent + Divider border |
| Editor/Quick add/flyouts | CardElevated, radius 12, ThemeShadow |
| Checkbox/toggle | stock controls (accent override se indigo) |
| Disabled | TextTertiary, never faint-on-faint (Save button ab invisible nahi) |

## Typography
Segoe UI Variable. Weekday caps 11 semibold TextTertiary letter-spacing +4%; date number 20 semibold (today 20 on circle); range title 20 semibold; sidebar rows 14; section headers 11 caps TextTertiary; chip title 12 semibold; chip time 11.

## Do / Don't
- Do: sirf `{ThemeResource App.*Brush}` / `Cal.*`. Don't: hex ya `Colors.X` XAML/C# me.
- Don't: gradients, translucent content, pastel-on-pastel text, boxed cards sirf ek section me.
- Don't: raw hex text "#004E8C" UI me; colour picker = 12 swatches ka grid.
