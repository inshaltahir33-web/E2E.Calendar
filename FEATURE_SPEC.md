Windows Calendar App — Feature Specification
12 Oct 2026 · @jawad
1. Overview
We are building a fast, native-feeling calendar and reminders app for Windows 10/11, published on the Microsoft Store under E2E Dev. It fills the gap left when Microsoft retired the built-in Mail & Calendar app and pushed everyone into Outlook.
The gap on Windows
• Microsoft ended support for the Mail & Calendar apps on 31 Dec 2024; the replacement is the new Outlook, which is email-first, web-based and weak offline (TechRadar).
• The Windows 11 taskbar calendar flyout showed no events at all until Microsoft began adding an Agenda view in 2026 (Pureinfotech). It is still view-only.
• Mac users get Apple Calendar + Reminders free, and Fantastical / BusyCal as premium options. Windows has no equivalent standalone app.
Product promise: one app for events, reminders and tasks that opens in under a second, works offline, syncs with Google, Microsoft and iCloud, and feels like it belongs on Windows 11.
Target users
Segment
Main need
What wins them
Students
Class timetable, exam and assignment reminders
Recurring schedules, colour coding, free tier
Freelancers / professionals
Meetings across Google + Outlook accounts
Unified view of all accounts, time zones, meeting links
Families / home users
Shared family events, bills, birthdays
Shared calendars, birthday and bill reminders
People who forget things
Reliable nudges
Persistent alarms, snooze, location/time reminders
Design principles
1. Speed first: launch to usable view in under 1 second.
2. Reminders that cannot be missed.
3. Natural language everywhere (type “Dentist Friday 5pm remind me 1 hour before”).
4. Native Windows 11 look: Fluent Design, Mica, light/dark mode.
5. Privacy by default: data stays on the device unless the user connects an account.
2. Core calendar features
These are the must-haves for launch; without them the app cannot replace Outlook or Google Calendar in a browser.
2.1 Views
View
Behaviour
Priority
Day
Hour grid, all-day strip on top, current-time red line
MVP
Week
7 columns (or 5-day work week toggle), drag to create
MVP
Month
Grid with event chips, “+3 more” overflow, mini heat dots
MVP
Agenda / List
Scrollable list of upcoming events and reminders grouped by day
MVP
Year
12 mini months, days with events highlighted
v1.1
Multi-day custom
2–14 day range chosen by the user
v1.1
Side mini-calendar
Small month picker in sidebar for quick jumping
MVP
2.2 Events
• Create, edit, duplicate, delete; undo for 10 seconds after delete.
• Fields: title, date/time or all-day, end time, location (with map link), notes (rich text), URL, attendees, calendar, colour, attachments (v1.1).
• Drag-and-drop to move; drag edge to resize; Ctrl+drag to copy.
• Meeting links auto-detected (Teams, Zoom, Google Meet) with a Join button.
• Travel time block before an event (optional).
• Event templates (“Gym 1h”, “Client call 30m”) for one-click creation.
2.3 Recurrence
• Daily, weekly (pick days), monthly (date or “2nd Tuesday”), yearly, every N days/weeks.
• End: never, on date, after N times.
• Edit “this event / this and following / all events”.
• Exceptions (skip one occurrence) and full RFC 5545 RRULE support so sync stays correct.
2.4 Calendars and organisation
• Multiple calendars per account (Work, Personal, Family), each with colour and show/hide toggle.
• Calendar sets / profiles (e.g. “Work mode” shows only work calendars) — premium.
• Tags/categories and search across all events (title, notes, location, attendees).
2.5 Time zones and regional settings
• Event-level time zone; secondary time zone column in Day/Week view.
• Automatic adjustment when the PC time zone changes (travel).
• Week start day, 12/24-hour clock, date format follow Windows settings with override.
• Built-in holiday calendars by country (Pakistan, US, UK, UAE, India, etc.), toggled in settings.
• Optional Islamic (Hijri) date display beside Gregorian dates, plus other alternate calendars (Chinese lunar, Hebrew) — v1.1.
3. Reminders and notifications
Reminders are the headline feature: they must fire on time even when the app window is closed, and they must be hard to miss but easy to snooze.
3.1 Reminder types
Type
Example
Priority
Event alert
10 min before meeting (multiple alerts per event allowed)
MVP
Standalone reminder
“Call bank at 3pm” with no event attached
MVP
Recurring reminder
“Take medicine every day 9am”, “Pay rent on 1st”
MVP
All-day / date reminder
Birthday, anniversary, bill due — alert at a chosen time the day before and on the day
MVP
Persistent / nagging alarm
Repeats every N minutes until marked done
MVP
Countdown reminder
“Remind me in 25 minutes” from tray or hotkey
MVP
Location reminder
“When I get to office” (Windows Location API, laptops)
v2
Smart / travel-aware
Leave-now alert using travel time from location
v2
3.2 Notification behaviour
• Native Windows toast notifications with action buttons: Snooze (5 / 10 / 30 min / 1 h / tomorrow / custom), Mark done, Open, Join meeting.
• Alarm-style full-screen or centred pop-up option for important reminders, with sound, that stays until dismissed.
• Custom sounds per reminder or calendar; option to use Windows default.
• Priority levels (Normal / High / Critical). Critical ones can break through Windows Focus / Do Not Disturb if the user allows it.
• Missed-reminder catch-up: if the PC was asleep or off, show missed reminders in a summary on wake.
• Daily digest notification each morning (“Today: 3 events, 2 reminders”) at a user-set time.
3.3 Reliability (developer requirements)
• Background process or Windows scheduled toast so alerts fire with the main window closed; app starts minimised to tray at login (user option).
• Use Windows ScheduledToastNotification where possible plus an in-app scheduler fallback.
• Wake-from-sleep handling: re-check the schedule on resume.
• Target: 99.9% of reminders fire within 5 seconds of the scheduled time in QA tests.
3.4 Reminder list (Reminders panel)
• Separate panel like Apple Reminders: Today, Scheduled, Overdue, Completed, Flagged.
• Reminders appear on the calendar grid as small tick-box items at their time.
• Overdue reminders roll forward and stay visible until done.
4. Tasks and to-dos
Tasks live beside events so users plan their day in one place instead of switching to Microsoft To Do.
• Task lists (Inbox, Work, Personal, custom) with due date, due time, priority, notes and sub-tasks (checklist).
• Tasks without a time sit in the all-day strip; tasks with a time appear on the grid.
• Drag a task from the sidebar onto the calendar to time-block it (creates a linked event).
• Unfinished tasks auto-roll to today; overdue count badge.
• Recurring tasks (same rules as events).
• Sync with Microsoft To Do and Google Tasks (v1.1); local-only lists work offline.
• Quick views: Today, Next 7 days, No date, Completed.
• Optional habit tracker (streaks for daily tasks) — premium, v1.1.
5. Accounts and sync
Users must see all their calendars in one view; two-way sync with Google and Microsoft is required for launch.
Source
Method
Direction
Priority
Local calendar (on this PC)
SQLite on device
n/a
MVP
Google Calendar
Google Calendar API, OAuth 2.0
Two-way
MVP
Outlook.com / Microsoft 365
Microsoft Graph API, OAuth 2.0
Two-way
MVP
iCloud Calendar
CalDAV with app-specific password
Two-way
v1.1
Any CalDAV server (Fastmail, Nextcloud, Yahoo)
CalDAV
Two-way
v1.1
ICS subscription (sports, holidays, school)
URL subscription, refresh every 1–24 h
Read-only
MVP
ICS file import / export
File picker, drag-drop .ics
One-off
MVP
Sync requirements
• Unlimited accounts in premium; free tier limited (see section 8).
• Offline-first: all changes saved locally, queued and synced when online; clear sync status icon.
• Conflict rule: last edit wins, with a “conflict copy” kept if both sides changed the same event.
• Push/near-real-time updates where APIs allow (Google push channels, Graph change notifications), otherwise poll every 5 minutes.
• Respond to invitations (Accept / Tentative / Decline) and send invites to attendees via the connected account.
• Tokens stored in Windows Credential Manager, never in plain files.
• Optional own cloud backup/sync between the user’s PCs (premium, v2).
6. Ease-of-use features
These are what make users choose this app over a browser tab; most of them are cheap to build and highly visible.
6.1 Natural-language quick add
• One input box parses text like “Lunch with Ali tomorrow 1pm at Kohinoor for 1h remind 30 min before” into title, date, time, duration, location and alert.
• Live preview of the parsed result before saving.
• Understands: today/tomorrow/next Monday, “every Friday”, “in 2 hours”, “#work” (calendar), “!high” (priority).
• English at launch; Urdu (Roman) and Arabic phrasing — v2.
6.2 Global quick-capture
• System-wide hotkey (default Win+Alt+C, configurable) opens a small quick-add box over any app.
• Tray icon menu: New event, New reminder, Remind me in 10/30/60 min, Today’s agenda.
6.3 Keyboard shortcuts
Shortcut
Action
N
New event
R
New reminder
T
Go to today
D / W / M / A
Day / Week / Month / Agenda view
Ctrl+F
Search
← / →
Previous / next period
Ctrl+Z
Undo
Ctrl+K
Command palette
6.4 Other conveniences
• Command palette (Ctrl+K) for every action.
• “Find a free slot” helper: suggests open times across selected calendars.
• Free-time / availability sharing: copy available slots as text to paste in chat or email.
• Birthdays pulled from contacts (Google/Microsoft) with yearly reminders.
• Weather in day headers (v1.1).
• Print / export week or month to PDF.
• Onboarding in 3 screens: connect account, pick theme, enable notifications.
7. Windows integration
Deep Windows integration is our main advantage over web calendars and Outlook.
• Fluent Design: WinUI 3 controls, Mica/Acrylic backgrounds, rounded corners, follows Windows light/dark mode and accent colour.
• Toast notifications with actions (section 3) and Notification Center history.
• Windows 11 Widgets board: “Today’s agenda” and “Next reminder” widgets (v1.1).
• System tray: icon with badge for today’s count; hover shows next event; left-click opens a mini agenda flyout.
• Taskbar: jump list (New event, New reminder, Today); progress/badge for overdue items.
• Start at login (optional, minimised to tray) so reminders always run.
• Windows Search integration: events findable from Start search (v2).
• Share target: share text or a link from another app to create an event/reminder.
• Snap layouts and resizable compact mode (a narrow always-on-top agenda strip — premium).
• Windows Hello app lock (optional) for privacy on shared PCs.
• Focus Assist: respect Do Not Disturb except for Critical reminders.
• Support for touch, pen (write on the day to create an event — v2) and high-DPI displays.
8. Premium and value for money
The free tier must be genuinely useful so the app earns good Store ratings; premium unlocks power features that busy users will pay for. Prices below are suggestions in USD, to be set per market in Partner Center.
8.1 Free vs Premium
Feature
Free
Premium
Day / Week / Month / Agenda views
Yes
Yes
Local calendar + ICS import/subscriptions
Yes
Yes
Connected accounts (Google / Microsoft / iCloud)
1 account
Unlimited
Event alerts and standalone reminders
Yes
Yes
Persistent / alarm-style reminders, custom sounds
3 active
Unlimited
Natural-language quick add
Yes
Yes
Global hotkey + tray quick capture
Yes
Yes
Tasks and lists
1 list
Unlimited lists, sub-tasks, time-blocking
Year view, custom multi-day view
No
Yes
Calendar sets / profiles
No
Yes
Event templates
2
Unlimited
Compact always-on-top agenda strip
No
Yes
Habit tracker, weather, daily digest
No
Yes
Themes, accent colours, custom fonts
Light/Dark only
All themes
Find a free slot / share availability
No
Yes
Windows Hello app lock
No
Yes
Ads
None
None
8.2 Pricing tiers (suggested)
Plan
Price
Notes
Monthly
$2.99 / month
7-day free trial
Yearly
$19.99 / year
Shown as “Save 44%”; default highlighted plan
Lifetime
$39.99 one-time
Strong value message vs subscriptions
8.3 Why it is value for money
• One app replaces three: calendar, reminders and to-do.
• Works offline and opens instantly, unlike browser calendars.
• No ads in either tier; user data is never sold.
• Lifetime option for users tired of subscriptions.
8.4 Paywall rules
• Paywall appears only when a user taps a premium feature, plus once after onboarding (skippable).
• Use Microsoft Store in-app purchases / subscriptions (Windows.Services.Store) for billing; restore purchases button.
• Premium badge (small crown) on locked items, never hidden features.
9. Technical, privacy and quality requirements
Recommended stack is native .NET + WinUI 3 packaged as MSIX, because reminders, tray, widgets and toasts work best natively and the app must stay light.
9.1 Platform and stack
Item
Requirement
OS
Windows 10 (21H2+) and Windows 11, x64 and ARM64
UI
WinUI 3 / Windows App SDK (C#, .NET 8+)
Storage
SQLite, encrypted at rest for tokens/notes
Packaging
MSIX for Microsoft Store, publisher E2E Dev
Billing
Windows.Services.Store subscriptions + durable add-on (lifetime)
Calendar standards
iCalendar RFC 5545, CalDAV RFC 4791
Background
Background task + startup task for reminders
9.2 Performance targets
• Cold start to usable view: under 1.0 s on a mid-range laptop.
• View switch and scroll: 60 fps, no visible lag with 10,000 events.
• Idle memory: under 150 MB; idle CPU near 0%.
• Reminder firing accuracy: within 5 s (section 3.3).
9.3 Privacy and security
• No account required to use the app locally.
• OAuth tokens in Windows Credential Manager; no passwords stored.
• No selling or sharing of calendar data; analytics anonymous and opt-out.
• Privacy policy URL required for Store listing; data export (ICS) and delete-all-data option in settings.
9.4 Accessibility and localisation
• Full keyboard navigation, Narrator screen-reader labels, high-contrast theme support, text scaling to 200%.
• Colour is never the only signal (icons + text for priority/status).
• Localisation-ready strings; launch in English, then Urdu, Arabic (right-to-left layout), Hindi, Spanish.
9.5 Settings screen (minimum)
General (start at login, week start, time format), Accounts, Calendars, Notifications & sounds, Appearance, Shortcuts, Premium/Subscription, Privacy & data, About/Help.
10. Roadmap and acceptance criteria
Ship the MVP first and only move to the next phase once its acceptance criteria pass; dates to be agreed with the developer.
Each phase builds on the previous one; items marked MVP, v1.1 and v2 in sections 2–9 follow this split.
MVP acceptance criteria
[ ] All four core views work with 10,000 events at 60 fps.
[ ] Two-way sync with one Google and one Microsoft account, including edits made offline.
[ ] Reminders fire with the window closed, after sleep/wake and after reboot (99.9% within 5 s).
[ ] Snooze, Mark done and Join actions work from the toast.
[ ] Quick add correctly parses 20 test phrases supplied by QA.
[ ] Global hotkey and tray quick capture work over any app.
[ ] Purchase, trial, restore and expiry tested for Monthly, Yearly and Lifetime.
[ ] Passes Windows App Certification Kit and Store submission review.
[ ] Narrator, keyboard-only and high-contrast checks pass.
Deliverables from the developer
[ ] Clickable UI mock-ups of main window, quick add, reminder pop-up and paywall before coding.
[ ] Weekly test build (MSIX) for review.
[ ] Source code, build instructions and Store listing assets (icons, screenshots) at handover.