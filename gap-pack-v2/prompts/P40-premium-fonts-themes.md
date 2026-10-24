[Premium] Spec 8.1 "Themes, accent colours, custom fonts". Prereq: P19 (accent presets only). Do NOT change docs/11 base token values.
1. Feature.Themes / Feature.CustomFonts. Free = Light/Dark only (system accent).
2. Premium themes: 6-8 named themes (e.g. Midnight, Paper, Forest, Rose, High-Contrast-Friendly Blue) implemented as extra ResourceDictionaries that override ONLY semantic tokens; each must pass 4.5:1 contrast checks (automated test over token pairs). Theme picker with live preview cards; crown on locked ones; applying a locked theme opens paywall.
3. Custom font: font family picker (installed fonts via Win2D CanvasTextFormat.GetSystemFontFamilies or DWrite) + size scale 90-130% for the calendar grid and event chips only (system text scaling up to 200% must still work). Preview line. Reset button.
4. Windows High Contrast mode ALWAYS wins over any theme.
5. Persist in settings; apply without restart.
Tests: contrast test for every theme, high-contrast override test, font fallback if font is uninstalled. Commit "feat: premium themes and fonts".
