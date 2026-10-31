using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Types;

namespace E2E.Calendar.Data.Repos;

public sealed class AlertRepository : IAlertRepository
{
    private const string AlertColumns = """
        id AS Id,
        owner_type AS OwnerType,
        owner_id AS OwnerId,
        trigger_offset_min AS TriggerOffsetMinutes,
        trigger_at_utc AS TriggerAtUtc,
        priority AS Priority,
        persistent AS Persistent,
        repeat_every_min AS RepeatEveryMinutes,
        sound AS Sound
        """;

    private const string InstanceColumns = """
        id AS Id,
        alert_id AS AlertId,
        owner_type AS OwnerType,
        owner_id AS OwnerId,
        occurrence_utc AS OccurrenceUtc,
        fire_at_utc AS FireAtUtc,
        state AS State,
        snoozed_until_utc AS SnoozedUntilUtc,
        fired_utc AS FiredUtc,
        repeat_count AS RepeatCount
        """;

    private const string UpsertAlertSql = """
        INSERT INTO alert (
            id, owner_type, owner_id, trigger_offset_min, trigger_at_utc,
            priority, persistent, repeat_every_min, sound)
        VALUES (
            @Id, @OwnerType, @OwnerId, @TriggerOffsetMinutes, @TriggerAtUtc,
            @Priority, @Persistent, @RepeatEveryMinutes, @Sound)
        ON CONFLICT(id) DO UPDATE SET
            owner_type = excluded.owner_type,
            owner_id = excluded.owner_id,
            trigger_offset_min = excluded.trigger_offset_min,
            trigger_at_utc = excluded.trigger_at_utc,
            priority = excluded.priority,
            persistent = excluded.persistent,
            repeat_every_min = excluded.repeat_every_min,
            sound = excluded.sound
        """;

    private const string UpsertInstanceSql = """
        INSERT INTO alert_instance (
            id, alert_id, owner_type, owner_id, occurrence_utc, fire_at_utc,
            state, snoozed_until_utc, fired_utc, repeat_count)
        VALUES (
            @Id, @AlertId, @OwnerType, @OwnerId, @OccurrenceUtc, @FireAtUtc,
            @State, @SnoozedUntilUtc, @FiredUtc, @RepeatCount)
        ON CONFLICT(id) DO UPDATE SET
            alert_id = excluded.alert_id,
            owner_type = excluded.owner_type,
            owner_id = excluded.owner_id,
            occurrence_utc = excluded.occurrence_utc,
            fire_at_utc = excluded.fire_at_utc,
            snoozed_until_utc = CASE
                WHEN alert_instance.state IN ('pending', 'snoozed')
                     AND alert_instance.fire_at_utc <> excluded.fire_at_utc THEN NULL
                ELSE alert_instance.snoozed_until_utc
            END,
            state = CASE
                WHEN alert_instance.state IN ('pending', 'snoozed')
                     AND alert_instance.fire_at_utc <> excluded.fire_at_utc THEN 'pending'
                ELSE alert_instance.state
            END
        """;

    private readonly Db _db;

