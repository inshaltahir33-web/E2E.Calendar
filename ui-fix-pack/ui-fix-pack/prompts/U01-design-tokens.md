Implement docs/08-DESIGN-SYSTEM.md sections 1, 2, 8, 9.
Create App/Themes/Tokens.xaml (spacing, radii, sizes, font sizes, calendar color palette with Light/Dark variants via ThemeDictionaries) and Styles.xaml (SidebarRowStyle, SectionHeaderStyle, ToolbarButtonStyle, ChipStyle, CardStyle, EventChipStyle, SubtleHoverStyle with VisualStateManager states Rest/Hover/Pressed/Selected/Focus/Disabled).
Merge into App.xaml. Then grep all XAML for hard-coded colors, magic numbers for margins/padding/sizes and font sizes and replace them with tokens/ThemeResources. Add KeyboardAcceleratorPlacementMode="Hidden" to every KeyboardAccelerator (fixes the stray "N" badge).
Do not change behavior. Build. Commit "feat(ui): design tokens and styles".
