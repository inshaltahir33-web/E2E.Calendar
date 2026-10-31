using E2E.Calendar.Sync.Ics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Polls subscribed ICS calendars so each one is fetched once its own
/// 1-24 hour refresh interval has elapsed.
/// </summary>
public sealed class IcsSubscriptionRefreshService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IcsSubscriptionService _subscriptions;
    private readonly ILogger<IcsSubscriptionRefreshService> _logger;

    public IcsSubscriptionRefreshService(
        IcsSubscriptionService subscriptions,
        ILogger<IcsSubscriptionRefreshService> logger)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(logger);

        _subscriptions = subscriptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            do
            {
                try
                {
                    await _subscriptions.RefreshDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "The ICS subscription refresh pass failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
