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

    public ObservableCollection<TimeGridDay> Days { get; } = new();

    public ObservableCollection<CalendarItem> Items { get; } = new();

    public abstract int DayCount { get; }

    public override async Task RefreshAsync()
    {
        var period = State.Period;
        var items = await Query.GetItemsAsync(period.FromUtc, period.ToUtc);
        var timeZone = TimeZoneInfo.Local;

        var days = new List<TimeGridDay>(DayCount);
        var start = DateOnly.FromDateTime(timeZone.ToLocal(period.FromUtc));

        for (var index = 0; index < DayCount; index++)
        {
            var date = start.AddDays(index);

            days.Add(new TimeGridDay(date, date.ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat)));
        }

        var visible = new HashSet<string>(days.Select(day => day.Date.ToString("yyyy-MM-dd")), StringComparer.Ordinal);

        var visibleItems = items
            .Where(item => visible.Contains(DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc)).ToString("yyyy-MM-dd")))
            .OrderBy(item => item.StartUtc)
            .ToList();

        Days.Clear();

        foreach (var day in days)
        {
            Days.Add(day);
        }

        Items.Clear();

        foreach (var item in visibleItems)
        {
            Items.Add(item);
        }
    }
}
