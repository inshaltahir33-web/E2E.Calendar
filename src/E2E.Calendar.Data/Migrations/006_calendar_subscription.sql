CREATE TABLE IF NOT EXISTS calendar_subscription(
  calendar_id TEXT PRIMARY KEY NOT NULL,
  url TEXT NOT NULL,
  refresh_hours INT NOT NULL DEFAULT 6,
  etag TEXT NULL,
  last_modified_utc TEXT NULL,
  last_refresh_utc TEXT NULL
);
