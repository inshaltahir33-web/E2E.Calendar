Read docs/06-SYNC.md (Common + Google) and docs/02-ARCHITECTURE.md §7.
Implement Sync/Google/GoogleCalendarProvider: OAuth PKCE loopback sign-in (Account add flow in Settings > Accounts, premium gating via IEntitlementService: free = 1 account), token storage via ISecretStore, calendar list import (colors map to nearest palette name), initial full pull, incremental pull with syncToken (410 -> full resync), push outbox (insert/patch/delete with etag), RRULE/EXDATE round-trip, meeting link from conferenceData, sync status icon states (idle/syncing/offline/error with tooltip) in toolbar + sidebar footer.
Tests: FakeHttp handler tests for pull/push/410/conflict copy/offline queue; env-flag integration test. Do not log tokens or event content.
Commit "feat(sync): google calendar two-way".
