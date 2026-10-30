using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task Defaults_are_returned_when_nothing_is_stored()
    {
        var service = new SettingsService(new FakeSettingsRepository());

        var settings = await service.LoadAsync();

        settings.Theme.Should().Be(AppTheme.System);
        settings.Accent.Should().Be(AccentSource.System);
        settings.HourHeight.Should().Be(AppSettings.DefaultHourHeight);
        settings.ClockFormat.Should().Be(ClockFormat.TwelveHour);
        settings.WeekStart.Should().Be(WeekStartDay.Monday);
        settings.ShowWeekends.Should().BeTrue();
        settings.OnboardingComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Stored_values_are_loaded_back()
    {
        var repository = new FakeSettingsRepository();
        var service = new SettingsService(repository);

        await service.UpdateAsync(new AppSettings
        {
            Theme = AppTheme.Dark,
            Accent = AccentSource.Custom,
            AccentColor = "#E3008C",
            HourHeight = 72,
            ClockFormat = ClockFormat.TwentyFourHour,
            WeekStart = WeekStartDay.Sunday,
            ShowWeekends = false,
            ShowWeekNumbers = true,
            ShowSecondaryTimeZone = true,
            SecondaryTimeZoneId = "UTC",
            OnboardingComplete = true,
        });

        var reloaded = await new SettingsService(repository).LoadAsync();

        reloaded.Theme.Should().Be(AppTheme.Dark);
        reloaded.Accent.Should().Be(AccentSource.Custom);
        reloaded.AccentColor.Should().Be("#E3008C");
        reloaded.HourHeight.Should().Be(72);
        reloaded.ClockFormat.Should().Be(ClockFormat.TwentyFourHour);
        reloaded.WeekStart.Should().Be(WeekStartDay.Sunday);
        reloaded.ShowWeekends.Should().BeFalse();
        reloaded.ShowWeekNumbers.Should().BeTrue();
        reloaded.ShowSecondaryTimeZone.Should().BeTrue();
        reloaded.SecondaryTimeZoneId.Should().Be("UTC");
        reloaded.OnboardingComplete.Should().BeTrue();
    }

    [Fact]
    public async Task An_out_of_range_hour_height_is_clamped()
    {
        var repository = new FakeSettingsRepository();
        repository.Values["appearance.hour_height"] = "9999";

        var settings = await new SettingsService(repository).LoadAsync();

        settings.HourHeight.Should().Be(AppSettings.MaxHourHeight);
    }

    [Fact]
    public async Task An_unparsable_value_falls_back_to_the_default()
    {
        var repository = new FakeSettingsRepository();
        repository.Values["appearance.theme"] = "NotATheme";

        var settings = await new SettingsService(repository).LoadAsync();

        settings.Theme.Should().Be(AppTheme.System);
    }

    [Fact]
    public async Task Updating_raises_the_changed_event()
    {
        var service = new SettingsService(new FakeSettingsRepository());
        AppSettings? observed = null;

        service.Changed += (_, settings) => observed = settings;

        await service.UpdateAsync(new AppSettings { Theme = AppTheme.Light });

        observed.Should().NotBeNull();
        observed!.Theme.Should().Be(AppTheme.Light);
    }

    [Fact]
    public async Task Updating_exposes_typed_observable_properties()
    {
        var service = new SettingsService(new FakeSettingsRepository());
        var changed = new List<string?>();
        service.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        await service.UpdateAsync(new AppSettings
        {
            Theme = AppTheme.Dark,
            WeekStart = WeekStartDay.Sunday,
            ClockFormat = ClockFormat.TwentyFourHour,
            ShowWeekends = false,
        });

        service.Theme.Should().Be(AppTheme.Dark);
        service.WeekStart.Should().Be(WeekStartDay.Sunday);
        service.Clock.Should().Be(ClockFormat.TwentyFourHour);
        service.ShowWeekends.Should().BeFalse();
        changed.Should().Contain(nameof(ISettingsService.Theme));
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            Values[key] = value;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string?>>(Values);
    }
}
