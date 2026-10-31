using System.Globalization;
using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Repos;

public sealed class EventTemplateRepository : IEventTemplateStore
{
    private const string SelectColumns = """
        id AS Id,
        name AS Name,
        title AS Title,
        duration_minutes AS DurationMinutes,
        calendar_id AS CalendarId,
        location AS Location,
        notes AS Notes,
        alert_offsets AS AlertOffsets,
        created_utc AS CreatedUtc
        """;

    private readonly Db _db;

    public EventTemplateRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<EventTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM event_template ORDER BY name COLLATE NOCASE, created_utc;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TemplateRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(row => row.ToModel()).ToList();
    }

    public async Task SaveAsync(EventTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        const string sql = """
            INSERT INTO event_template (
                id, name, title, duration_minutes, calendar_id, location, notes, alert_offsets, created_utc)
            VALUES (
                @Id, @Name, @Title, @DurationMinutes, @CalendarId, @Location, @Notes, @AlertOffsets, @CreatedUtc)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                title = excluded.title,
                duration_minutes = excluded.duration_minutes,
                calendar_id = excluded.calendar_id,
                location = excluded.location,
                notes = excluded.notes,
                alert_offsets = excluded.alert_offsets;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                template.Id,
                template.Name,
                template.Title,
                template.DurationMinutes,
                template.CalendarId,
                template.Location,
                template.Notes,
                AlertOffsets = FormatOffsets(template.AlertOffsetsMinutes),
                template.CreatedUtc,
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        const string sql = "DELETE FROM event_template WHERE id = @Id;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    private static string? FormatOffsets(IReadOnlyList<int> offsets) =>
        offsets.Count == 0 ? null : string.Join(',', offsets.Select(offset => offset.ToString(CultureInfo.InvariantCulture)));

    private static IReadOnlyList<int> ParseOffsets(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
            .ToList();
    }

    private sealed class TemplateRow
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public string? CalendarId { get; set; }

        public string? Location { get; set; }

        public string? Notes { get; set; }

        public string? AlertOffsets { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public EventTemplate ToModel() => new()
        {
            Id = Id,
            Name = Name,
            Title = Title,
            DurationMinutes = DurationMinutes,
            CalendarId = CalendarId,
            Location = Location,
            Notes = Notes,
            AlertOffsetsMinutes = ParseOffsets(AlertOffsets),
            CreatedUtc = CreatedUtc,
        };
    }
}
