using System.Globalization;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace E2E.Calendar.Platform;

/// <summary>
/// Shows the once-a-day morning summary while the app runs (including from the tray).
/// The sent date is persisted so a restart later the same day cannot toast twice.
/// </summary>
public sealed class DailyDigestNotificationService : BackgroundService
{
    private const string LastSentKey = "notifications.daily_digest_last_sent";
    private const string DigestTag = "daily-digest";
    private const string DateFormat = "yyyy-MM-dd";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly DailyDigestService _digest;
    private readonly ISettingsService _settings;
    private readonly ISettingsRepository _settingsStore;
    private readonly IClock _clock;
    private readonly FocusAssistService _focusAssist;
    private readonly ILogger<DailyDigestNotificationService> _logger;

    private DateOnly? _lastSentLocalDate;
    private bool _lastSentLoaded;

    public DailyDigestNotificationService(
        DailyDigestService digest,
        ISettingsService settings,
        ISettingsRepository settingsStore,
        IClock clock,
        FocusAssistService focusAssist,
        ILogger<DailyDigestNotificationService> logger)
    {
        _digest = digest;
        _settings = settings;
        _settingsStore = settingsStore;
        _clock = clock;
        _focusAssist = focusAssist;
        _logger = logger;
    }

    /// <summary>False when the OS cannot display app notifications, so the toggle is hidden.</summary>
    public static bool IsSupported
    {
        get
        {
            try
            {
                return AppNotificationManager.IsSupported();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Daily digest check failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendIfDueAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        if (!settings.NotificationsEnabled || !settings.DailyDigestEnabled)
        {
            return;
        }

        // A digest is not a critical reminder: hold it until Do Not Disturb ends.
        if (_focusAssist.IsActive)
        {
            return;
        }

        if (!_lastSentLoaded)
        {
            var stored = await _settingsStore.GetAsync(LastSentKey, cancellationToken);
            _lastSentLocalDate = DateOnly.TryParseExact(
                stored,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
                ? date
                : null;
            _lastSentLoaded = true;
        }

        var now = _clock.UtcNow;
        if (!_digest.IsDue(now, settings.DailyDigestTime, _lastSentLocalDate))
        {
            return;
        }

        var digest = await _digest.BuildAsync(now, cancellationToken);
        if (!TryShowToast(digest))
        {
            return;
        }

        _lastSentLocalDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, _clock.LocalTimeZone).DateTime);
        await _settingsStore.SetAsync(
            LastSentKey,
            _lastSentLocalDate.Value.ToString(DateFormat, CultureInfo.InvariantCulture),
            cancellationToken);
    }

    private bool TryShowToast(DailyDigest digest)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "open-app")
                .SetTag(DigestTag)
                .SetGroup(ToastCleanup.Group)
                .AddText("Daily digest")
                .AddText(digest.Title);

            AppNotificationManager.Default.Show(builder.BuildNotification());

            _logger.LogInformation("Daily digest shown: {Title}.", digest.Title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The daily digest notification could not be shown.");
            return false;
        }
    }
}
