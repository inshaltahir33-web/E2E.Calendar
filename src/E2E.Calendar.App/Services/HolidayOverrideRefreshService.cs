using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Refreshes cached holiday overrides roughly once per day and re-applies them to
/// any enabled holiday calendars.
/// </summary>
public sealed class HolidayOverrideRefreshService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly HolidayCalendarService _holidays;
    private readonly ILogger<HolidayOverrideRefreshService> _logger;

    public HolidayOverrideRefreshService(
        HolidayCalendarService holidays,
        ILogger<HolidayOverrideRefreshService> logger)
    {
        _holidays = holidays;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial refresh shortly after startup.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await _holidays.RefreshEnabledAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Holiday override refresh failed");
        }

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _holidays.RefreshEnabledAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Holiday override refresh failed");
            }
        }
    }
}