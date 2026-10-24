[v1.1] Spec 4. Prereq: P10 (tasks), P13, P14.
Extend docs/06-SYNC.md with a "Tasks" section first.
1. Google Tasks API (scope .../auth/tasks): tasklists.list, tasks.list with updatedMin + showCompleted/showDeleted, insert/patch/delete. Google has date-only due: store time locally and keep it local-only (document this limitation in UI tooltip).
2. Microsoft To Do via Graph (Tasks.ReadWrite): /me/todo/lists, /lists/{id}/tasks/delta, checklistItems for sub-tasks, dueDateTime + reminderDateTime, recurrence (map to internal RRULE where possible, else read-only).
3. Map each remote task list to a local task list with an "account" badge. Free tier = 1 list rule from P19 still applies to local lists; synced lists count against the connected-account limit instead.
4. Same outbox/pull pattern, conflict rule and status icon as event sync. Completed tasks sync both ways.
5. Settings > Accounts: per-account toggle "Sync tasks".
Tests: FakeHttp for delta/updatedMin, completion round-trip, sub-task mapping, offline edit. Commit "feat(sync): tasks google and microsoft".
