using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace E2E.Calendar.Data.Migrations;

public sealed record Migration(int Version, string Name, string Sql);

public sealed class MigrationRunner
{
    private static readonly IReadOnlyList<Migration> All = Load();

    public static IReadOnlyList<Migration> Migrations => All;

    public async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var currentVersion = await GetUserVersionAsync(connection, cancellationToken);

        foreach (var migration in All.Where(m => m.Version > currentVersion).OrderBy(m => m.Version))
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                if (migration.Version == 3 &&
                    await ColumnExistsAsync(connection, transaction, "event", "attendees", cancellationToken))
                {
                    command.CommandText = "SELECT 1;";
                }
                else
                {
                    command.CommandText = migration.Sql;
                }

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.Transaction = transaction;
                versionCommand.CommandText = string.Create(
                    CultureInfo.InvariantCulture,
                    $"PRAGMA user_version = {migration.Version};");
                await versionCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public static async Task<int> GetUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<Migration> Load()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var migrations = new List<Migration>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = resourceName[(resourceName.LastIndexOf('.', resourceName.Length - 5) + 1)..];
            var withoutExtension = fileName[..^4];
            var separator = withoutExtension.IndexOf('_', StringComparison.Ordinal);

            if (separator <= 0)
            {
                throw new InvalidOperationException($"Migration '{resourceName}' is not named '<version>_<name>.sql'.");
            }

            var version = int.Parse(withoutExtension[..separator], CultureInfo.InvariantCulture);
            var name = withoutExtension[(separator + 1)..];

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Migration resource '{resourceName}' could not be opened.");
            using var reader = new StreamReader(stream);

            migrations.Add(new Migration(version, name, reader.ReadToEnd()));
        }

        return migrations.OrderBy(m => m.Version).ToList();
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({table});";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
