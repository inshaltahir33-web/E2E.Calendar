using Microsoft.Data.Sqlite;
using CalendarDb = E2E.Calendar.Data.Db;

namespace E2E.Calendar.Data.Tests;

public sealed class TempDatabase : IAsyncDisposable
{
    public string DatabasePath { get; } =
        Path.Combine(Path.GetTempPath(), $"e2e-calendar-{Guid.NewGuid():N}.db");

    public FakeSecretStore SecretStore { get; } = new();

    public CalendarDb Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Database = new CalendarDb(DatabasePath, SecretStore);
        await Database.MigrateAsync();
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = DatabasePath + suffix;

            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
            }
        }

        return ValueTask.CompletedTask;
    }
}
