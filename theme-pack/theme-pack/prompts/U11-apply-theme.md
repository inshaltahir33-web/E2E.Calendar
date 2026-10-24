Read docs/11-THEME.md and docs/08-DESIGN-SYSTEM.md. Themes/Tokens.xaml is provided (final; do not change values).
1. Replace Themes/Tokens.xaml with the provided file and merge it first in App.xaml MergedDictionaries.
2. REMOVE any Mica/Acrylic/transparent/gradient/colour wash behind the content area and sidebar. Content area = opaque CardBackground (radius 12 top-left, 1px Divider border); sidebar = opaque SidebarBackground. Mica only behind the title bar if desired.
3. grep all XAML and C# for hard-coded colours (#hex, Colors.*, SolidColorBrush Color=), LightGray etc. and replace with App.* / Cal.* tokens. Report the count before/after.
4. Calendar colours: model stores a palette NAME (Indigo, Blue...) not hex. Event chip uses Cal.{Name}.Bar/Fill/Text per docs/11. Fallback for missing = Slate, never grey default. Migrate existing hex values to nearest palette name.
5. Today column TodayTint + accent circle; weekend WeekendTint; selected sidebar row Selected + 3px accent bar; hover/pressed states from tokens.
6. Calendars list checkboxes must be clearly visible (indigo checked state, not disabled-looking grey). Fix any control that looks disabled.
7. Typography per docs/11. Verify Light, Dark and High Contrast.
Build, run with demo data, tell me which screens to screenshot. Commit "feat(ui): ink indigo theme".
