1. Sync/Ics: IcsService import (file picker + drag-drop), export (calendar -> .ics), subscription (URL, refresh 1-24h, ETag, read-only calendar). Round-trip tests with RRULE/EXDATE/VALARM.
2. Core/Sync: ISyncProvider, SyncEngine (outbox drain with exponential backoff, pull with sync tokens, ConflictResolver last-edit-wins + conflict copy), network-change trigger, 5-minute timer. Implement with a FakeSyncProvider + tests. Do NOT implement real Google/Graph yet; create empty provider classes with TODO-free interfaces and docs in docs/06-SYNC.md describing OAuth PKCE steps.
3. Platform CredentialStore (ISecretStore) using Windows Credential Manager.
Commit "feat: ics and sync skeleton".
