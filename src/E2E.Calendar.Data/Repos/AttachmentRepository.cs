using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Types;

namespace E2E.Calendar.Data.Repos;

public sealed class AttachmentRepository : IAttachmentRepository
{
    private readonly Db _db;

    public AttachmentRepository(Db db) => _db = db;

    public async Task<IReadOnlyList<EventAttachment>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<EventAttachment>(
            new CommandDefinition(
                "SELECT id AS Id, event_id AS EventId, file_path AS FilePath, display_name AS DisplayName, content_type AS ContentType, size_bytes AS SizeBytes, added_utc AS AddedUtc FROM event_attachment WHERE event_id = @EventId ORDER BY display_name;",
                new { EventId = eventId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task UpsertAsync(EventAttachment attachment, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO event_attachment (id, event_id, file_path, display_name, content_type, size_bytes, added_utc)
            VALUES (@Id, @EventId, @FilePath, @DisplayName, @ContentType, @SizeBytes, @AddedUtc)
            ON CONFLICT(id) DO UPDATE SET file_path = excluded.file_path, display_name = excluded.display_name,
                content_type = excluded.content_type, size_bytes = excluded.size_bytes;
            """,
            new
            {
                attachment.Id,
                attachment.EventId,
                attachment.FilePath,
                attachment.DisplayName,
                attachment.ContentType,
                attachment.SizeBytes,
                AddedUtc = SqliteTime.ToUtcText(attachment.AddedUtc),
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM event_attachment WHERE id = @Id;",
            new { Id = id },
            cancellationToken: cancellationToken));
    }
}
