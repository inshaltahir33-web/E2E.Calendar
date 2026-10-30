# UI Spec (single source of truth)

## Rules
- Edit the existing Themes\Tokens.xaml. Do NOT create a second tokens file.
- Use only {ThemeResource} tokens in XAML. No hard-coded colors, radii or font sizes in views.
- Change visuals/layout only. Never change business logic, services or ViewModels
  (except tiny UI-state properties needed for visibility).
- Light and Dark both supported.

## Colors (Dark / Light)
- AppBackground      #0E1015 / #F5F6FA
- Surface (cards)    #151821 / #FFFFFF
- SurfaceRaised      #1B1F2A / #F0F1F6
- Border             #262B38 / #E3E5EC
- GridLine           #1E2230 / #ECEDF3
- TextPrimary        #E8EAF0 / #1A1D26
- TextSecondary      #8B91A3 / #6B7080
- Accent             #6366F1 (hover #7477F5, pressed #5558E3)
- AccentSoft (6% accent) for today column / selected rows
- NowLine            #F43F5E
- Danger             #EF4444
- Event colors: pastel fill (calendar color at 18% opacity) + 3px solid left bar in full color, text TextPrimary.

## Shape and spacing
- 8px spacing grid (4, 8, 12, 16, 24, 32).
- Corner radius: cards 12, buttons/inputs 8, chips 6, pills 999.
- Borders 1px Border color. Shadows only on flyouts and the editor drawer.

## Typography (Segoe UI Variable)
- Page/month title 20 semibold; section label 11 semibold uppercase TextSecondary;
  body 13; secondary 12; hour labels 11 TextSecondary.

## Layout
- Top bar height 48. Sidebar width 264 (collapsed 56). Editor drawer 360 on the RIGHT.
- Main content sits in a Surface card with radius 12 and 1px border, 8px margin from edges.
- Hour height 56 default. Week: today column AccentSoft, weekend columns slightly darker.
- Day header: weekday 11 uppercase TextSecondary, date number 20; today = Accent filled circle 32px, white text.
- Month: 1px grid lines, date number 12 top-left, today = Accent filled circle, selected day = 1px Accent border, other-month days at 40% opacity, event chip height 20 radius 4, "+N more" link.
- Now line: 2px NowLine with a 8px dot at the left.

## Components
- Primary button: Accent fill, white text, height 32. Secondary: SurfaceRaised + Border.
- View switcher: segmented pill (Day|Week|Month|Agenda), selected = Accent fill.
- Toggles use the default accent style and are enabled.
- Empty states: small icon + 1 line text + 1 button, centered, never on top of the now line.