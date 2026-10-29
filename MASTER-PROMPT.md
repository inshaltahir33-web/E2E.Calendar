# MASTER PROMPT: Stabilise, complete and package the Windows Calendar app

You are the senior engineer and tech lead taking over this repository. The app is a native Windows 10/11 calendar + reminders + tasks app (WinUI 3, .NET 8+, SQLite, MSIX, publisher "E2E Dev"). It was built in many separate AI sessions, so it is inconsistent: some features work, some are fake or half-done, the UI mixes several styles, and some controls do not behave (for example dialogs/flyouts that open and never close). Your job is to turn it into one coherent, professional, fully working product.

## 0. Ground rules (apply to every phase)
- Work in phases, in order. Commit at the end of each phase (small, clear messages). Never rewrite history, never force push, never push to a remote; I will push.
- Do not add fake features. Every visible control must work, or be hidden, or be disabled with a visible reason. No dead buttons, no empty tabs, no "TODO" screens in a shipped build.
- Never delete or weaken a test to make it pass. Fix the code.
- Secrets (Google/Microsoft client IDs, secrets, tokens) are never committed. They come from `dotnet user-secrets` / env vars. Tokens only in Windows Credential Manager.
- Keep one source of truth: one theme/token dictionary, one navigation mechanism, one DI container, one way to do dialogs, one date/time service. Remove duplicates instead of adding a third.
- Be honest in every report: PASS / FAIL / NOT RUN. If you could not run or verify something, say so. Never claim "works" without evidence (test name, command output, or exact manual steps).
- Preserve user data model and migrations; add new migrations instead of editing old ones.
- Google sync stays behind a build flag `GoogleSyncEnabled` (default false) until Google approves our OAuth app. Microsoft sync is enabled.

## 1. Phase 0: Understand the product (no code changes)
Read everything before judging anything:
1. `docs/spec-original.txt` (the product spec, the source of truth), `AGENTS.md`, and every file in `docs/` and `prompts/` (design rules docs/08, docs/11, sync spec docs/06-SYNC.md, gap analysis docs/12-GAPS-ANALYSIS.md).
2. Write `docs/PRODUCT-BRIEF.md`: in your own words, who the users are, the design principles, the MVP / v1.1 / v2 split, the free vs premium matrix, and the MVP acceptance criteria. Keep it under 2 pages. This proves you understood the requirements.

## 2. Phase 1: Audit (no feature work yet)
1. Build the solution, run all tests, launch the app. Record exact results.
2. Produce `docs/AUDIT.md` covering: architecture and project structure; duplicate or conflicting classes/styles/services; dead code; stub/fake implementations (search for TODO, NotImplemented, hard-coded sample data, empty handlers); UI inconsistencies (spacing, fonts, colours not from tokens, mixed control styles, layouts that break at small width / 200% scaling); navigation and dialog/flyout lifecycle bugs; threading and performance risks; security issues.
3. Produce `docs/REQUIREMENTS-MATRIX.md`: one row per spec item (use the spec section numbers). Columns: spec item | phase (MVP/v1.1/v2) | status (Working / Partial / Broken / Missing) | evidence (file + test or manual check) | fix needed.
4. Produce `docs/FIX-PLAN.md`: ordered work packages with size estimate, risk and dependencies.
5. STOP here. Print a summary (counts per status, top 10 problems, proposed order) and wait for my reply "go" before Phase 2.

## 3. Phase 2: Foundation
- Make the build green with zero errors and no new warnings. Fix nullable and analyzer issues properly.
- Clean architecture: MVVM with a single DI container; Domain / Data (SQLite, migrations) / Services (reminders, sync, entitlement, parsing) / UI separated. Move logic out of code-behind.
- Delete dead code and duplicates found in the audit. Merge competing theme/style files into one token system (colours, spacing, radius, typography, elevation) with light, dark and Windows high-contrast variants. No hard-coded colours or font sizes in views.
- Central services: IClock (testable time), IDialogService, INavigationService, ISettingsService, ILogger (no personal data in logs), IEntitlementService.
- Commit "refactor: foundation".

