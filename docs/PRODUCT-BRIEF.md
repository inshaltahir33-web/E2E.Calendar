# Product Brief — E2E Calendar

*Author: Antigravity (senior engineer / tech lead)*
*Date: 25 Oct 2026*

---

## Who are the users?

| Segment | Primary need | Winning feature |
|---|---|---|
| **Students** | Recurring class schedules, exam/assignment alerts | Recurring events, colour-coded calendars, free tier |
| **Freelancers / professionals** | See Google and Outlook meetings side-by-side | Unified account view, meeting-link Join button, time zones |
| **Families / home users** | Shared events, bills, birthdays | Shared calendars, birthday auto-reminders, tasks |
| **People who forget things** | Reliable nudges, even after sleep/reboot | Persistent alarm, snooze, missed-reminder catch-up |

The common thread: users are leaving Microsoft Mail & Calendar (retired Dec 2024) and have no good native Windows alternative. Outlook-new is email-first and unreliable offline. The Mac ecosystem has Fantastical; Windows has nothing equivalent.

---

## Design principles

1. **Speed first** — cold launch to usable view in < 1 s.
2. **Reminders that cannot be missed** — 99.9 % within 5 s, even with window closed or after sleep/reboot.
3. **Natural language everywhere** — "Dentist Friday 5 pm remind me 1 hour before" just works.
4. **Native Windows 11 look** — WinUI 3, Mica, system accent colour, Segoe UI Variable, light/dark/high-contrast.
5. **Privacy by default** — no account required; OAuth tokens only in Windows Credential Manager; no personal data in logs; analytics opt-out.

---

## Phase split

### MVP — ship this, nothing else

| Area | What ships |
|---|---|
| Views | Day, Week (5/7-day), Month, Agenda; side mini-calendar |
| Events | Create/edit/duplicate/delete + 10 s undo; drag-move/resize; Ctrl+drag copy; recurrence (RFC 5545); meeting-link detect; event templates (2 free/unlimited premium) |
| Reminders | Event alert, standalone, recurring, all-day, persistent alarm, countdown; Snooze/Done/Open/Join toast actions; missed catch-up on wake |
| Tasks | Inbox list, due date, sub-tasks, overdue roll-forward |
| Sync | Local SQLite; ICS import/export/subscription; Google Calendar two-way; Microsoft Graph two-way |
| Quick add | NLP parser; global hotkey (Win+Alt+C); tray icon |
| Windows integration | Toast notifications (action buttons); start at login; tray with badge |
| Onboarding | 3 screens: connect account → theme → enable notifications |
| Settings | All 9 sections from spec s9.5 |
| Accessibility | Full keyboard, Narrator labels, 4.5:1 contrast, 200 % text scaling |
| Packaging | MSIX (x64 + ARM64), WACK pass, Store submission ready |

### v1.1 — next release after Store approval

Year view, custom multi-day view; iCloud/CalDAV; MS To Do/Google Tasks sync; push notifications (Google watch / Graph subscription); Windows 11 Widgets; Taskbar jump list + overdue badge; Windows share target; Hijri/Chinese/Hebrew dates; weather in day headers; habit tracker; attachments.

### v2 — future

Location-aware reminders; travel-time block; Windows Search integration; pen input; cloud backup (E2E encrypted); Urdu/Arabic RTL; Windows Hello app lock (gated in premium).

---

## Free vs Premium matrix (key rows)

| Feature | Free | Premium |
|---|---|---|
| All four core views | ✅ | ✅ |
| Connected accounts | 1 | Unlimited |
| Persistent alarms | 3 active | Unlimited |
| Task lists | 1 | Unlimited + sub-tasks + time-blocking |
| Event templates | 2 | Unlimited |
| Year view / custom multi-day | ❌ | ✅ |
| Calendar sets/profiles | ❌ | ✅ |
| Find free slot / share availability | ❌ | ✅ |
| Themes, accent, custom fonts | Light/Dark only | All themes |
| Ads | None | None |

**Pricing:** Monthly $2.99 · Yearly $19.99 ("Save 44 %", highlighted) · Lifetime $39.99. Billing via `Windows.Services.Store` in-app purchase. 7-day free trial.

---

## MVP acceptance criteria (from spec §10)

- [ ] All four core views scroll at 60 fps with 10,000 events.
- [ ] Two-way sync with one Google account + one Microsoft account, including offline edits.
- [ ] Reminders fire with window closed, after sleep/wake, and after reboot (99.9 % within 5 s).
- [ ] Snooze, Mark done, and Join actions work from the toast.
- [ ] Quick add correctly parses 20 QA test phrases.
- [ ] Global hotkey and tray quick capture work over any foreground app.
- [ ] Purchase, trial, restore, and expiry are testable for Monthly/Yearly/Lifetime.
- [ ] Passes Windows App Certification Kit; ready for Store submission.
- [ ] Narrator, keyboard-only, and high-contrast checks pass.
