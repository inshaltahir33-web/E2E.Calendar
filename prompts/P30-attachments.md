[v1.1] Spec 2.2. Prereq: P06 (editor), P13/P14 (sync).
1. Migration: attachment(id,event_id,name,mime,size,local_path,remote_url,remote_id,created). Files copied into %LocalAppData%/<app>/attachments/<event_id>/ (never modify originals). Limit 10 MB each / 25 MB per event, clear error text.
2. Event editor: "Attach file" button + drag-drop zone, list with icon, size, open, remove. Open uses Launcher.LaunchFileAsync. Sanitize file names (no path traversal).
3. Sync: Google = Drive file links (attachments[] fileUrl, title) - link only, do not upload binary in this prompt. Microsoft Graph = fileAttachment (<3 MB inline base64) else show "too large, link only". Remote link attachments appear as "Open link" items.
4. Delete-all-data and delete event must delete the files too. Export ICS writes ATTACH;FMTTYPE only for URL attachments.
5. Never log file names or paths.
Tests: copy/limit/traversal, delete cascade, ICS round-trip of URL attachment. Commit "feat: event attachments".
