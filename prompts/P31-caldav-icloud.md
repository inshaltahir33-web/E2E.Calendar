[v1.1] Spec 5. Prereq: P13 (sync architecture), P19 (account limit).
1. Create docs/06b-CALDAV.md first: RFC 4791 (CalDAV), RFC 6764 (discovery), RFC 6578 (sync-collection), RFC 5545. iCloud: https://caldav.icloud.com + app-specific password. Fastmail / Nextcloud / Yahoo presets.
2. Sync/CalDav/CalDavProvider implementing the same ISyncProvider as Google/Microsoft: PROPFIND discovery (current-user-principal -> calendar-home-set), calendar list with colours (apple-ical calendar-color), initial REPORT, incremental via sync-token (fallback to ctag/etag diff), PUT/DELETE with If-Match, VEVENT <-> internal model, VTIMEZONE handling, RRULE/EXDATE/RECURRENCE-ID round-trip, VALARM -> alerts.
3. Add-account UI: provider picker (iCloud / Fastmail / Nextcloud / Other CalDAV), server URL, username, app password + a short "how to create an iCloud app-specific password" help link. Password goes ONLY to ISecretStore (Credential Manager). Test connection button.
4. Respect free-tier 1 account rule. Same conflict rule: last edit wins + conflict copy.
5. Do not log URLs with credentials, event content or passwords.
Tests: recorded-XML fixtures for iCloud and Nextcloud, 412 conflict, token expiry, offline outbox. Commit "feat(sync): caldav and icloud".
