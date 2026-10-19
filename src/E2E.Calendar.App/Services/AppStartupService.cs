using E2E.Calendar.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App.Services;

public sealed class AppStartupService : IHostedService
{
    private readonly Data.Db _database;
    private readonly CalendarService _calendars;
    private readonly ILogger<AppStartupService> _logger;

    public AppStartupService(Data.Db database, CalendarService calendars, ILogger<AppStartupService> logger)
    {
        _database = database;
        _calendars = calendars;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _database.MigrateAsync(cancellationToken);

        var calendars = await _calendars.EnsureDefaultsAsync(cancellationToken);

        _logger.LogInformation(
            "Database ready at {DatabasePath} with {CalendarCount} calendar(s): {Calendars}",
            _database.DatabasePath,
            calendars.Count,
            string.Join(", ", calendars.Select(calendar => calendar.Name)));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
