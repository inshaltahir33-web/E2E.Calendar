using Dapper;
using E2E.Calendar.Core.Sync;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class OutboxTests : IAsyncLifetime
{
    private readonly TempDatabase _database = new();
    private Outbox _outbox = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _outbox = new Outbox(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task An_outbox_entry_is_persisted()
    {
        await _outbox.EnqueueAsync(new OutboxItem
        {
            Entity = OutboxEntities.Event,
            EntityId = "evt-1",
            Op = OutboxOp.Upsert,
            Payload = "{\"title\":\"Standup\"}",
            Attempts = 0,
            NextTryUtc = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
        });

        await using var connection = await _database.Database.OpenAsync();

        var rows = (await connection.QueryAsync(
            "SELECT entity, entity_id, op, payload, attempts, next_try_utc FROM outbox;")).ToList();

        rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task The_database_key_is_generated_once_and_stored_in_the_secret_store()
    {
        var key = await _database.SecretStore.GetAsync(Db.DatabaseKeySecretName);

        key.Should().NotBeNullOrWhiteSpace();
        key!.Length.Should().Be(64);
        key.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task The_database_key_is_reused_across_connections()
    {
        await using (await _database.Database.OpenAsync())
        {
        }

        var first = await _database.SecretStore.GetAsync(Db.DatabaseKeySecretName);

        await using (await _database.Database.OpenAsync())
        {
        }

        var second = await _database.SecretStore.GetAsync(Db.DatabaseKeySecretName);

        second.Should().Be(first);
        _database.SecretStore.SetCount.Should().Be(1);
    }
}
