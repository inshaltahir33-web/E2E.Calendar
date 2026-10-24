using E2E.Calendar.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace E2E.Calendar.Platform;

public sealed class ReminderNotificationService : BackgroundService
{
    private readonly IReminderRepository _reminders;
    private readonly IEventRepository _events;
    private readonly IClock _clock;
    private readonly ILogger<ReminderNotificationService> _logger;
    private readonly HashSet<string> _notified = new(StringComparer.Ordinal);

    public ReminderNotificationService(
        IReminderRepository reminders,
        IEventRepository events,
        IClock clock,
        ILogger<ReminderNotificationService> logger)
    {
        _reminders = reminders;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            await CheckUpcomingAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckUpcomingAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var until = now.AddMinutes(10);

        foreach (var reminder in await _reminders.GetInRangeAsync(now, until, cancellationToken))
        {
            if (reminder.DueUtc is not { } due || !_notified.Add($"reminder:{reminder.Id}:{due:O}"))
            {
                continue;
            }

            ShowToast("Reminder", reminder.Title, due);
        }

        foreach (var calendarEvent in await _events.GetInRangeAsync(null, now, until, cancellationToken))
        {
            if (calendarEvent.StartUtc < now || !_notified.Add($"event:{calendarEvent.Id}:{calendarEvent.StartUtc:O}"))
            {
                continue;
            }

            ShowToast("Upcoming event", calendarEvent.Title, calendarEvent.StartUtc);
        }
    }

    private void ShowToast(string heading, string body, DateTimeOffset when)
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml($"""
                <toast>
                  <visual>
                    <binding template="ToastGeneric">
                      <text>{System.Security.SecurityElement.Escape(heading)}</text>
                      <text>{System.Security.SecurityElement.Escape($"{body} · {when.ToLocalTime():t}")}</text>
                    </binding>
                  </visual>
                </toast>
                """);
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(xml));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not display reminder notification.");
        }
    }
}
