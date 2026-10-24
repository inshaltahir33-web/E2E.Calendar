# 12 — Gap Analysis: Spec vs Prompts

Legend: ✅ prompt me tha · 🟡 adha/sirf skeleton · ❌ prompt me nahi tha

**Total 70 spec items → ✅ 24 · 🟡 13 · ❌ 33 (ab sab ke prompts: P13–P42)**

| § | Spec item | Status | Prompt | Phase |
|---|---|---|---|---|
| 2.1 | Day / Week / Month / Agenda views | ✅ | P05, U04, U05 | MVP |
| 2.1 | Side mini-calendar | ✅ | P04, U03 | MVP |
| 2.1 | Year view + custom multi-day | ❌ | P26 | v1.1 |
| 2.2 | Event create/edit/duplicate/delete + undo | ✅ | P01, P06 | MVP |
| 2.2 | Drag move / resize / Ctrl+drag copy | ✅ | U04 | MVP |
| 2.2 | Meeting link auto-detect + Join | ✅ | P06 | MVP |
| 2.2 | Event templates | ✅ | P06 | MVP |
| 2.2 | Rich-text notes | 🟡 | P21 (P06 me plain text) | MVP |
| 2.2 | Attendees field + invites | 🟡 | P15 (UI me tha, logic nahi) | MVP |
| 2.2 | Travel time block | ❌ | P21 | MVP-optional |
| 2.2 | Attachments | ❌ | P30 | v1.1 |
| 2.3 | Recurrence + exceptions + this/following/all | ✅ | P03, P06 | MVP |
| 2.4 | Multiple calendars, colour, show/hide | ✅ | P04, U11 | MVP |
| 2.4 | Calendar sets / profiles | ❌ | P19 | Premium |
| 2.4 | Tags / categories | ❌ | P16 (DB me table bhi nahi thi) | MVP |
| 2.4 | Search across events | 🟡 | P16 (FTS table thi, service/UI nahi) | MVP |
| 2.5 | Event-level time zone + secondary column | ✅ | P06, U04 | MVP |
| 2.5 | Auto-adjust on PC time-zone change | 🟡 | P07 (planner re-run), test P23 | MVP |
| 2.5 | Week start / 12-24h / date format follow Windows | 🟡 | U08 (override), P17 (Windows default) | MVP |
| 2.5 | Holiday calendars by country | ❌ | P17 | MVP |
| 2.5 | Hijri / Chinese / Hebrew dates | ❌ | P27 | v1.1 |
| 3.1 | Event alert, standalone, recurring, all-day, persistent, countdown | ✅ | P07, P09 | MVP |
| 3.1 | Location + travel-aware reminders | ❌ | P34 | v2 |
| 3.2 | Toast actions Snooze/Done/Open/Join | ✅ | P07 | MVP |
| 3.2 | Alarm popup + priority + Focus Assist | ✅ | P07 | MVP |
| 3.2 | Custom sounds per reminder/calendar | 🟡 | P21 | MVP |
| 3.2 | Missed-reminder catch-up | ✅ | P07 | MVP |
| 3.2 | Daily digest (time setting) | 🟡 | P07 (logic), settings P17 | Premium |
| 3.3 | 99.9% within 5 s proof (test harness) | ❌ | P23 | MVP |
| 3.4 | Reminders panel + grid tick-boxes | ✅ | P10, U12 | MVP |
| 4 | Tasks, subtasks, roll-over, time-block | ✅ | P10 | MVP |
| 4 | Sync with MS To Do / Google Tasks | ❌ | P32 | v1.1 |
| 4 | Habit tracker | ❌ | P29 | v1.1 |
| 5 | Local SQLite + ICS import/export/subscription | ✅ | P02, P11 | MVP |
| 5 | Google two-way sync (real) | 🟡 | P13 (P11 = skeleton only) | MVP |
| 5 | Microsoft Graph two-way sync (real) | 🟡 | P14 (P11 = skeleton only) | MVP |
| 5 | Invitations Accept/Tentative/Decline | ❌ | P15 | MVP |
| 5 | Push channels / change notifications | ❌ | P36 (polling MVP me hai) | v1.1 |
| 5 | iCloud / CalDAV | ❌ | P31 | v1.1 |
| 5 | Own cloud backup/sync | ❌ | P37 | v2 |
| 6.1 | Natural-language quick add | ✅ | P08 | MVP |
| 6.2 | Global hotkey + tray | ✅ | P09 | MVP |
| 6.3 | Keyboard shortcuts | ✅ | P04 | MVP |
| 6.4 | Command palette Ctrl+K | ❌ | P18 (sirf shortcut tha) | MVP |
| 6.4 | Find free slot + share availability | ❌ | P18 | Premium |
| 6.4 | Birthdays from contacts | ❌ | P15 | MVP |
| 6.4 | Print / export to PDF | ❌ | P22 | MVP |
| 6.4 | Weather in headers | ❌ | P28 | v1.1 |
| 6.4 | Onboarding 3 screens | ✅ | U08 | MVP |
| 7 | Fluent look, Mica, light/dark/theme | ✅ | U01, U11 | MVP |
| 7 | Tray, start at login | ✅ | P07, P09 | MVP |
| 7 | Taskbar jump list + overdue badge | ❌ | P20 | MVP |
| 7 | Share target | ❌ | P20 | MVP |
| 7 | Windows Hello app lock | ❌ | P20 | Premium |
| 7 | Compact always-on-top agenda strip | ❌ | P20 | Premium |
| 7 | Widgets board | ❌ | P33 | v1.1 |
| 7 | Windows Search / pen input | ❌ | P35 | v2 |
| 8 | Entitlement service (interface) | 🟡 | P01, P10 (sirf interface/gating 1 jagah) | MVP |
| 8 | Store billing, trial, restore, paywall UI | ❌ | P19 | MVP |
| 8 | Free-tier limits enforced in services | ❌ | P19 | MVP |
| 8 | Premium themes / accent / fonts | ❌ | P19 (accent) + P40 (themes, fonts) | Premium |
| 9 | SQLite encrypted, Credential Manager | ✅ | P02, P11, P12 | MVP |
| 9 | Performance targets (startup, RAM, CPU) | 🟡 | P02 (10k query), P23 (baaqi) | MVP |
| 9 | Delete-all-data + ICS export | ✅ | P12 | MVP |
| 9 | Accessibility audit (Narrator, HC, 200%) | 🟡 | U-prompts me rules, P23 me full audit | MVP |
| 9 | Localisation-ready strings (.resw), RTL | ❌ | P25 | MVP-ready |
| 9 | Anonymous analytics opt-out | ❌ | P39 (spec ke mutabiq, opt-out toggle) | MVP |
| 10 | MSIX package, WACK, Store submission | ❌ | P24 | MVP |
| 10 | Clickable mock-ups before coding | 🟡 | P41 | MVP |
| 10 | Store listing assets (icons, screenshots, text) | ❌ | P24 | MVP |

