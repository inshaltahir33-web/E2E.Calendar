using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Types;

namespace E2E.Calendar.Data.Repos;

public sealed class TaskRepository : ITaskRepository
{
    private const string SelectColumns = """
        id AS Id,
        list_id AS ListId,
        title AS Title,
        notes AS Notes,
        due_utc AS DueUtc,
        has_time AS HasTime,
        priority AS Priority,
        rrule AS RRule,
        done_utc AS DoneUtc,
        parent_id AS ParentId,
        linked_event_id AS LinkedEventId,
        dirty AS Dirty,
        deleted AS Deleted
        """;

    private const string UpsertSql = """
        INSERT INTO task (
            id, list_id, title, notes, due_utc, has_time, priority, rrule, done_utc,
            parent_id, linked_event_id, dirty, deleted)
        VALUES (
            @Id, @ListId, @Title, @Notes, @DueUtc, @HasTime, @Priority, @RRule, @DoneUtc,
            @ParentId, @LinkedEventId, @Dirty, @Deleted)
        ON CONFLICT(id) DO UPDATE SET
            list_id = excluded.list_id,
            title = excluded.title,
            notes = excluded.notes,
            due_utc = excluded.due_utc,
            has_time = excluded.has_time,
            priority = excluded.priority,
            rrule = excluded.rrule,
            done_utc = excluded.done_utc,
            parent_id = excluded.parent_id,
            linked_event_id = excluded.linked_event_id,
            dirty = excluded.dirty,
            deleted = excluded.deleted
        """;

    private readonly Db _db;

    public TaskRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<TaskItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM task WHERE id = @Id;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<TaskItem>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM task WHERE deleted = 0 ORDER BY title;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TaskItem>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<TaskItem>> GetByListAsync(string listId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM task WHERE deleted = 0 AND list_id = @ListId ORDER BY title;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TaskItem>(
            new CommandDefinition(sql, new { ListId = listId }, cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<TaskList>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id AS Id, name AS Name, color AS Color, remote_id AS RemoteId FROM task_list ORDER BY name;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TaskList>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task UpsertAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(UpsertSql, TaskRow.FromModel(task), cancellationToken: cancellationToken));
    }
}
