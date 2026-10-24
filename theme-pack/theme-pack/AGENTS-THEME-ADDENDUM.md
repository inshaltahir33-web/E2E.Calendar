## Theme rules (append to AGENTS.md)
- Brand theme = Ink Indigo, defined ONLY in Themes/Tokens.xaml (docs/11-THEME.md). Never edit values without being asked.
- No hex, Colors.*, or new brushes in XAML/C#. Use {ThemeResource App.*Brush} or Cal.{Name}.{Bar|Fill|Text}Brush.
- Content and sidebar are opaque. No Mica/gradients behind content (Mica takes wallpaper colour and breaks the look).
- Every UI task: verify Light + Dark + High Contrast and report which screenshots to take.
