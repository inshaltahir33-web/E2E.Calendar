using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.App.Services;

namespace E2E.Calendar.App.ViewModels;

/// <summary>
/// Backs the Settings page. Each editable property writes through to
/// <see cref="SettingsService"/> so changes persist immediately.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly CalendarService _calendars;
    private readonly IEntitlementService _entitlements;
    private readonly SyncCoordinator _sync;

    private bool _loading;

    public SettingsViewModel(
        SettingsService settings,
        CalendarService calendars,
        IEntitlementService entitlements,
        SyncCoordinator sync)
    {
        _settings = settings;
        _calendars = calendars;
        _entitlements = entitlements;
        _sync = sync;

        Sections =
        [
            new SettingsSection("General", "\uE713"),
            new SettingsSection("Accounts", "\uE77B"),
            new SettingsSection("Calendars", "\uE787"),
            new SettingsSection("Notifications & sounds", "\uEA8F"),
            new SettingsSection("Appearance", "\uE790"),
            new SettingsSection("Shortcuts", "\uE765"),
            new SettingsSection("Premium", "\uE735"),
            new SettingsSection("Privacy & data", "\uE72E"),
            new SettingsSection("Developer", "\uEC7A"),
            new SettingsSection("About", "\uE946"),
        ];

        ThemeLabels = ["Use system setting", "Light", "Dark"];
        AccentLabels = ["Windows accent", "Match calendar", "Custom"];
        DensityLabels = ["Comfortable", "Compact"];
        ClockFormatLabels = ["12-hour", "24-hour"];
        WeekStartLabels = ["Sunday", "Monday", "Saturday"];
        AccentColors = new ObservableCollection<string>(CalendarPalette.All);
    }

    public IReadOnlyList<SettingsSection> Sections { get; }

    public ObservableCollection<string> ThemeLabels { get; }

    public ObservableCollection<string> AccentLabels { get; }

    public ObservableCollection<string> DensityLabels { get; }

    public ObservableCollection<string> ClockFormatLabels { get; }

    public ObservableCollection<string> WeekStartLabels { get; }

    public ObservableCollection<string> AccentColors { get; }

    public ObservableCollection<SettingsCalendarItem> Calendars { get; } = new();

    public ObservableCollection<SettingsAccountItem> Accounts { get; } = new();

    public string AppVersion => "1.0.0 (local)";

    public string DatabaseSummary => "Stored on this device, encrypted with SQLCipher";

    public bool IsPremium => _entitlements.Can(Feature.CalendarSets);

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
    private bool _showBirthdays;

    [ObservableProperty]
    private int _densityIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGoogleAccountCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddMicrosoftAccountCommand))]
    private bool _isAccountOperationInProgress;

    [ObservableProperty]
    private string? _accountStatus;

    public double MinHourHeight => AppSettings.MinHourHeight;

    public double MaxHourHeight => AppSettings.MaxHourHeight;

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
        ShowBirthdays = settings.ShowBirthdays;
        DensityIndex = settings.Density == AppDensity.Compact ? 1 : 0;

        var calendars = await _calendars.GetAllAsync();

        Calendars.Clear();

        foreach (var calendar in calendars)
        {
            Calendars.Add(new SettingsCalendarItem
            {
                Id = calendar.Id,
                Name = calendar.Name,
                Color = calendar.Color,
                IsVisible = calendar.Visible,
                IsReadOnly = calendar.ReadOnly,
            });
        }

        Accounts.Clear();
        Accounts.Add(new SettingsAccountItem
        {
            Provider = Provider.Local,
            DisplayName = "This device",
            Detail = $"{calendars.Count} calendar(s) · local only",
        });
        foreach (var account in _sync.Accounts)
        {
            Accounts.Add(new SettingsAccountItem
            {
                Provider = account.Provider,
                DisplayName = account.DisplayName,
                Detail = account.Email ?? "Connected account",
            });
        }

        _loading = false;
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

    public bool CanAddAccount => !IsAccountOperationInProgress;

    private async Task AddAccountAsync(Provider provider)
    {
        if (!_entitlements.Can(Feature.CalendarSets) && _sync.Accounts.Count > 0)
        {
            AccountStatus = "The free plan supports one connected account.";
            return;
        }

        IsAccountOperationInProgress = true;
        AccountStatus = $"Opening {provider} sign-in...";

        try
        {
            await _sync.AddAccountAsync(provider);
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
        ShowBirthdays = false;
        DensityIndex = 0;

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
        ShowSecondaryTimeZone = ShowSecondaryTimeZone,
        ShowBirthdays = ShowBirthdays,
        Density = DensityIndex == 1 ? AppDensity.Compact : AppDensity.Comfortable,
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

    public async Task ImportBirthdaysAsync(CancellationToken cancellationToken = default)
    {
        await _sync.ImportBirthdaysAsync(cancellationToken);
        await LoadAsync();
    }

    partial void OnDensityIndexChanged(int value) => Persist();
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
}

public sealed record SettingsAccountItem
{
    public required Provider Provider { get; init; }

    public required string DisplayName { get; init; }

    public required string Detail { get; init; }
}
