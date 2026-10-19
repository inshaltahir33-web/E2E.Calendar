using Dapper;
using E2E.Calendar.Data.Migrations;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class MigrationTests : IAsyncLifetime
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
    public async Task Migration_creates_every_table_of_the_architecture()
    {
        await using var connection = await _database.Database.OpenAsync();

        var tables = (await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;")).ToList();

        tables.Should().Contain(
        [
            "account",
            "alert",
            "alert_instance",
            "calendar",
            "event",
            "event_fts",
            "outbox",
            "reminder",
            "settings",
            "task",
            "task_list",
        ]);
    }

    [Fact]
    public async Task Migration_creates_the_documented_indexes()
    {
        await using var connection = await _database.Database.OpenAsync();

        var indexes = (await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'index' AND name LIKE 'ix_%' ORDER BY name;")).ToList();

        indexes.Should().Contain(["ix_ai_fire", "ix_event_range"]);
    }

    [Fact]
    public async Task Migration_creates_the_fts5_search_table()
    {
        await using var connection = await _database.Database.OpenAsync();

        var sql = (await connection.QueryAsync<string>(
            "SELECT sql FROM sqlite_master WHERE name = 'event_fts';")).Single();

        sql.Should().Contain("fts5");
        sql.Should().Contain("title");
        sql.Should().Contain("attendees");
    }

    [Fact]
    public async Task Migration_records_the_applied_version_in_user_version()
    {
        await using var connection = await _database.Database.OpenAsync();

        var version = await MigrationRunner.GetUserVersionAsync(connection);

        version.Should().Be(MigrationRunner.Migrations.Max(m => m.Version));
        version.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Migrating_an_already_migrated_database_is_a_no_op()
    {
        await _database.Database.MigrateAsync();

        await using var connection = await _database.Database.OpenAsync();
        var version = await MigrationRunner.GetUserVersionAsync(connection);

        version.Should().Be(MigrationRunner.Migrations.Max(m => m.Version));
    }

    [Fact]
    public void The_init_migration_is_the_first_migration()
    {
        MigrationRunner.Migrations.Should().NotBeEmpty();
        MigrationRunner.Migrations[0].Version.Should().Be(1);
        MigrationRunner.Migrations[0].Name.Should().Be("init");
    }
}
