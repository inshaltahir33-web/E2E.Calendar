namespace E2E.Calendar.Data.Repos;

using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

public sealed class CalendarRepository : ICalendarRepository
{
    private const string SelectColumns = """
        id AS Id,
        account_id AS AccountId,
        remote_id AS RemoteId,
        name AS Name,
        color AS Color,
        visible AS Visible,
        read_only AS ReadOnly,
        is_local AS IsLocal,
        sync_token AS SyncToken
        """;

    private const string UpsertSql = """
        INSERT INTO calendar (
            id, account_id, remote_id, name, color, visible, read_only, is_local, sync_token)
        VALUES (
            @Id, @AccountId, @RemoteId, @Name, @Color, @Visible, @ReadOnly, @IsLocal, @SyncToken)
        ON CONFLICT(id) DO UPDATE SET
            account_id = excluded.account_id,
            remote_id = excluded.remote_id,
            name = excluded.name,
            color = excluded.color,
            visible = excluded.visible,
            read_only = excluded.read_only,
            is_local = excluded.is_local,
            sync_token = excluded.sync_token
        """;

    private readonly Db _db;

    public CalendarRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<Calendar>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM calendar ORDER BY name;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<Calendar>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<Calendar?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM calendar WHERE id = @Id;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<Calendar>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(Calendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                UpsertSql,
                new
                {
                    calendar.Id,
                    calendar.AccountId,
                    calendar.RemoteId,
                    calendar.Name,
                    calendar.Color,
                    Visible = calendar.Visible ? 1L : 0L,
                    ReadOnly = calendar.ReadOnly ? 1L : 0L,
                    IsLocal = calendar.IsLocal ? 1L : 0L,
                    calendar.SyncToken,
                },
                cancellationToken: cancellationToken));
    }
}
