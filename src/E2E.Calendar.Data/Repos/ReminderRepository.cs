using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Types;

namespace E2E.Calendar.Data.Repos;

public sealed class ReminderRepository : IReminderRepository
{
    private const string SelectColumns = """
        id AS Id,
        title AS Title,
        notes AS Notes,
        due_utc AS DueUtc,
        tz_id AS TimeZoneId,
        rrule AS RRule,
        priority AS Priority,
        flagged AS Flagged,
        done_utc AS DoneUtc,
        list_id AS ListId,
        created_utc AS CreatedUtc,
        updated_utc AS UpdatedUtc,
        deleted AS Deleted,
        dirty AS Dirty
        """;

    private const string UpsertSql = """
        INSERT INTO reminder (
            id, title, notes, due_utc, tz_id, rrule, priority, flagged, done_utc,
            list_id, created_utc, updated_utc, deleted, dirty)
        VALUES (
            @Id, @Title, @Notes, @DueUtc, @TimeZoneId, @RRule, @Priority, @Flagged, @DoneUtc,
            @ListId, @CreatedUtc, @UpdatedUtc, @Deleted, @Dirty)
        ON CONFLICT(id) DO UPDATE SET
            title = excluded.title,
            notes = excluded.notes,
            due_utc = excluded.due_utc,
            tz_id = excluded.tz_id,
            rrule = excluded.rrule,
            priority = excluded.priority,
            flagged = excluded.flagged,
            done_utc = excluded.done_utc,
            list_id = excluded.list_id,
            created_utc = excluded.created_utc,
            updated_utc = excluded.updated_utc,
            deleted = excluded.deleted,
            dirty = excluded.dirty
        """;

    private readonly Db _db;

    public ReminderRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Reminder?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM reminder WHERE id = @Id;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<Reminder>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Reminder>> GetInRangeAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM reminder
            WHERE deleted = 0 AND due_utc IS NOT NULL AND due_utc >= @From AND due_utc < @To
            ORDER BY due_utc;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<Reminder>(
            new CommandDefinition(
                sql,
                new
                {
                    From = SqliteTime.ToUtcText(fromUtc),
                    To = SqliteTime.ToUtcText(toUtc),
                },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<Reminder>> GetDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM reminder
            WHERE deleted = 0 AND done_utc IS NULL AND due_utc IS NOT NULL AND due_utc <= @Now
            ORDER BY due_utc;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<Reminder>(
            new CommandDefinition(
                sql,
                new { Now = SqliteTime.ToUtcText(nowUtc) },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task UpsertAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(UpsertSql, ReminderRow.FromModel(reminder), cancellationToken: cancellationToken));
    }
}
