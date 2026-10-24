using Dapper;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Data.Repos;

public sealed class AccountRepository : IAccountRepository
{
    private readonly Db _db;

    public AccountRepository(Db db) => _db = db;

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AccountRow>(
            new CommandDefinition(
                "SELECT id, provider, display_name, email, sync_token, last_sync_utc FROM account ORDER BY display_name;",
                cancellationToken: cancellationToken));
        return rows.Select(row => row.ToModel()).ToArray();
    }

    public async Task UpsertAsync(Account account, CancellationToken cancellationToken = default)
    {
        await using var connection = await _db.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO account (id, provider, display_name, email, sync_token, last_sync_utc)
            VALUES (@Id, @Provider, @DisplayName, @Email, @SyncToken, @LastSyncUtc)
            ON CONFLICT(id) DO UPDATE SET
                provider = excluded.provider,
                display_name = excluded.display_name,
                email = excluded.email,
                sync_token = excluded.sync_token,
                last_sync_utc = excluded.last_sync_utc;
            """,
            new
            {
                account.Id,
                Provider = account.Provider.ToString(),
                account.DisplayName,
                account.Email,
                account.SyncToken,
                LastSyncUtc = account.LastSyncUtc?.ToUniversalTime().ToString("O"),
            },
            cancellationToken: cancellationToken));
    }

    private sealed class AccountRow
    {
        public required string Id { get; init; }
        public string? Provider { get; init; }
        public required string DisplayName { get; init; }
        public string? Email { get; init; }
        public string? SyncToken { get; init; }
        public string? LastSyncUtc { get; init; }

        public Account ToModel() => new()
        {
            Id = Id,
            Provider = Enum.TryParse<Provider>(Provider, true, out var provider) ? provider : E2E.Calendar.Core.Models.Provider.Local,
            DisplayName = DisplayName,
            Email = Email,
            SyncToken = SyncToken,
            LastSyncUtc = DateTimeOffset.TryParse(LastSyncUtc, out var timestamp) ? timestamp : null,
        };
    }
}
