namespace E2E.Calendar.Data.Repos;

using Dapper;
using E2E.Calendar.Core.Abstractions;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly Db _db;

    public SettingsRepository(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        const string sql = "SELECT value FROM settings WHERE key = @Key;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { Key = key }, cancellationToken: cancellationToken));
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        const string sql = """
            INSERT INTO settings (key, value) VALUES (@Key, @Value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;

        await using var connection = await _db.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Key = key, Value = value }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT key AS Key, value AS Value FROM settings;";
        await using var connection = await _db.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<(string Key, string? Value)>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in rows)
        {
            result[key] = value;
        }

        return result;
    }
}
