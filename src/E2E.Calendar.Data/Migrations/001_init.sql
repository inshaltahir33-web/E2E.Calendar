CREATE TABLE account(
  id TEXT PRIMARY KEY NOT NULL,
  provider TEXT,
  display_name TEXT,
  email TEXT,
  sync_token TEXT,
  last_sync_utc TEXT
);

CREATE TABLE calendar(
  id TEXT PRIMARY KEY NOT NULL,
  account_id TEXT NULL,
  remote_id TEXT,
  name TEXT,
  color TEXT,
  visible INT DEFAULT 1,
  read_only INT DEFAULT 0,
  is_local INT,
  sync_token TEXT
);

CREATE TABLE event(
  id TEXT PRIMARY KEY NOT NULL,
  calendar_id TEXT,
  remote_id TEXT,
  etag TEXT,
  title TEXT,
  notes TEXT,
  location TEXT,
  url TEXT,
  start_utc TEXT,
  end_utc TEXT,
  all_day INT,
  tz_id TEXT,
  rrule TEXT NULL,
  exdates TEXT NULL,
  recurrence_master_id TEXT NULL,
  recurrence_id_utc TEXT NULL,
  color TEXT,
  meeting_url TEXT,
  travel_minutes INT,
  created_utc TEXT,
  updated_utc TEXT,
  deleted INT DEFAULT 0,
  dirty INT DEFAULT 0
);

CREATE INDEX ix_event_range ON event(calendar_id, start_utc, end_utc);

CREATE TABLE alert(
  id TEXT PRIMARY KEY NOT NULL,
  owner_type TEXT,
  owner_id TEXT,
  trigger_offset_min INT NULL,
  trigger_at_utc TEXT NULL,
  priority INT,
  persistent INT,
  repeat_every_min INT,
  sound TEXT
);

CREATE TABLE reminder(
  id TEXT PRIMARY KEY NOT NULL,
  title TEXT,
  notes TEXT,
  due_utc TEXT,
  tz_id TEXT,
  rrule TEXT,
  priority INT,
  flagged INT,
  done_utc TEXT NULL,
  list_id TEXT NULL,
  created_utc TEXT,
  updated_utc TEXT,
  deleted INT,
  dirty INT
);

CREATE TABLE task_list(
  id TEXT PRIMARY KEY NOT NULL,
  name TEXT,
  color TEXT,
  remote_id TEXT
);

CREATE TABLE task(
  id TEXT PRIMARY KEY NOT NULL,
  list_id TEXT,
  title TEXT,
  notes TEXT,
  due_utc TEXT NULL,
  has_time INT,
  priority INT,
  rrule TEXT,
  done_utc TEXT NULL,
  parent_id TEXT NULL,
  linked_event_id TEXT NULL,
  dirty INT,
  deleted INT
);

CREATE TABLE alert_instance(
  id TEXT PRIMARY KEY NOT NULL,
  alert_id TEXT,
  owner_type TEXT,
  owner_id TEXT,
  occurrence_utc TEXT,
  fire_at_utc TEXT,
  state TEXT,
  snoozed_until_utc TEXT NULL,
  fired_utc TEXT NULL,
  repeat_count INT DEFAULT 0
);

CREATE INDEX ix_ai_fire ON alert_instance(state, fire_at_utc);

CREATE TABLE outbox(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  entity TEXT,
  entity_id TEXT,
  op TEXT,
  payload TEXT,
  attempts INT,
  next_try_utc TEXT
);

CREATE TABLE settings(
  key TEXT PRIMARY KEY NOT NULL,
  value TEXT
);

CREATE VIRTUAL TABLE event_fts USING fts5(title, notes, location, attendees, content='');
