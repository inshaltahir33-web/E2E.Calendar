using System.Collections.ObjectModel;
using E2E.Calendar.App.Controls;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public abstract class TimeGridViewModel : CalendarPageViewModel
{
    protected TimeGridViewModel(CalendarStateViewModel state, EventQueryService query, IEventQueryCache cache)
        : base(state, query, cache)
    {
    }

    public IReadOnlyList<TimeGridDay> Days { get; private set; } = Array.Empty<TimeGridDay>();

    public IReadOnlyList<CalendarItem> Items { get; private set; } = Array.Empty<CalendarItem>();

    public abstract int DayCount { get; }

    public DateTimeOffset? Now => State.UtcNow;

    public ClockFormat ClockFormat { get; set; } = ClockFormat.TwentyFourHour;

    public string? SecondaryTimeZoneId { get; set; } = "UTC";

    public override async Task RefreshAsync()
    {
        await LoadPeriodAsync(async () =>
        {
            var period = State.Period;
            var items = await Query.GetItemsAsync(period.FromUtc, period.ToUtc);
            var timeZone = TimeZoneInfo.Local;
            var today = State.LocalDate;

            var days = new List<TimeGridDay>(DayCount);
            var start = DateOnly.FromDateTime(timeZone.ToLocal(period.FromUtc));

            for (var index = 0; index < DayCount; index++)
            {
                var date = start.AddDays(index);

                days.Add(new TimeGridDay(
                    date,
                    date.ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture),
                    date.ToString("ddd", System.Globalization.CultureInfo.CurrentCulture),
                    date == today,
                    date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday));
            }

            var visible = new HashSet<string>(
                days.Select(day => day.Date.ToString("yyyy-MM-dd")),
                StringComparer.Ordinal);

            var visibleItems = items
                .Where(item => visible.Contains(DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc)).ToString("yyyy-MM-dd")))
                .OrderBy(item => item.StartUtc)
                .ToList();

            Days = days;
            Items = visibleItems;

            OnPropertyChanged(nameof(Days));
            OnPropertyChanged(nameof(Items));
        });
    }
}
