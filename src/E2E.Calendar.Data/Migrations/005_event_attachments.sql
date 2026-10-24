CREATE TABLE IF NOT EXISTS event_attachment (
    id TEXT PRIMARY KEY,
    event_id TEXT NOT NULL,
    file_path TEXT NOT NULL,
    display_name TEXT NOT NULL,
    content_type TEXT NULL,
    size_bytes INTEGER NULL,
    added_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_event_attachment_event ON event_attachment(event_id);
