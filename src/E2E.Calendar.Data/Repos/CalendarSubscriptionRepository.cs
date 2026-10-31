using System.Globalization;
using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Repos;

public sealed class CalendarSubscriptionRepository : ICalendarSubscriptionRepository
{
    private const string SelectColumns = """
        calendar_id AS CalendarId,
        url AS Url,
        refresh_hours AS RefreshHours,
        etag AS ETag,
        last_modified_utc AS LastModifiedUtc,
        last_refresh_utc AS LastRefreshUtc
        """;

    private readonly Db _db;

    public CalendarSubscriptionRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<CalendarSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<SubscriptionRow>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM calendar_subscription ORDER BY calendar_id;",
                cancellationToken: cancellationToken));

        return rows.Select(row => row.ToModel()).ToArray();
    }

    public async Task<CalendarSubscription?> GetAsync(string calendarId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        var row = await connection.QueryFirstOrDefaultAsync<SubscriptionRow>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM calendar_subscription WHERE calendar_id = @CalendarId;",
                new { CalendarId = calendarId },
                cancellationToken: cancellationToken));

        return row?.ToModel();
    }

    public async Task UpsertAsync(CalendarSubscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO calendar_subscription (
                calendar_id, url, refresh_hours, etag, last_modified_utc, last_refresh_utc)
            VALUES (
                @CalendarId, @Url, @RefreshHours, @ETag, @LastModifiedUtc, @LastRefreshUtc)
            ON CONFLICT(calendar_id) DO UPDATE SET
                url = excluded.url,
                refresh_hours = excluded.refresh_hours,
                etag = excluded.etag,
                last_modified_utc = excluded.last_modified_utc,
                last_refresh_utc = excluded.last_refresh_utc;
            """,
            new
            {
                subscription.CalendarId,
                subscription.Url,
                subscription.RefreshHours,
                subscription.ETag,
                LastModifiedUtc = ToUtcText(subscription.LastModifiedUtc),
                LastRefreshUtc = ToUtcText(subscription.LastRefreshUtc),
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string calendarId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM calendar_subscription WHERE calendar_id = @CalendarId;",
            new { CalendarId = calendarId },
            cancellationToken: cancellationToken));
    }

    private static string? ToUtcText(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseUtc(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var timestamp)
            ? timestamp
            : null;

    private sealed class SubscriptionRow
    {
        public required string CalendarId { get; init; }

        public required string Url { get; init; }

        public int RefreshHours { get; init; }

        public string? ETag { get; init; }

        public string? LastModifiedUtc { get; init; }

        public string? LastRefreshUtc { get; init; }

        public CalendarSubscription ToModel() => new()
        {
            CalendarId = CalendarId,
            Url = Url,
            RefreshHours = RefreshHours,
            ETag = ETag,
            LastModifiedUtc = ParseUtc(LastModifiedUtc),
            LastRefreshUtc = ParseUtc(LastRefreshUtc),
        };
    }
}
