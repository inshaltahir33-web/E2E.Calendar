using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.App;
using E2E.Calendar.App.Services;
using E2E.Calendar.Data;
using E2E.Calendar.Platform;
using E2E.Calendar.Sync.Ics;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using CalendarModel = E2E.Calendar.Core.Models.Calendar;

namespace E2E.Calendar.App.ViewModels;

/// <summary>
/// Backs the Settings page. Each editable property writes through to
/// <see cref="SettingsService"/> so changes persist immediately.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly CalendarService _calendars;
    private readonly IEventRepository _events;
    private readonly IcsService _ics;
    private readonly IClock _clock;
    private readonly IcsSubscriptionService _subscriptions;
    private readonly IEntitlementService _entitlements;
    private readonly IPaywallService _paywall;
    private readonly SyncCoordinator _sync;
    private readonly Db _db;
    private readonly ISecretStore _secrets;
    private readonly StartupTaskService _startupTasks;
    private readonly IEventTemplateStore _templateStore;

    private static readonly int[] SnoozeMinuteOptions = [5, 10, 15, 30, 60];
    private static readonly int[] RepeatMinuteOptions = [5, 10, 15, 30, 60];

    private bool _loading;

    public SettingsViewModel(
        SettingsService settings,
        CalendarService calendars,
        IEventRepository events,
        IcsService ics,
        IClock clock,
        IcsSubscriptionService subscriptions,
        IEntitlementService entitlements,
        IPaywallService paywall,
        SyncCoordinator sync,
        Db db,
        ISecretStore secrets,
        StartupTaskService startupTasks,
        IEventTemplateStore templateStore)
    {
        _settings = settings;
        _calendars = calendars;
        _events = events;
        _ics = ics;
        _clock = clock;
        _subscriptions = subscriptions;
        _entitlements = entitlements;
        _paywall = paywall;
        _sync = sync;
        _db = db;
        _secrets = secrets;
        _startupTasks = startupTasks;
        _templateStore = templateStore;

        _calendars.Changed += OnCalendarsChanged;

        var sections = new List<SettingsSection>
        {
            new SettingsSection("General", "\uE713"),
            new SettingsSection("Accounts", "\uE77B"),
        };

        sections.Add(new SettingsSection("Calendars", "\uE787"));
        sections.Add(new SettingsSection("Templates", "\uE8EF"));
        sections.Add(new SettingsSection("Notifications", "\uEA8F"));
        sections.Add(new SettingsSection("Appearance", "\uE790"));
        sections.Add(new SettingsSection("Shortcuts", "\uE765"));
        sections.Add(new SettingsSection("Premium", "\uE735"));
        sections.Add(new SettingsSection("Privacy & data", "\uE72E"));

        if (AppFeatures.DemoEnabled)
        {
            sections.Add(new SettingsSection("Developer", "\uEC7A"));
        }

        sections.Add(new SettingsSection("About", "\uE946"));

        Sections = sections;

        ThemeLabels = ["Use system setting", "Light", "Dark"];
        AccentLabels = ["Windows accent", "Match calendar", "Custom"];
        ClockFormatLabels = ["12-hour", "24-hour"];
        WeekStartLabels = ["Sunday", "Monday", "Saturday"];
        TimeZoneLabels = new ObservableCollection<string>(TimeZoneCatalog.Common.Select(item => item.Label));
        AccentColors = new ObservableCollection<string>(CalendarPalette.All);
        SnoozeLabels = ["5 minutes", "10 minutes", "15 minutes", "30 minutes", "1 hour"];
        RepeatIntervalLabels = ["Every 5 minutes", "Every 10 minutes", "Every 15 minutes", "Every 30 minutes", "Every hour"];
    }

    public IReadOnlyList<SettingsSection> Sections { get; }

    public ObservableCollection<string> ThemeLabels { get; }

    public ObservableCollection<string> AccentLabels { get; }

    public ObservableCollection<string> ClockFormatLabels { get; }

    public ObservableCollection<string> WeekStartLabels { get; }

    public ObservableCollection<string> TimeZoneLabels { get; }

    public ObservableCollection<string> AccentColors { get; }

    public IReadOnlyList<string> SnoozeLabels { get; }

    public IReadOnlyList<string> RepeatIntervalLabels { get; }

    public ObservableCollection<SettingsCalendarItem> Calendars { get; } = new();

    public ObservableCollection<SettingsAccountItem> Accounts { get; } = new();

    public ObservableCollection<SettingsTemplateItem> Templates { get; } = new();

    public bool HasTemplates => Templates.Count > 0;

    public bool HasNoTemplates => !HasTemplates;

    public ObservableCollection<SettingsPremiumPlan> PremiumPlans { get; } = new()
    {
        new SettingsPremiumPlan
        {
            Title = "Monthly",
            Price = "$2.99 / month",
            Detail = "7-day free trial",
        },
        new SettingsPremiumPlan
        {
            Title = "Yearly",
            Price = "$19.99 / year",
            Detail = "Save 44% compared with monthly",
            Badge = "Save 44%",
            IsHighlighted = true,
        },
        new SettingsPremiumPlan
        {
            Title = "Lifetime",
            Price = "$39.99 one-time",
            Detail = "Pay once, keep premium forever",
        },
    };

    public IReadOnlyList<SettingsPremiumFeature> PremiumFeatures { get; } =
    [
        new("Day / Week / Month / Agenda views", "Included", "Included", false),
        new("Local calendars and ICS subscriptions", "Included", "Included", false),
        new("Event alerts and standalone reminders", "Included", "Included", false),
        new("Persistent reminders", "3 active", "Unlimited", false),
        new("Tasks and lists", "1 list", "Unlimited lists", false),
        new("Natural-language quick add", "Included", "Included", false),
        new("Global hotkey and tray quick capture", "Included", "Included", false),
        new("Themes and accent colours", "Included", "Included", false),
        new("Connected accounts (Google / Microsoft / iCloud)", "Coming soon", "Coming soon", true),
        new("Year view and custom multi-day view", "Coming soon", "Coming soon", true),
        new("Calendar sets and profiles", "Coming soon", "Coming soon", true),
        new("Event templates", "Included", "Included", false),
        new("Compact always-on-top agenda strip", "Coming soon", "Coming soon", true),
        new("Habit tracker, weather and daily digest", "Coming soon", "Coming soon", true),
        new("Find a free slot and share availability", "Coming soon", "Coming soon", true),
        new("Windows Hello app lock", "Coming soon", "Coming soon", true),
    ];

    public string AppVersion => "1.0.0 (local)";

    public string DatabaseSummary => "Stored on this device, encrypted with SQLCipher";

    [ObservableProperty]
    private int _themeIndex;

    [ObservableProperty]
    private int _accentIndex;

    [ObservableProperty]
    private string? _accentColor;

    [ObservableProperty]
    private double _hourHeight = AppSettings.DefaultHourHeight;

    [ObservableProperty]
    private int _clockFormatIndex;

    [ObservableProperty]
    private int _weekStartIndex = 1;

    [ObservableProperty]
    private bool _showWeekends = true;

    [ObservableProperty]
    private bool _showWeekNumbers;

    [ObservableProperty]
    private bool _showSecondaryTimeZone;

    [ObservableProperty]
    private int _secondaryTimeZoneIndex;

    [ObservableProperty]
    private bool _showBirthdays;

    [ObservableProperty]
    private bool _startAtLogin;

    [ObservableProperty]
    private bool _isStartAtLoginSupported;

    [ObservableProperty]
    private bool _isStartAtLoginEditable;

    [ObservableProperty]
    private bool _canOpenStartupSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartAtLoginNote))]
    private string? _startAtLoginNote;

    [ObservableProperty]
    private int _defaultSnoozeIndex = 1;

    [ObservableProperty]
    private int _persistentRepeatIndex = 1;

    [ObservableProperty]
    private bool _dailyDigestEnabled;

    [ObservableProperty]
    private TimeSpan _dailyDigestTime = new(8, 0, 0);

    [ObservableProperty]
    private bool _criticalBreaksThroughDoNotDisturb = true;

    public bool HasStartAtLoginNote => !string.IsNullOrWhiteSpace(StartAtLoginNote);

    public double MinHourHeight => AppSettings.MinHourHeight;

    public double MaxHourHeight => AppSettings.MaxHourHeight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGoogleAccountCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddMicrosoftAccountCommand))]
    private bool _isAccountOperationInProgress;

    [ObservableProperty]
    private string? _accountStatus;

    [ObservableProperty]
    private string _premiumStatus = "Checking your plan…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPremiumNote))]
    private string? _premiumNote;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PremiumUpgradeLabel))]
    [NotifyPropertyChangedFor(nameof(PremiumRestoreLabel))]
    [NotifyPropertyChangedFor(nameof(IsPurchaseUnavailable))]
    [NotifyCanExecuteChangedFor(nameof(ShowPaywallCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestorePurchasesCommand))]
    private bool _isStoreAvailable;

    private const string StoreOnlyMessage = "Available in the Microsoft Store version";

    public bool HasPremiumNote => !string.IsNullOrWhiteSpace(PremiumNote);

    public bool IsPurchaseUnavailable => !IsStoreAvailable;

    public bool CanPurchase => IsStoreAvailable;

    public string PremiumUpgradeLabel => IsStoreAvailable ? "Upgrade" : StoreOnlyMessage;

    public string PremiumRestoreLabel => IsStoreAvailable ? "Restore purchases" : StoreOnlyMessage;

    public async Task LoadAsync()
    {
        _loading = true;

        var settings = await _settings.LoadAsync();
        await _sync.LoadAccountsAsync();

        ThemeIndex = settings.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0,
        };

        AccentIndex = settings.Accent switch
        {
            AccentSource.Calendar => 1,
            AccentSource.Custom => 2,
            _ => 0,
        };

        AccentColor = settings.AccentColor;
        HourHeight = settings.HourHeight;
        ClockFormatIndex = settings.ClockFormat == ClockFormat.TwentyFourHour ? 1 : 0;
        WeekStartIndex = settings.WeekStart switch
        {
            WeekStartDay.Sunday => 0,
            WeekStartDay.Saturday => 2,
            _ => 1,
        };
        ShowWeekends = settings.ShowWeekends;
        ShowWeekNumbers = settings.ShowWeekNumbers;
        ShowSecondaryTimeZone = settings.ShowSecondaryTimeZone;
        SecondaryTimeZoneIndex = TimeZoneCatalog.IndexOf(settings.SecondaryTimeZoneId);
        ShowBirthdays = settings.ShowBirthdays;

        DefaultSnoozeIndex = IndexOf(SnoozeMinuteOptions, settings.DefaultSnoozeMinutes, 1);
        PersistentRepeatIndex = IndexOf(RepeatMinuteOptions, settings.PersistentRepeatMinutes, 1);
        DailyDigestEnabled = settings.DailyDigestEnabled;
        DailyDigestTime = settings.DailyDigestTime;
        CriticalBreaksThroughDoNotDisturb = settings.CriticalBreaksThroughDoNotDisturb;

        // The OS owns the startup task state, so read it back instead of persisting a copy.
        ApplyStartupStatus(await _startupTasks.GetStatusAsync());

        IsStoreAvailable = _entitlements.IsStoreAvailable;
        await RefreshPremiumAsync();

        var calendars = await _calendars.GetAllAsync();
        var subscriptions = (await _subscriptions.GetAllAsync())
            .ToDictionary(subscription => subscription.CalendarId);

        ApplyCalendars(calendars, subscriptions);
        await LoadTemplatesAsync();

        Accounts.Clear();
        Accounts.Add(new SettingsAccountItem
        {
            Id = "local",
            Provider = Provider.Local,
            DisplayName = "This device",
            Detail = $"{calendars.Count} calendar(s) · local only",
        });
        foreach (var account in _sync.Accounts)
        {
            Accounts.Add(new SettingsAccountItem
            {
                Id = account.Id,
                Provider = account.Provider,
                DisplayName = account.DisplayName,
                Detail = DescribeAccount(account),
            });
        }

        _loading = false;
    }

    private void OnCalendarsChanged(object? sender, CalendarListChangedEventArgs e) =>
        _ = RefreshCalendarsAsync();

    private async Task RefreshCalendarsAsync()
    {
        var calendars = await _calendars.GetAllAsync();
        var subscriptions = (await _subscriptions.GetAllAsync())
            .ToDictionary(subscription => subscription.CalendarId);

        ApplyCalendars(calendars, subscriptions);
    }

    private async Task LoadTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _templateStore.GetAllAsync(cancellationToken);

        Templates.Clear();

        foreach (var template in templates)
        {
            Templates.Add(new SettingsTemplateItem(
                template.Id,
                template.Name,
                DescribeTemplate(template)));
        }

        OnPropertyChanged(nameof(HasTemplates));
        OnPropertyChanged(nameof(HasNoTemplates));
    }

    public async Task DeleteTemplateAsync(string id, CancellationToken cancellationToken = default)
    {
        await _templateStore.DeleteAsync(id, cancellationToken);
        await LoadTemplatesAsync(cancellationToken);
    }

    private static string DescribeTemplate(EventTemplate template)
    {
        var alerts = template.AlertOffsetsMinutes.Count switch
        {
            0 => "no alerts",
            1 => "1 alert",
            var count => $"{count} alerts",
        };

        return string.IsNullOrWhiteSpace(template.Location)
            ? $"{template.DurationMinutes} min · {alerts}"
            : $"{template.DurationMinutes} min · {template.Location} · {alerts}";
    }

    private void ApplyCalendars(
        IReadOnlyList<CalendarModel> calendars,
        IReadOnlyDictionary<string, CalendarSubscription> subscriptions)
    {
        Calendars.Clear();

        foreach (var calendar in calendars)
        {
            subscriptions.TryGetValue(calendar.Id, out var subscription);

            Calendars.Add(new SettingsCalendarItem
            {
                Id = calendar.Id,
                Name = calendar.Name,
                Color = calendar.Color,
                IsVisible = calendar.Visible,
                IsReadOnly = calendar.ReadOnly,
                IsSubscription = subscription is not null,
                Detail = subscription is not null
                    ? DescribeSubscription(subscription)
                    : calendar.ReadOnly ? "Read-only" : null,
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddAccount), AllowConcurrentExecutions = false)]
    private async Task AddGoogleAccountAsync()
    {
        await AddAccountAsync(Provider.Google);
    }

    [RelayCommand(CanExecute = nameof(CanAddAccount), AllowConcurrentExecutions = false)]
    private async Task AddMicrosoftAccountAsync()
    {
        await AddAccountAsync(Provider.Microsoft);
    }

    [RelayCommand(CanExecute = nameof(CanAddAccount), AllowConcurrentExecutions = false)]
    private async Task AddCalDavAccountAsync()
    {
        await AddAccountAsync(Provider.CalDav);
    }

    public bool CanAddAccount => !IsAccountOperationInProgress;

    private const int FreeAccountLimit = 1;

    private async Task AddAccountAsync(Provider provider)
    {
        if (!_entitlements.Current.IsPremium && _sync.Accounts.Count >= FreeAccountLimit)
        {
            AccountStatus = "The free plan includes one connected account.";

            if (!await _paywall.ShowAsync())
            {
                return;
            }
        }

        IsAccountOperationInProgress = true;
        AccountStatus = $"Opening {provider} sign-in...";

        try
        {
            await _sync.AddAccountAsync(provider);
            await _calendars.ReloadAsync();
            await LoadAsync();
            AccountStatus = $"{provider} account connected.";
        }
        catch (OperationCanceledException)
        {
            AccountStatus = $"{provider} sign-in was cancelled.";
        }
        catch (Exception ex)
        {
            AccountStatus = $"{provider} sign-in failed: {ex.Message}";
        }
        finally
        {
            IsAccountOperationInProgress = false;
        }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        _loading = true;

        ThemeIndex = 0;
        AccentIndex = 0;
        AccentColor = null;
        HourHeight = AppSettings.DefaultHourHeight;
        ClockFormatIndex = 0;
        WeekStartIndex = 1;
        ShowWeekends = true;
        ShowWeekNumbers = false;
        ShowSecondaryTimeZone = false;
        SecondaryTimeZoneIndex = TimeZoneCatalog.IndexOf(null);
        ShowBirthdays = false;
        DefaultSnoozeIndex = 1;
        PersistentRepeatIndex = 1;
        DailyDigestEnabled = false;
        DailyDigestTime = new(8, 0, 0);
        CriticalBreaksThroughDoNotDisturb = true;

        _loading = false;

        await _settings.UpdateAsync(Snapshot());
    }

    private AppSettings Snapshot() => new()
    {
        Theme = ThemeIndex switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.System },
        Accent = AccentIndex switch { 1 => AccentSource.Calendar, 2 => AccentSource.Custom, _ => AccentSource.System },
        AccentColor = AccentColor,
        HourHeight = (int)Math.Round(HourHeight),
        ClockFormat = ClockFormatIndex == 1 ? ClockFormat.TwentyFourHour : ClockFormat.TwelveHour,
        WeekStart = WeekStartIndex switch
        {
            0 => WeekStartDay.Sunday,
            2 => WeekStartDay.Saturday,
            _ => WeekStartDay.Monday,
        },
        ShowWeekends = ShowWeekends,
        ShowWeekNumbers = ShowWeekNumbers,
        ShowRemindersAndTasks = _settings.Current.ShowRemindersAndTasks,
        ShowSecondaryTimeZone = ShowSecondaryTimeZone,
        SecondaryTimeZoneId = TimeZoneCatalog.Common[Math.Clamp(SecondaryTimeZoneIndex, 0, TimeZoneCatalog.Common.Count - 1)].Id,
        ShowBirthdays = ShowBirthdays,
        DefaultSnoozeMinutes = SnoozeMinuteOptions[Math.Clamp(DefaultSnoozeIndex, 0, SnoozeMinuteOptions.Length - 1)],
        NotificationsEnabled = _settings.Current.NotificationsEnabled,
        PersistentRepeatMinutes = RepeatMinuteOptions[Math.Clamp(PersistentRepeatIndex, 0, RepeatMinuteOptions.Length - 1)],
        DailyDigestEnabled = DailyDigestEnabled,
        DailyDigestTime = DailyDigestTime,
        CriticalBreaksThroughDoNotDisturb = CriticalBreaksThroughDoNotDisturb,
        OnboardingComplete = true,
    };

    private void Persist()
    {
        if (_loading)
        {
            return;
        }

        _ = _settings.UpdateAsync(Snapshot());
    }

    partial void OnThemeIndexChanged(int value) => Persist();

    partial void OnAccentIndexChanged(int value) => Persist();

    partial void OnAccentColorChanged(string? value) => Persist();

    partial void OnHourHeightChanged(double value) => Persist();

    partial void OnClockFormatIndexChanged(int value) => Persist();

    partial void OnWeekStartIndexChanged(int value) => Persist();

    partial void OnShowWeekendsChanged(bool value) => Persist();

    partial void OnShowWeekNumbersChanged(bool value) => Persist();

    partial void OnShowSecondaryTimeZoneChanged(bool value) => Persist();

    partial void OnSecondaryTimeZoneIndexChanged(int value) => Persist();

    partial void OnDefaultSnoozeIndexChanged(int value) => Persist();

    partial void OnPersistentRepeatIndexChanged(int value) => Persist();

    partial void OnDailyDigestEnabledChanged(bool value) => Persist();

    partial void OnDailyDigestTimeChanged(TimeSpan value) => Persist();

    partial void OnCriticalBreaksThroughDoNotDisturbChanged(bool value) => Persist();

    partial void OnStartAtLoginChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        _ = ApplyStartAtLoginAsync(value);
    }

    private async Task ApplyStartAtLoginAsync(bool enabled)
    {
        var status = await _startupTasks.SetEnabledAsync(enabled);

        // Windows may refuse the change (for example the task was disabled in Task Manager),
        // so mirror whatever state it reports instead of the requested one.
        _loading = true;
        ApplyStartupStatus(status);
        _loading = false;
    }

    private void ApplyStartupStatus(StartupStatus status)
    {
        IsStartAtLoginSupported = status is not StartupStatus.Unsupported;
        IsStartAtLoginEditable = StartupTaskService.IsEditable(status);
        CanOpenStartupSettings = status is StartupStatus.DisabledByUser;
        StartAtLogin = StartupTaskService.IsEnabled(status);
        StartAtLoginNote = status switch
        {
            StartupStatus.Unsupported => "Start at login is only available in the packaged (MSIX) build of E2E Calendar.",
            StartupStatus.DisabledByUser => "Windows turned this off. Enable E2E Calendar in Windows Settings > Apps > Startup.",
            StartupStatus.DisabledByPolicy => "Start at login is turned off by your organization's policy.",
            StartupStatus.EnabledByPolicy => "Start at login is turned on by your organization's policy.",
            _ => null,
        };
    }

    private static int IndexOf(int[] options, int value, int fallback)
    {
        var index = Array.IndexOf(options, value);

        return index >= 0 ? index : fallback;
    }

    public async Task ImportBirthdaysAsync(CancellationToken cancellationToken = default)
    {
        await _sync.ImportBirthdaysAsync(cancellationToken);
        await _calendars.ReloadAsync(cancellationToken);
        await LoadAsync();
    }

    public async Task CreateCalendarAsync(string name, string color, CancellationToken cancellationToken = default)
    {
        await _calendars.CreateLocalAsync(name, color, cancellationToken);
        await LoadAsync();
    }

    public async Task AddSubscriptionAsync(string url, string? name, int refreshHours, CancellationToken cancellationToken = default)
    {
        await _subscriptions.AddAsync(url, refreshHours, name, cancellationToken);
        await LoadAsync();
    }

    public async Task<string> ExportEventsAsync(CancellationToken cancellationToken = default)
    {
        var events = await _events.GetInRangeAsync(
            null,
            _clock.UtcNow.AddYears(-10),
            _clock.UtcNow.AddYears(10),
            cancellationToken);

        return _ics.Export(events);
    }

    public async Task DeleteCalendarAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id is CalendarService.PersonalCalendarId or CalendarService.WorkCalendarId)
        {
            throw new InvalidOperationException("The default calendars cannot be deleted.");
        }

        // Subscription calendars also own a feed row and their imported events.
        if (await _subscriptions.GetAsync(id, cancellationToken) is not null)
        {
            await _subscriptions.RemoveAsync(id, cancellationToken);
            await LoadAsync();
            return;
        }

        await _calendars.DeleteAsync(id, cancellationToken);
        await LoadAsync();
    }

    private static string DescribeSubscription(CalendarSubscription subscription)
    {
        var lastRefresh = subscription.LastRefreshUtc is { } refreshed
            ? $"last refreshed {refreshed.ToLocalTime():g}"
            : "waiting for the first refresh";

        return $"Read-only · {lastRefresh} · refreshes every {subscription.RefreshHours} h";
    }

    private static string DescribeAccount(Account account)
    {
        var identity = string.IsNullOrWhiteSpace(account.Email) ? account.Provider.ToString() : account.Email;
        var sync = account.LastSyncUtc is { } last
            ? $"synced {last.ToLocalTime():g}"
            : "not synced yet";

        return $"{identity} · {sync}";
    }

    public async Task SetCalendarColorAsync(string id, string color, CancellationToken cancellationToken = default)
    {
        await _calendars.SetColorAsync(id, color, cancellationToken);
    }

    public async Task RemoveAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await _sync.RemoveAccountAsync(accountId, cancellationToken);
        await _calendars.ReloadAsync(cancellationToken);
        await LoadAsync();
    }

    public async Task DeleteAllDataAsync(CancellationToken cancellationToken = default)
    {
        await _sync.LoadAccountsAsync(cancellationToken);

        foreach (var account in _sync.Accounts.ToList())
        {
            var prefix = account.Provider switch
            {
                Provider.Microsoft => "ms",
                Provider.Google => "google",
                Provider.CalDav => "caldav",
                _ => null,
            };

            if (prefix is null)
            {
                continue;
            }

            await _secrets.RemoveAsync($"{prefix}:access:{account.Id}", cancellationToken);
            await _secrets.RemoveAsync($"{prefix}:refresh:{account.Id}", cancellationToken);

            if (prefix == "caldav")
            {
                await _secrets.RemoveAsync($"caldav:password:{account.Id}", cancellationToken);
                await _secrets.RemoveAsync($"caldav:url:{account.Id}", cancellationToken);
            }
        }

        _sync.Accounts.Clear();

        await _secrets.RemoveAsync(Db.DatabaseKeySecretName, cancellationToken);

        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var path = suffix.Length == 0 ? _db.DatabasePath : _db.DatabasePath + suffix;

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        await _db.MigrateAsync(cancellationToken);
        await _settings.LoadAsync(cancellationToken);
        await _sync.LoadAccountsAsync(cancellationToken);
        await _calendars.ReloadAsync(cancellationToken);
        await LoadAsync();
    }

    public async Task RefreshPremiumAsync()
    {
        var snapshot = await _entitlements.RefreshAsync();
        PremiumStatus = EntitlementText.Describe(snapshot, _entitlements.IsStoreAvailable);
    }

    [RelayCommand(CanExecute = nameof(CanPurchase))]
    private async Task ShowPaywallAsync()
    {
        await _paywall.ShowAsync();
        await RefreshPremiumAsync();
    }

    [RelayCommand(CanExecute = nameof(CanPurchase), AllowConcurrentExecutions = false)]
    private async Task RestorePurchasesAsync()
    {
        var result = await _entitlements.RestoreAsync();
        PremiumNote = result.Message ?? "Purchases restored.";
        await RefreshPremiumAsync();
    }

}

public sealed record SettingsSection(string Title, string Glyph);

public sealed partial class SettingsCalendarItem : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible = true;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Color { get; init; }

    public bool IsReadOnly { get; init; }

    public bool IsSubscription { get; init; }

    public bool IsNotSubscription => !IsSubscription;

    public string? Detail { get; init; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
}

public sealed record SettingsTemplateItem(string Id, string Name, string Detail);

public sealed record SettingsAccountItem
{
    public required string Id { get; init; }

    public required Provider Provider { get; init; }

    public required string DisplayName { get; init; }

    public required string Detail { get; init; }
}

public sealed class SettingsPremiumPlan
{
    public required string Title { get; init; }

    public required string Price { get; init; }

    public required string Detail { get; init; }

    public string? Badge { get; init; }

    public bool IsHighlighted { get; init; }

    public Visibility BadgeVisibility => string.IsNullOrEmpty(Badge) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility HighlightVisibility => IsHighlighted ? Visibility.Visible : Visibility.Collapsed;
}

public sealed record SettingsPremiumFeature(string Name, string Free, string Premium, bool IsComingSoon)
{
    public double RowOpacity => IsComingSoon ? 0.6 : 1.0;
}
