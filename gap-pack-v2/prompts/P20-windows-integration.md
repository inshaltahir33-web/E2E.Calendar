Windows integration extras (Platform project):
1. Taskbar jump list (New event, New reminder, Today) and overdue badge (BadgeNotificationManager / taskbar overlay).
2. Share target: manifest declaration; sharing text/URL from another app opens Quick Add prefilled.
3. Windows Hello lock (premium): optional on launch/resume via UserConsentVerifier; blur content while locked; timeout setting.
4. Compact always-on-top agenda strip (premium): narrow window (320px) via AppWindow presenter OverlappedPresenter.IsAlwaysOnTop, shows next events + reminders, remembers position, Snap-friendly.
5. Focus Assist: document and implement best-effort detection; Critical alarms use alarm popup (audio) — document the limits in docs/07-KNOWN-GAPS.md.
6. Verify high-DPI and touch targets >= 40px.
Commit "feat(platform): jump list, share target, hello, compact strip".
