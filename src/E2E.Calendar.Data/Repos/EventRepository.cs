using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Types;

namespace E2E.Calendar.Data.Repos;

public sealed class EventRepository : IEventRepository
{
    private const string SelectColumns = """
        id AS Id,
        calendar_id AS CalendarId,
        remote_id AS RemoteId,
        etag AS ETag,
        title AS Title,
        notes AS Notes,
        location AS Location,
        url AS Url,
        start_utc AS StartUtc,
        end_utc AS EndUtc,
        all_day AS AllDay,
        tz_id AS TimeZoneId,
        rrule AS RRule,
        exdates AS ExDates,
        recurrence_master_id AS RecurrenceMasterId,
        recurrence_id_utc AS RecurrenceIdUtc,
        color AS Color,
        meeting_url AS MeetingUrl,
        attendees AS Attendees,
        tags AS Tags,
        category AS Category,
        travel_minutes AS TravelMinutes,
        created_utc AS CreatedUtc,
        updated_utc AS UpdatedUtc,
        deleted AS Deleted,
        dirty AS Dirty
        """;

    private const string UpsertSql = """
        INSERT INTO event (
            id, calendar_id, remote_id, etag, title, notes, location, url,
            start_utc, end_utc, all_day, tz_id, rrule, exdates,
            recurrence_master_id, recurrence_id_utc, color, meeting_url, attendees, tags, category, travel_minutes,
            created_utc, updated_utc, deleted, dirty)
        VALUES (
            @Id, @CalendarId, @RemoteId, @ETag, @Title, @Notes, @Location, @Url,
            @StartUtc, @EndUtc, @AllDay, @TimeZoneId, @RRule, @ExDates,
            @RecurrenceMasterId, @RecurrenceIdUtc, @Color, @MeetingUrl, @Attendees, @Tags, @Category, @TravelMinutes,
            @CreatedUtc, @UpdatedUtc, @Deleted, @Dirty)
        ON CONFLICT(id) DO UPDATE SET
            calendar_id = excluded.calendar_id,
            remote_id = excluded.remote_id,
            etag = excluded.etag,
            title = excluded.title,
            notes = excluded.notes,
            location = excluded.location,
            url = excluded.url,
            start_utc = excluded.start_utc,
            end_utc = excluded.end_utc,
            all_day = excluded.all_day,
            tz_id = excluded.tz_id,
            rrule = excluded.rrule,
            exdates = excluded.exdates,
            recurrence_master_id = excluded.recurrence_master_id,
            recurrence_id_utc = excluded.recurrence_id_utc,
            color = excluded.color,
            meeting_url = excluded.meeting_url,
            attendees = excluded.attendees,
            tags = excluded.tags,
            category = excluded.category,
            travel_minutes = excluded.travel_minutes,
            created_utc = excluded.created_utc,
            updated_utc = excluded.updated_utc,
            deleted = excluded.deleted,
            dirty = excluded.dirty
        """;

    private const string InsertManySql = """
        INSERT INTO event (
            id, calendar_id, remote_id, etag, title, notes, location, url,
            start_utc, end_utc, all_day, tz_id, rrule, exdates,
            recurrence_master_id, recurrence_id_utc, color, meeting_url, attendees, tags, category, travel_minutes,
            created_utc, updated_utc, deleted, dirty)
        VALUES (
            @Id, @CalendarId, @RemoteId, @ETag, @Title, @Notes, @Location, @Url,
            @StartUtc, @EndUtc, @AllDay, @TimeZoneId, @RRule, @ExDates,
            @RecurrenceMasterId, @RecurrenceIdUtc, @Color, @MeetingUrl, @Attendees, @Tags, @Category, @TravelMinutes,
            @CreatedUtc, @UpdatedUtc, @Deleted, @Dirty)
        """;

    private readonly Db _db;

    private const int InsertBatchSize = 40;

