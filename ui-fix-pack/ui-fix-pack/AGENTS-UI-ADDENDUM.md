## UI rules (append to AGENTS.md)
- Before any XAML work read docs/08-DESIGN-SYSTEM.md. Use tokens from Themes/Tokens.xaml; no hard-coded colors/sizes.
- One component per task. After UI changes always run the app with demo data and describe what you verified.
- Never leave KeyboardAccelerators with visible badges; set PlacementMode Hidden.
- Never use stock controls in a way that clips (check at sidebar 272px and window 900px).
- Every new UI element: hover/pressed/focus states, AutomationProperties.Name, Dark + High-contrast check.
- If a spec number is missing, ask instead of guessing.
