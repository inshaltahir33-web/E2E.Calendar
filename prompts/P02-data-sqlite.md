Implement E2E.Calendar.Data per docs/02-ARCHITECTURE.md §3.
- Migrations/001_init.sql with all tables, indexes, FTS5 table; migration runner using PRAGMA user_version.
- Db factory: Microsoft.Data.Sqlite with SQLCipher bundle; key provider interface ISecretStore (generate 256-bit key once, store via ISecretStore); WAL mode; foreign keys on.
- Dapper repositories implementing Core interfaces; parameterised SQL only; range queries use ix_event_range.
- Data.Tests: temp-file DB, CRUD tests, and a benchmark test inserting 10,000 events and querying a month range (assert < 50 ms).
Build + test. Commit "feat(data): sqlite repos and migrations".
