using System.Security.Cryptography;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace E2E.Calendar.Data;

public sealed class Db
{
    public const string DatabaseKeySecretName = "e2e.calendar.db.key";
    private const int KeySizeInBytes = 32;

    private readonly string _databasePath;
    private readonly ISecretStore _secretStore;
    private readonly SemaphoreSlim _keyLock = new(1, 1);

    public Db(string databasePath, ISecretStore secretStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(secretStore);

        DapperSetup.Configure();

        _databasePath = databasePath;
        _secretStore = secretStore;
    }

    public string DatabasePath => _databasePath;

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetOrCreateKeyAsync(cancellationToken);
        var isNewDatabase = !File.Exists(_databasePath);

        // Check for corrupt/zero-length database file before attempting open
        if (!isNewDatabase && File.Exists(_databasePath) && new FileInfo(_databasePath).Length == 0)
        {
            // Delete empty database file and recreate fresh
            try { File.Delete(_databasePath); } catch { }
            isNewDatabase = true;
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = $"x'{key}'",
            Pooling = true,
        };

        var connection = new SqliteConnection(builder.ToString());

        try
        {
            await connection.OpenAsync(cancellationToken);
            await ApplyPragmasAsync(connection, isNewDatabase, cancellationToken);
            return connection;
        }
        catch when (!isNewDatabase && File.Exists(_databasePath))
        {
            // Database file exists but could not be opened - likely corrupted or incompatible
            // Delete and retry as a fresh database
            try { File.Delete(_databasePath); } catch { }
            isNewDatabase = true;

            // Retry opening as a fresh database
            try { await connection.CloseAsync(); } catch { }
            await connection.DisposeAsync();

            builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Password = $"x'{key}'",
                Pooling = true,
            };

            connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(cancellationToken);
            await ApplyPragmasAsync(connection, isNewDatabase, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA journal_mode = WAL;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await new MigrationRunner().MigrateAsync(connection, cancellationToken);
    }

    private static async Task ApplyPragmasAsync(
        SqliteConnection connection,
        bool setJournalMode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = setJournalMode
            ? "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;"
            : "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string> GetOrCreateKeyAsync(CancellationToken cancellationToken)
    {
        var existing = await _secretStore.GetAsync(DatabaseKeySecretName, cancellationToken);

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return ValidateKey(existing);
        }

        await _keyLock.WaitAsync(cancellationToken);

        try
        {
            existing = await _secretStore.GetAsync(DatabaseKeySecretName, cancellationToken);

            if (!string.IsNullOrWhiteSpace(existing))
            {
                return ValidateKey(existing);
            }

            var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(KeySizeInBytes)).ToLowerInvariant();
            await _secretStore.SetAsync(DatabaseKeySecretName, key, cancellationToken);

            return key;
        }
        finally
        {
            _keyLock.Release();
        }
    }

    private static string ValidateKey(string key)
    {
        if (key.Length != KeySizeInBytes * 2 || !key.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException(
                $"The stored database key 'e2e.calendar.db.key' is not a {KeySizeInBytes * 8}-bit hex value.");
        }

        return key;
    }
}