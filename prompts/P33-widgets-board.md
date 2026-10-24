[v1.1] Spec 7. Windows 11 only (hide on Win10). Prereq: P24 (MSIX).
1. Windows App SDK Widget Provider (out-of-proc COM server in the same MSIX, registered in Package.appxmanifest with the Windows.Widgets / com.microsoft.windows.widgets extension). Two widgets: "Today's agenda" (small/medium/large) and "Next reminder" (small).
2. Adaptive Cards JSON templates. Agenda: up to 3/5/8 items with time, title, calendar colour bar. Next reminder: title + relative time ("in 12 min") + Snooze/Done buttons.
3. Provider reads ONLY from local SQLite (read-only connection) - no network, no tokens. Update on event/reminder change through a lightweight shared signal, and every minute for relative times.
4. Widget actions (Done, Snooze, Open) go back through the same reminder service/command API used by toast actions; Open uses protocol activation to the right date/event.
5. Respect dark/light via Adaptive Card host config. Narrator-friendly text.
Tests: card JSON snapshot tests, action routing unit tests. Add manual QA steps to docs/13-QA.md. Commit "feat: windows 11 widgets".
