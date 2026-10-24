1. MSIX packaging for x64 + ARM64 (publisher "E2E Dev", identity placeholders in Package.appxmanifest clearly marked), capabilities minimal (internetClient, no broad file access), startupTask, toast activation, share target, protocol for OAuth if needed. App icon/asset set (all required scales) from a single SVG placeholder.
2. GitHub Actions: build + test + `dotnet publish` MSIX artifact per tag (unsigned for Store upload; sideload test cert in a separate local script).
3. Run Windows App Certification Kit locally; write docs/WACK-REPORT.md with fixes.
4. Store listing pack in /store: description (short + long), keywords, release notes, privacy policy markdown (data stays on device; accounts only on user connect; Google Limited Use statement; delete-all-data), support page text, age-rating answers, screenshots shot-list (Week Light, Week Dark, Month, Quick add, Reminder toast, Paywall) with headline text.
Commit "chore: packaging and store assets".
