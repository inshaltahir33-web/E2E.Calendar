CREATE TABLE IF NOT EXISTS event_template(
  id TEXT PRIMARY KEY NOT NULL,
  name TEXT NOT NULL,
  title TEXT NOT NULL,
  duration_minutes INT NOT NULL DEFAULT 60,
  calendar_id TEXT NULL,
  location TEXT NULL,
  notes TEXT NULL,
  alert_offsets TEXT NULL,
  created_utc TEXT NOT NULL
);