    public AlertRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Alert?> GetAlertByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {AlertColumns} FROM alert WHERE id = @Id;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<Alert>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Alert>> GetAlertsForOwnerAsync(
        OwnerType ownerType,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {AlertColumns}
            FROM alert
            WHERE owner_type = @OwnerType AND owner_id = @OwnerId
            ORDER BY id;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<Alert>(
            new CommandDefinition(
                sql,
                new { OwnerType = EnumText.ToText(ownerType), OwnerId = ownerId },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<Alert>> GetAllAlertsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<Alert>(
            new CommandDefinition(
                $"SELECT {AlertColumns} FROM alert ORDER BY id;",
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task UpsertAlertAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(UpsertAlertSql, AlertRow.FromModel(alert), cancellationToken: cancellationToken));
    }

    public async Task DeleteAlertAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM alert WHERE id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<AlertInstance?> GetInstanceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {InstanceColumns} FROM alert_instance WHERE id = @Id;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<AlertInstance>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task UpsertInstanceAsync(AlertInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(UpsertInstanceSql, AlertInstanceRow.FromModel(instance), cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AlertInstance>> GetPendingInstancesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {InstanceColumns}
            FROM alert_instance
            WHERE state = @State AND fire_at_utc <= @Now
            ORDER BY fire_at_utc;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AlertInstance>(
            new CommandDefinition(
                sql,
                new { State = EnumText.ToText(AlertState.Pending), Now = SqliteTime.ToUtcText(nowUtc) },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<AlertInstance>> GetPendingInstancesForSchedulingAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        await using var connection = await _db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AlertInstance>(
            new CommandDefinition(
                $"""
                SELECT {InstanceColumns}
                FROM alert_instance
                WHERE state = @State
                ORDER BY fire_at_utc
                LIMIT @Limit;
                """,
                new { State = EnumText.ToText(AlertState.Pending), Limit = limit },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<bool> TryMarkFiredAsync(
        string instanceId,
        DateTimeOffset firedUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE alert_instance
                SET state = @Fired, fired_utc = @FiredUtc
                WHERE id = @Id AND state = @Pending;
                """,
                new
                {
                    Id = instanceId,
                    Pending = EnumText.ToText(AlertState.Pending),
                    Fired = EnumText.ToText(AlertState.Fired),
                    FiredUtc = SqliteTime.ToUtcText(firedUtc),
                },
                cancellationToken: cancellationToken));
        return affected == 1;
    }

    public async Task MarkMissedAsync(
        string instanceId,
        DateTimeOffset missedUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE alert_instance
                SET state = @Missed, fired_utc = @MissedUtc
                WHERE id = @Id AND state = @Pending;
                """,
                new
                {
                    Id = instanceId,
                    Pending = EnumText.ToText(AlertState.Pending),
                    Missed = EnumText.ToText(AlertState.Missed),
                    MissedUtc = SqliteTime.ToUtcText(missedUtc),
                },
                cancellationToken: cancellationToken));
    }

    public async Task MarkSnoozedAsync(
        string instanceId,
        DateTimeOffset snoozedUntilUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE alert_instance
                SET state = @Snoozed, snoozed_until_utc = @SnoozedUntil
                WHERE id = @Id AND state IN (@Pending, @Fired);
                """,
                new
                {
                    Id = instanceId,
                    Pending = EnumText.ToText(AlertState.Pending),
                    Fired = EnumText.ToText(AlertState.Fired),
                    Snoozed = EnumText.ToText(AlertState.Snoozed),
                    SnoozedUntil = SqliteTime.ToUtcText(snoozedUntilUtc),
                },
                cancellationToken: cancellationToken));
    }

    public async Task MarkDoneAsync(
        string instanceId,
        DateTimeOffset doneUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE alert_instance
                SET state = @Done, fired_utc = @DoneUtc
                WHERE id = @Id AND state IN (@Pending, @Fired, @Snoozed);
                """,
                new
                {
                    Id = instanceId,
                    Pending = EnumText.ToText(AlertState.Pending),
                    Fired = EnumText.ToText(AlertState.Fired),
                    Snoozed = EnumText.ToText(AlertState.Snoozed),
                    Done = EnumText.ToText(AlertState.Done),
                    DoneUtc = SqliteTime.ToUtcText(doneUtc),
                },
                cancellationToken: cancellationToken));
    }

    public async Task DeleteStalePendingInstancesAsync(
        string alertId,
        IReadOnlyList<string> keepInstanceIds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);

        // Snoozed copies carry snoozed_until_utc and are managed by the snooze flow,
        // never by the planner, so they are never treated as stale. Repeat instances
        // carry repeat_count > 0 and belong to the persistent chain, not the planner.
        if (keepInstanceIds.Count == 0)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    DELETE FROM alert_instance
                    WHERE alert_id = @AlertId AND state = @Pending AND snoozed_until_utc IS NULL
                      AND repeat_count = 0;
                    """,
                    new { AlertId = alertId, Pending = EnumText.ToText(AlertState.Pending) },
                    cancellationToken: cancellationToken));
            return;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM alert_instance
                WHERE alert_id = @AlertId AND state = @Pending AND snoozed_until_utc IS NULL
                  AND repeat_count = 0 AND id NOT IN @Ids;
                """,
                new
                {
                    AlertId = alertId,
                    Pending = EnumText.ToText(AlertState.Pending),
                    Ids = keepInstanceIds,
                },
                cancellationToken: cancellationToken));
    }

    public async Task CancelPendingRepeatsAsync(
        string alertId,
        DateTimeOffset occurrenceUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM alert_instance
                WHERE alert_id = @AlertId AND occurrence_utc = @Occurrence
                  AND state = @Pending AND repeat_count > 0;
                """,
                new
                {
                    AlertId = alertId,
                    Occurrence = SqliteTime.ToUtcText(occurrenceUtc),
                    Pending = EnumText.ToText(AlertState.Pending),
                },
                cancellationToken: cancellationToken));
    }

    public async Task<AlertInstance?> PeekNextPendingInstanceAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {InstanceColumns}
            FROM alert_instance
            WHERE state = @State
            ORDER BY fire_at_utc
            LIMIT 1;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<AlertInstance>(
            new CommandDefinition(
                sql,
                new { State = EnumText.ToText(AlertState.Pending) },
                cancellationToken: cancellationToken));
    }
}
