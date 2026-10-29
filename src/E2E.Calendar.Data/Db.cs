using System.Security.Cryptography;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Data.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.Data;

public sealed class Db
{
    public const string DatabaseKeySecretName = "e2e.calendar.db.key";
    private const int KeySizeInBytes = 32;

    private readonly string _databasePath;
    private readonly ISecretStore _secretStore;
    private readonly ILogger<Db>? _logger;
    private readonly SemaphoreSlim _keyLock = new(1, 1);

    public Db(string databasePath, ISecretStore secretStore, ILogger<Db>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(secretStore);

        DapperSetup.Configure();

        _databasePath = databasePath;
        _secretStore = secretStore;
        _logger = logger;
    }

    public string DatabasePath => _databasePath;

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetOrCreateKeyAsync(cancellationToken);
        var isNewDatabase = !File.Exists(_databasePath);

        // A zero-length file carries no data, so it is safe to treat it as a new
        // empty database. The file itself is never deleted; SQLite initialises it
        // on open.
        if (!isNewDatabase && File.Exists(_databasePath) && new FileInfo(_databasePath).Length == 0)
        {
            _logger?.LogWarning(
                "The database file '{Path}' is zero bytes; treating it as a new empty database.",
                _databasePath);
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
        catch (Exception exception) when (!isNewDatabase && File.Exists(_databasePath))
        {
            // The file exists but could not be opened (corrupt, wrong key, incompatible
            // format, ...). Never delete user data: preserve the file under a .bak name
            // and start a fresh database so the app can still run.
            try { await connection.CloseAsync(); } catch { }
            await connection.DisposeAsync();

            var backupPath = BackupCorruptDatabase(exception);
            if (backupPath is null)
            {
                // The file could not be preserved, so a fresh database must not be
                // created in its place. Surface the original failure.
                throw;
            }

            _logger?.LogWarning(
                exception,
                "The database file '{Path}' could not be opened; it was preserved as '{Backup}' and a fresh database was created.",
                _databasePath,
                backupPath);

            connection = new SqliteConnection(builder.ToString());
            try
            {
                await connection.OpenAsync(cancellationToken);
                await ApplyPragmasAsync(connection, setJournalMode: true, cancellationToken);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
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

    /// <summary>
    /// Renames the unreadable database (and its WAL/SHM sidecars) to
    /// <c><name>.corrupt-<timestamp>.bak</c> so a fresh database can be created.
    /// Returns the backup path, or <c>null</c> when the file could not be preserved.
    /// </summary>
    private string? BackupCorruptDatabase(Exception exception)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupPath = $"{_databasePath}.corrupt-{timestamp}.bak";

        // Guarantee a unique name without ever overwriting an existing backup.
        for (var attempt = 1; File.Exists(backupPath) && attempt < 100; attempt++)
        {
            backupPath = $"{_databasePath}.corrupt-{timestamp}-{attempt}.bak";
        }

        try
        {
            File.Move(_databasePath, backupPath);
        }
        catch (Exception moveException)
        {
            _logger?.LogError(
                moveException,
                "Could not preserve the unreadable database file '{Path}'.",
                _databasePath);
            return null;
        }

        // Move the sidecar files of the old database next to the backup so they
        // cannot be mistaken for parts of the fresh database.
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = _databasePath + suffix;
            if (!File.Exists(sidecar))
            {
                continue;
            }

            try
            {
                File.Move(sidecar, backupPath + suffix);
            }
            catch (Exception sidecarException)
            {
                _logger?.LogWarning(
                    sidecarException,
                    "Could not move the database sidecar file '{Sidecar}'.",
                    sidecar);
            }
        }

        return backupPath;
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