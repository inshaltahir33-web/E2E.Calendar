Read docs/06-SYNC.md (Microsoft). Implement Sync/Microsoft/GraphCalendarProvider with MSAL (PKCE loopback), token cache serialised into Credential Manager, calendars list, calendarView/delta with deltaLink (+ reset on syncStateNotFound), outbox push (POST/PATCH/DELETE), recurrence + exceptions mapping, Teams meeting link from onlineMeeting.joinUrl. Reuse SyncEngine and conflict logic from P11/P13; no copy-paste—extract shared code.
Free tier: total connected accounts = 1 (any provider); premium unlimited.
Tests like P13. Commit "feat(sync): microsoft graph two-way".