## Do cheezein jo maine mention ki thi par banai nahi thi
- `docs/06-SYNC.md` (P11 me referenced) -> ab is pack me hai.
- `docs/07-KNOWN-GAPS.md` -> P12 khud banata hai.
- `docs/12-CLOUD-BACKEND.md` (v2 cloud) -> ab P37 pehle ye doc likhta hai.

# Missing items: kya hai, kis liye, kaise, natija

| Item | Kis liye istemal (user ko kya milta hai) | Kaise (tech) | Natija jab ban jaye | Prompt |
|---|---|---|---|---|
| Google sync | User ke Google calendars ek app me, offline edit bhi sync | Google Calendar API v3, OAuth PKCE (loopback), `syncToken`, outbox push | Google event badlo, 5 min me app me; app me badlo, Google me | P13 |
| Microsoft sync | Outlook/365 calendars | Microsoft Graph, MSAL, `calendarView/delta`, outbox push | Outlook ke events dono taraf sync | P14 |
| Invitations + attendees | Meeting accept/decline, invite bhejna | Provider API (Google `attendees`, Graph `/accept` `/tentativelyAccept` `/decline`) | Invite aaye to toast/panel me Accept/Tentative/Decline | P15 |
| Birthdays from contacts | Birthday reminder khud ban jaye | Google People API / Graph contacts (read-only), yearly all-day reminder | "Ali's birthday" saal bhar auto | P15 |
| Search + tags | Purana event 2 second me dhoondna | SQLite FTS5 (`event_fts`), tags table + many-to-many, Ctrl+F results flyout | Title/notes/location/attendees search <100ms | P16 |
| Holiday calendars | Pakistan/US/UK/UAE/India chhuttiyan | Bundled .ics per country + toggle, read-only calendar | Settings me country on karo, holidays dikhen | P17 |
| Windows regional defaults | App Windows ke week-start/12-24h ko follow kare | `Windows.Globalization` / `CultureInfo` + override | Naye user ko manual setting nahi karni | P17 |
| Command palette | Keyboard se har kaam: "new reminder", "go to Dec 5" | Ctrl+K overlay, fuzzy match actions+events, ICommand registry | Mouse ke baghair poori app | P18 |
| Find free slot + share | Meeting ka time suggest, chat me paste | Busy intervals merge -> invert in work hours -> text | "Mon 2-3pm, Tue 11-12" copy | P18 |
| Store billing + paywall | Paise kamana (Monthly/Yearly/Lifetime) | `Windows.Services.Store.StoreContext`, subscription + durable add-on, `IEntitlementService`, paywall dialog, restore | Trial/buy/restore kaam kare, free limits lagen | P19 |
| Calendar sets/profiles, premium themes | "Work mode" ek click | Saved visibility presets, accent presets in Tokens | Premium badge ke saath | P19 |
| Jump list, badge, Hello lock, share target, compact strip | Windows jaisa deep integration | `JumpList`, `BadgeNotificationManager`, `UserConsentVerifier`, `ShareTarget` manifest, AppWindow always-on-top | Taskbar right-click menu, lock, share-to-app | P20 |
| Rich notes, travel block, custom sounds | Detail aur personalization | RichEditBox (RTF -> sanitized HTML/Markdown), travel_minutes block render, per-alert sound file picker | Notes bold/list, "Leave in 20 min" block | P21 |
| Print / PDF export | Week/Month print | WinUI print + PDF via `PrintManager`/QuestPDF | Week PDF bane | P22 |
| Reliability + perf + a11y harness | "99.9% within 5 s" sabit karna | Soak test with fake clock + real-time test, startup stopwatch, memory/CPU profile, Narrator checklist | Report me numbers, spec ke targets pass | P23 |
| MSIX, WACK, Store assets, CI | Store pe publish | MSIX packaging, WACK run, GitHub Actions artifact, listing text/screenshots | Submission-ready package | P24 |
| Localisation-ready strings | Baad me Urdu/Arabic RTL | `.resw` per language, x:Uid, FlowDirection test | Koi hard-coded text nahi | P25 |

