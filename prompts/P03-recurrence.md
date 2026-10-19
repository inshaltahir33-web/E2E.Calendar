Implement Core/Recurrence/RecurrenceExpander using Ical.Net per docs/02-ARCHITECTURE.md §4.
API: IEnumerable<Occurrence> Expand(CalendarEvent master, IEnumerable<CalendarEvent> exceptions, DateTimeOffset from, DateTimeOffset to).
Support daily/weekly(days)/monthly(date and "2nd Tuesday")/yearly/every N, COUNT/UNTIL, EXDATE, edited single occurrences, and "this and following" split helper (SplitSeries).
DST-correct: expand in event tz_id then convert to UTC.
Write >=15 xUnit tests including DST spring-forward, month-end (31st), leap day, EXDATE, exception override.
Add an EventQueryService that merges non-recurring + expanded recurring for a range with an LRU cache invalidated on writes.
Commit "feat(core): recurrence engine".
