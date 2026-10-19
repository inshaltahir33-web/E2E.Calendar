using System.Text;
using Dapper;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;
using CalendarDb = E2E.Calendar.Data.Db;

namespace E2E.Calendar.Data.Tests;

public sealed class DatabaseEncryptionTests : IAsyncLifetime
{
    private readonly TempDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task The_database_file_is_not_readable_as_plain_sqlite()
    {
        await using (await _database.Database.OpenAsync())
        {
        }

        SqliteConnection.ClearAllPools();

        var header = await File.ReadAllBytesAsync(_database.DatabasePath);

        header.Length.Should().BeGreaterThan(16);
        Encoding.Latin1.GetString(header, 0, 16).Should().NotBe("SQLite format 3\0");
    }

    [Fact]
    public async Task The_database_cannot_be_read_with_a_different_key()
    {
        var events = new EventRepository(_database.Database);
        await events.UpsertAsync(new CalendarEvent
        {
            Id = "evt-1",
            CalendarId = "cal-local",
            Title = "Secret",
            StartUtc = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero),
            TimeZoneId = "Asia/Kolkata",
            CreatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            Dirty = true,
        });

        var wrongKeyStore = new FakeSecretStore();
        var wrongKeyDatabase = new CalendarDb(_database.DatabasePath, wrongKeyStore);

        var act = async () =>
        {
            await using var connection = await wrongKeyDatabase.OpenAsync();
            await connection.QueryAsync<string>("SELECT title FROM event;");
        };

        await act.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task The_database_can_be_reopened_with_the_stored_key()
    {
        var events = new EventRepository(_database.Database);
        await events.UpsertAsync(new CalendarEvent
        {
            Id = "evt-1",
            CalendarId = "cal-local",
            Title = "Secret",
            StartUtc = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero),
            TimeZoneId = "Asia/Kolkata",
            CreatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            Dirty = true,
        });

        var reopened = new CalendarDb(_database.DatabasePath, _database.SecretStore);
        var reopenedEvents = new EventRepository(reopened);

        var loaded = await reopenedEvents.GetByIdAsync("evt-1");

        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Secret");
    }
}