# Pehle "jaan boojh kar chhodi" cheezein — ab sab ke prompts hain
| Item | Phase | Prompt | Note |
|---|---|---|---|
| Year + multi-day views | v1.1 Premium | P26 | |
| Hijri / Chinese / Hebrew | v1.1 | P27 | Hijri +/-2 din adjustment (Pakistan ke liye) |
| Weather in headers | v1.1 Premium | P28 | Open-Meteo, sirf city naam, auto-location nahi |
| Habit tracker | v1.1 Premium | P29 | |
| Attachments | v1.1 | P30 | |
| iCloud / CalDAV | v1.1 | P31 | |
| MS To Do + Google Tasks sync | v1.1 | P32 | |
| Windows 11 Widgets | v1.1 | P33 | |
| Push relay (instant sync) | v1.1/v2 | P36 | Server chahiye, content kabhi relay se nahi guzarta |
| Location + travel-aware reminders | v2 | P34 | |
| Windows Search + pen input | v2 | P35 | |
| Cloud backup/sync (E2E encrypted) | v2 Premium | P37 | Server chahiye |
| Urdu (Roman) + Arabic parsing | v2 | P38 | |
| Analytics (anonymous, opt-out) | MVP | P39 | Spec ke mutabiq; default ek constant se flip hota hai |
| Premium themes + custom fonts | Premium | P40 | |
| Clickable mock-ups | Deliverable | P41 | |
| MVP acceptance run | Release gate | P42 | |

# Order (naye prompts ke saath)
1. Pehle U11, U12 (theme + UI bugs).
2. Sync block: P13 -> P14 -> P15.
3. Roz ke features: P16, P17, P18.
4. Paisa: P19 -> P40.
5. Polish: P20, P21, P22. Phir P39 (analytics) aur P41 (mockups).
6. Release: P23 -> P25 -> P24 -> P42. MVP Store pe jaye.
7. v1.1: P26, P27, P28, P29, P30, P31, P32, P33, P36.
8. v2: P34, P35, P37, P38.