    public EventRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<CalendarEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM event WHERE id = @Id;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<CalendarEvent>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetInRangeAsync(
        string? calendarId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var calendarFilter = calendarId is null ? string.Empty : "AND calendar_id = @CalendarId";

        var sql = $"""
            SELECT {SelectColumns}
            FROM event
            WHERE deleted = 0
              AND ((rrule IS NULL AND start_utc < @To AND end_utc > @From)
                   OR (rrule IS NOT NULL AND start_utc < @To))
              {calendarFilter}
            ORDER BY start_utc;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CalendarEvent>(
            new CommandDefinition(
                sql,
                new
                {
                    From = SqliteTime.ToUtcText(fromUtc),
                    To = SqliteTime.ToUtcText(toUtc),
                    CalendarId = calendarId,
                },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<CalendarEvent>> SearchAsync(
        EventSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var predicates = new List<string> { "deleted = 0" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            predicates.Add("(title LIKE @Text OR notes LIKE @Text OR location LIKE @Text OR tags LIKE @Text OR category LIKE @Text)");
            parameters.Add("Text", $"%{criteria.Text.Trim()}%");
        }

        if (criteria.FromUtc is not null)
        {
            predicates.Add("end_utc > @From");
            parameters.Add("From", SqliteTime.ToUtcText(criteria.FromUtc.Value));
        }

        if (criteria.ToUtc is not null)
        {
            predicates.Add("start_utc < @To");
            parameters.Add("To", SqliteTime.ToUtcText(criteria.ToUtc.Value));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            predicates.Add("lower(category) = lower(@Category)");
            parameters.Add("Category", criteria.Category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(criteria.CalendarId))
        {
            predicates.Add("calendar_id = @CalendarId");
            parameters.Add("CalendarId", criteria.CalendarId.Trim());
        }

        var tagIndex = 0;
        foreach (var tag in criteria.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
        {
            var parameterName = $"Tag{tagIndex++}";
            predicates.Add($"(',' || replace(lower(coalesce(tags, '')), ' ', '') || ',') LIKE @_{parameterName}");
            parameters.Add($"_{parameterName}", $"%,{tag.Trim().ToLowerInvariant()},%");
        }

        var sql = $"""
            SELECT {SelectColumns}
            FROM event
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY start_utc, title;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CalendarEvent>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetSoftDeletedAsync(
        DateTimeOffset deletedBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM event
            WHERE deleted = 1 AND updated_utc <= @DeletedBefore
            ORDER BY updated_utc;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CalendarEvent>(
            new CommandDefinition(
                sql,
                new { DeletedBefore = SqliteTime.ToUtcText(deletedBeforeUtc) },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetExceptionsAsync(
        string masterId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM event
            WHERE recurrence_master_id = @MasterId
              AND recurrence_id_utc >= @From AND recurrence_id_utc < @To
            ORDER BY recurrence_id_utc;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CalendarEvent>(
            new CommandDefinition(
                sql,
                new
                {
                    MasterId = masterId,
                    From = SqliteTime.ToUtcText(fromUtc),
                    To = SqliteTime.ToUtcText(toUtc),
                },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(UpsertSql, EventRow.FromModel(calendarEvent), cancellationToken: cancellationToken));
    }

    public async Task InsertManyAsync(IReadOnlyList<CalendarEvent> calendarEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvents);

        if (calendarEvents.Count == 0)
        {
            return;
        }

        await using var connection = await _db.OpenAsync(cancellationToken);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var rows = calendarEvents.Select(EventRow.FromModel).ToList();

        foreach (var batch in rows.Chunk(InsertBatchSize))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(InsertManySql, batch, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteByCalendarIdsAsync(IReadOnlyList<string> calendarIds, CancellationToken cancellationToken = default)
    {
        if (calendarIds.Count == 0)
        {
            return;
        }

        await using var connection = await _db.OpenAsync(cancellationToken);
        var names = calendarIds.Select((_, index) => $"@Calendar{index}").ToArray();
        var parameters = new DynamicParameters();
        for (var index = 0; index < calendarIds.Count; index++)
        {
            parameters.Add($"Calendar{index}", calendarIds[index]);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM event WHERE calendar_id IN ({string.Join(", ", names)});",
            parameters,
            cancellationToken: cancellationToken));
    }
}
