using System.Collections.ObjectModel;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed class MonthViewModel : CalendarPageViewModel
{
    public MonthViewModel(CalendarStateViewModel state, EventQueryService query)
        : base(state, query)
    {
    }

    public ObservableCollection<MonthDay> Days { get; } = new();

    public override async Task RefreshAsync()
    {
        var period = State.Period;
        var items = await Query.GetItemsAsync(period.FromUtc, period.ToUtc);
        var timeZone = TimeZoneInfo.Local;

        var byDay = items
            .GroupBy(item => DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc)))
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.StartUtc).ToList());

        var firstOfMonth = DateOnly.FromDateTime(timeZone.ToLocal(period.FromUtc));
        var gridStart = CalendarPeriod.StartOfWeek(firstOfMonth);
        var today = DateOnly.FromDateTime(timeZone.ToLocal(State.CurrentDate));

        var days = new List<MonthDay>(42);

        for (var index = 0; index < 42; index++)
        {
            var date = gridStart.AddDays(index);

            byDay.TryGetValue(date, out var dayItems);
            dayItems ??= new List<CalendarItem>();

            days.Add(new MonthDay
            {
                Date = date,
                DayNumber = date.Day,
                IsToday = date == today,
                IsCurrentMonth = date.Month == firstOfMonth.Month && date.Year == firstOfMonth.Year,
                Chips = dayItems.Take(3).Select(ToChip).ToList(),
                AllItems = dayItems,
                MoreCount = Math.Max(0, dayItems.Count - 3),
                EventCount = dayItems.Count,
            });
        }

        Days.Clear();

        foreach (var day in days)
        {
            Days.Add(day);
        }
    }

    private static MonthChip ToChip(CalendarItem item) => new()
    {
        Title = item.Title,
        Color = item.Color,
    };
}
