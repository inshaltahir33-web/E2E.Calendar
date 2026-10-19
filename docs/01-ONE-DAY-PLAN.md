# Ek Din Ka Plan (Realistic)

## Pehle sach baat
Poori spec (sync, widgets, billing, location, 5 languages) 1 din me nahi banti — wo hafton ka kaam hai.
**Aaj ka target = "Vertical Slice MVP-lite"**: chalne wala app jisme local calendar, 4 views, event CRUD + recurrence,
reminders jo window band hone par bhi fire hon, quick-add parser, tray, aur Git pe pushed repo.
Google/Microsoft sync ka sirf skeleton + ICS import/export aaj; full OAuth sync Day 2-3.

## Tools
- VS Code: editing + OpenCode terminal. **Visual Studio 2022 (Community, free)** WinUI 3 XAML build/F5 ke liye zaroori hai (VS Code akela WinUI designer/packaging achi tarah nahi chalata).
- OpenCode: code generate/refactor. Har prompt ke baad aap `dotnet build` + test khud verify karo.

## Hour-by-hour (~12 ghante)
| Time | Kaam | Prompt | Output |
|---|---|---|---|
| 0:00-0:45 | Setup, repo, AGENTS.md, git init, GitHub repo | P00 | Solution skeleton, first push |
| 0:45-2:00 | Core models + SQLite + migrations + repos | P01, P02 | DB chalta hua, tests |
| 2:00-3:15 | Recurrence engine (Ical.Net) + occurrence expansion | P03 | Tests green |
| 3:15-5:30 | App shell (NavigationView, Mica, sidebar) + Month/Week/Day/Agenda | P04, P05 | UI chalta hua |
| 5:30-6:30 | Event editor flyout + CRUD + undo | P06 | Events ban/edit/delete |
| 6:30-8:00 | Reminder engine + toasts + snooze + wake catch-up | P07 | Window band, toast fire |
| 8:00-9:00 | Natural language quick add + live preview | P08 | 20 phrase tests |
| 9:00-10:00 | Tray + global hotkey + startup task | P09 | Win+Alt+C kaam kare |
| 10:00-10:45 | Tasks/Reminders panel (basic) | P10 | Local lists |
| 10:45-11:30 | ICS import/export + Sync skeleton | P11 | .ics round trip |
| 11:30-12:00 | Security pass, README, tag v0.1.0, push | P12 | GitHub release tag |

## Day 2-5 (agla)
D2 Google OAuth two-way sync · D3 Microsoft Graph · D4 Store billing + paywall + premium gating · D5 QA, accessibility, WACK, Store submission.

## Daily routine with OpenCode
1. `git checkout -b feat/<name>`
2. Prompt do (prompts/ se copy-paste). OpenCode me pehle Plan mode (Tab key) me plan dekho, phir Build mode.
3. `dotnet build && dotnet test`; fail ho to error OpenCode ko paste karo.
4. `git add -A && git commit -m "feat: ..."` -> `git push -u origin HEAD` -> merge.
