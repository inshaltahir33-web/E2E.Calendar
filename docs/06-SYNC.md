# Sync Spec (Google + Microsoft)

## Common
- Desktop app = **public client**. No client secret. OAuth 2.0 **Authorization Code + PKCE**, system browser, loopback redirect `http://127.0.0.1:<random-port>/`.
- Client IDs in user-secrets / build config (not secrets, but keep out of repo history if possible). Refresh/access tokens ONLY in Windows Credential Manager (`ISecretStore`).
- The desktop Google flow uses Authorization Code + PKCE and normally does not need a client secret. The app accepts an optional `Google:ClientSecret` value for Google OAuth client types that require one; never commit it.

### Local Google OAuth configuration

Configure the client ID using user-secrets:

```powershell
dotnet user-secrets set "Google:ClientId" "<google-client-id>" --project src\E2E.Calendar.App
dotnet user-secrets set "Google:ClientSecret" "<google-client-secret>" --project src\E2E.Calendar.App
```

Alternatively set environment variables before launching:

```powershell
$env:E2E_GOOGLE_CLIENT_ID = "<google-client-id>"
$env:E2E_GOOGLE_CLIENT_SECRET = "<google-client-secret>"
```

`GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are also accepted. The client secret is optional for public PKCE clients and is never logged or persisted.
- Flow per account: authorize -> store tokens -> list calendars -> create `account` + `calendar` rows -> initial full pull -> incremental.
- Offline-first: UI reads SQLite only. SyncEngine pushes `outbox` then pulls deltas. Backoff on 429/5xx (honour `Retry-After`), exponential up to 15 min.
- Poll every 5 min + on app start/resume/network regained/manual. (Push webhooks need a public HTTPS server -> not MVP.)
- Conflict: compare etag/updated; both changed -> last-edit-wins, loser saved as "(conflict copy)".
- Delete = soft delete locally, tombstone pushed; remote delete -> soft delete locally.
- Recurrence: keep RRULE string untouched round-trip; exceptions mapped via recurrence_id.

## Google
- Scopes (narrowest): `https://www.googleapis.com/auth/calendar.events`, `calendar.readonly` (calendar list), optional `contacts.readonly`/`directory`/People for birthdays. Calendar scopes are *sensitive* -> OAuth verification needed for >100 users (privacy policy URL, domain, demo video).
- Use `events.list` with `syncToken`; on HTTP 410 -> drop token, full resync. `singleEvents=false` so RRULE is kept.
- Write: `events.insert/patch/delete` with `If-Match` etag; map `conferenceData` -> meeting link.
- Library: Google.Apis.Calendar.v3 (+ Google.Apis.Auth for PKCE loopback).

## Microsoft
- App registration: "Accounts in any org + personal Microsoft accounts", platform "Mobile and desktop", redirect `http://localhost`.
- Scopes: `Calendars.ReadWrite`, `offline_access`, `User.Read` (+ `Contacts.Read` for birthdays).
- Pull: `GET /me/calendars/{id}/calendarView/delta` (store `deltaLink`); on `syncStateNotFound` -> full resync.
- Write: `POST/PATCH/DELETE /me/events/{id}`; invitations: `/accept`, `/tentativelyAccept`, `/decline`.
- Library: Microsoft.Identity.Client (MSAL) + Microsoft.Graph. Token cache serialised into Credential Manager.

## Testing
Use two throw-away accounts (one Google, one Outlook). Integration tests behind env flag; unit tests with `FakeSyncProvider`. Test: create/edit/delete both directions, offline edit then reconnect, conflict, recurring + exception, 410/delta-reset.
