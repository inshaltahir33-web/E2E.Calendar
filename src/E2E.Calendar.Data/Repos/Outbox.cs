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
}
