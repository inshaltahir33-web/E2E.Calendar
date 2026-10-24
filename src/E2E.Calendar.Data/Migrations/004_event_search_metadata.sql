ALTER TABLE event ADD COLUMN tags TEXT;
ALTER TABLE event ADD COLUMN category TEXT;

CREATE INDEX IF NOT EXISTS ix_event_search_dates ON event(start_utc, end_utc, deleted);
CREATE INDEX IF NOT EXISTS ix_event_search_category ON event(category);
