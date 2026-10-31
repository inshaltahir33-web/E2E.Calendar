using System.ComponentModel;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface ISettingsService : INotifyPropertyChanged
{
    AppSettings Current { get; }
    AppTheme Theme { get; }
    WeekStartDay WeekStart { get; }
    ClockFormat Clock { get; }
    bool ShowWeekends { get; }
    bool ShowWeekNumbersWeek { get; }
    bool ShowWeekNumbersMonth { get; }
    bool ShowRemindersAndTasks { get; }
    bool ShowSecondaryTimeZone { get; }
    string? SecondaryTimeZoneId { get; }


    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
