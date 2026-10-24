using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Sync;
using E2E.Calendar.Data.Types;

namespace E2E.Calendar.Data.Repos;

public sealed class Outbox : IOutbox
{
    private const string EnqueueSql = """
        INSERT INTO outbox (entity, entity_id, op, payload, attempts, next_try_utc)
        VALUES (@Entity, @EntityId, @Op, @Payload, @Attempts, @NextTryUtc);
        """;

    private readonly Db _db;

    public Outbox(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task EnqueueAsync(OutboxItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                EnqueueSql,
                new
                {
                    item.Entity,
                    item.EntityId,
                    Op = EnumText.ToText(item.Op),
                    item.Payload,
                    item.Attempts,
                    NextTryUtc = item.NextTryUtc is null ? null : SqliteTime.ToUtcText(item.NextTryUtc.Value),
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<OutboxItem>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using var connection = await _db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<OutboxItem>(
            new CommandDefinition(
                "SELECT id AS Id, entity AS Entity, entity_id AS EntityId, op AS Op, payload AS Payload, attempts AS Attempts, next_try_utc AS NextTryUtc FROM outbox WHERE next_try_utc IS NULL OR next_try_utc <= @Now ORDER BY id LIMIT @Limit;",
                new { Now = SqliteTime.ToUtcText(DateTimeOffset.UtcNow), Limit = limit },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task RemoveAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM outbox WHERE id = @Id;",
            new { Id = id },
            cancellationToken: cancellationToken));
    }
}
