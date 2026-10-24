[v2 · Premium] Spec 5 "Optional own cloud backup/sync between the user's PCs". Prereq: P12, P19. Needs a server.
Write docs/12-CLOUD-BACKEND.md FIRST (referenced in the gaps doc): threat model, API, storage, cost, deletion policy. Stop and show it before coding the server if anything is ambiguous.
1. Zero-knowledge design: client-side AES-256-GCM encryption, key derived from a user passphrase (Argon2id) + random salt; server stores only ciphertext blobs + version numbers. Passphrase lost = data lost (say it clearly in UI). Recovery key export (PDF/text) at setup.
2. Server: ASP.NET Core API + object storage/Postgres, auth by a Microsoft/Google sign-in (OIDC) token only to identify the user, endpoints: PUT/GET /sync/{device}/changes?since=, GET /snapshot, DELETE /account (hard delete everything within 30 days max, instantly for blobs). Quotas per user.
3. Client: ICloudSyncService syncing a change log of local-only data (local calendars, tasks, reminders, habits, settings; NOT tokens or provider-synced events). Merge rule: per-record last-writer-wins with device id tie-break, tombstones, conflict copy for events.
4. Feature.CloudSync (premium only), restore-on-new-PC flow: sign in -> passphrase -> download snapshot.
5. Settings > Privacy & data: status, last sync, "Delete cloud data", "Turn off".
Tests: encryption round-trip + tamper detection, merge of two devices with concurrent edits, delete flow. Commit "feat: encrypted cloud backup (v2)".