## 4. Phase 3: UI/UX rebuild to a professional standard
Target: Windows 11 Fluent look, as if made by Microsoft.
- App shell: left sidebar (mini month calendar, calendars with colour + show/hide, task lists, tags), top toolbar (today, prev/next, current period title, view switcher Day/Week/Month/Agenda, search, quick add, sync status, settings), Mica backdrop, rounded corners, correct title bar behaviour. Window resizes cleanly down to a narrow width; sidebar collapses to an icon rail.
- Views: Day, Week (5/7 day toggle), Month ("+N more" overflow), Agenda. Consistent grid lines, hour labels, red current-time line, all-day strip, event chips with calendar colour, drag to create/move/resize, keyboard navigation. 60 fps with 10,000 events (virtualise).
- Dialogs, flyouts, popups, context menus, date/time pickers: ONE consistent pattern through IDialogService. Every one must close with: Esc, the close/cancel button, a click outside (for light-dismiss ones), and after Save; focus must return to the control that opened it. No orphan overlays, no stuck popups, no multiple stacked dialogs by accident.
- States for every screen: loading, empty (with a helpful action), error (with retry). Consistent icons (Segoe Fluent Icons), no emoji as UI.
- Accessibility: full keyboard path, tab order, Narrator names on every control, 4.5:1 contrast, text scaling to 200%, colour never the only signal.
- Commit "feat(ui): professional shell, views and dialogs". Save screenshots of each screen (light and dark) in `docs/screenshots/`.

## 5. Phase 4: Functional QA of EVERY control
1. Create `docs/QA-CHECKLIST.md`: a table with every button, tab, menu item, toggle, shortcut, tray item, toast action and settings option in the app: control | where | expected behaviour | result (PASS/FAIL/NOT RUN) | evidence.
2. Verify each one. Use automated UI tests (WinAppDriver / Appium / FlaUI, or ViewModel tests) where possible; otherwise write exact manual steps and mark NOT RUN if you could not execute them.
3. Fix every FAIL, then re-run. Repeat until none are FAIL. Specifically verify: create/edit/duplicate/delete + 10 s undo; drag/resize/copy; recurrence (this / this and following / all, exceptions); all reminder types, snooze, mark done, persistent alarm, countdown, catch-up after sleep; tasks and sub-tasks; quick add parsing; tray + global hotkey; all keyboard shortcuts from spec 6.3; ICS import/export/subscription; settings persistence after restart.
4. Commit "fix: functional QA pass 1".

## 6. Phase 5: Build what is missing (priority order)
Use `docs/REQUIREMENTS-MATRIX.md` and the prompts in `prompts/` as detailed specs for each item. Finish each item completely (UI + logic + tests + settings + docs) before starting the next.
1. MVP core gaps: reminders reliability (ScheduledToast + in-app scheduler fallback, wake handling), tasks, search + tags (P16), holidays + regional defaults (P17), command palette Ctrl+K + find free slot (P18), onboarding (3 screens), full Settings (9 sections from spec 9.5), data export + delete-all.
2. Microsoft sync (P14) fully working two-way, offline queue, conflict copy, sync status. Google sync (P13) implemented but behind `GoogleSyncEnabled`. Invitations + birthdays (P15).
3. Premium and billing (P19, P40): feature gating in services, paywall, restore, free-tier limits, crown badges. Billing code must compile and be testable with a fake entitlement switch (Debug only).
4. Windows integration (P20), event extras (P21), print/PDF (P22), analytics opt-out (P39).
5. Only after the above pass QA: v1.1 items (P26 to P33, P36) and v2 (P34, P35, P37, P38) as time allows.
After each item: update the matrix row, add tests, commit "feat: <item>".

## 7. Phase 6: Quality gates
- Unit + integration tests green; coverage report for Services.
- Performance (spec 9.2): cold start under 1 s, idle RAM under 150 MB, idle CPU about 0 %, 10k events at 60 fps. Measure and record numbers in `docs/PERF.md`.
- Reminder accuracy harness (99.9% within 5 s) with fake clock + a real-time run (P23).
- Accessibility pass (Narrator, keyboard only, high contrast, 200 %) recorded in `docs/A11Y.md`.
- Security: no secrets in repo (scan git history of the working tree), tokens only in Credential Manager, logs free of personal data.

## 8. Phase 7: Packaging and handover
- `scripts/build-msix.ps1`: restore, build, test, publish x64 and ARM64, create the MSIX (and a `.msixbundle` if both architectures), sign with a test certificate for sideload testing. Document how to install the test certificate and the package (`docs/PACKAGING.md`). Store submission uses the Partner Center identity (publisher E2E Dev); leave clearly marked placeholders for the real identity values.
- Fix `.gitignore` (bin, obj, .vs, *.msix, *.pfx, user secrets). Add `README.md` (what the app is, build, run, test, package, configure secrets) and `CHANGELOG.md`.
- Run the Windows App Certification Kit if available; record the result or mark NOT RUN with the command to run it.
- Final: `docs/ACCEPTANCE.md` (spec section 10 checkboxes with PASS / FAIL / NOT RUN and evidence), then print the git commands I should run to push. Do not push.

## 9. How to report
At the end of each phase print: what changed (5 lines), tests (pass/fail counts), anything skipped or not verified, and the next phase. If something blocks you (a secret, an account, a decision only I can make), stop and tell me exactly what you need.