using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class MonthViewModel : CalendarPageViewModel
{
    private readonly EventService _events;
    private readonly IEventRepository _repository;
    private readonly IClock _clock;

    [ObservableProperty]
    private bool _showWeekNumbers;

    [ObservableProperty]
    private IReadOnlyList<MonthDay> _days = Array.Empty<MonthDay>();

    [ObservableProperty]
    private IReadOnlyList<int> _weekNumbers = Array.Empty<int>();

    public MonthViewModel(
        CalendarStateViewModel state,
        EventQueryService query,
        IEventQueryCache cache,
        EventService events,
        IEventRepository repository,
        IClock clock)
        : base(state, query, cache)
    {
        _events = events;
        _repository = repository;
        _clock = clock;
    }

    public override async Task RefreshAsync()
    {
        await LoadPeriodAsync(async () =>
        {
            var period = State.Period;
            var timeZone = _clock.LocalTimeZone;
            var firstOfMonth = DateOnly.FromDateTime(timeZone.ToLocal(period.FromUtc));
            var gridStart = CalendarPeriod.StartOfWeek(firstOfMonth, State.WeekStart);

            // The month view always renders six full weeks, so query the whole
            // 42-day grid — otherwise the leading/trailing adjacent-month cells
            // never receive their events.
            var gridEnd = gridStart.AddDays(42);
            var items = await Query.GetItemsAsync(
                timeZone.ToUtc(gridStart.ToDateTime(TimeOnly.MinValue)),
                timeZone.ToUtc(gridEnd.ToDateTime(TimeOnly.MinValue)));

            var byDay = items
                .GroupBy(item => DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc)))
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.StartUtc).ToList());

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
                    IsWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                    IsSelected = date == State.SelectedDate,
                    Chips = dayItems.Take(3).Select(ToChip).ToList(),
                    AllItems = dayItems,
                    MoreCount = Math.Max(0, dayItems.Count - 3),
                    EventCount = dayItems.Count,
                });
            }

            var weekNumbers = new int[6];

            for (var week = 0; week < weekNumbers.Length; week++)
            {
                var weekStart = gridStart.AddDays(week * 7);
                weekNumbers[week] = ISOWeek.GetWeekOfYear(weekStart.ToDateTime(TimeOnly.MinValue));
            }

            Days = days;
            WeekNumbers = weekNumbers;
        });
    }

    public void SelectDay(DateOnly date)
    {
        State.SelectDateCommand.Execute(date);

        foreach (var day in Days)
        {
            day.IsSelected = day.Date == date;
        }
    }

    public async Task<bool> MoveItemToDateAsync(string itemId, DateOnly targetDate)
    {
        var calendarEvent = await _repository.GetByIdAsync(itemId);

        if (calendarEvent is null)
        {
            return false;
        }

        var timeZone = _clock.LocalTimeZone;
        var localStart = timeZone.ToLocal(calendarEvent.StartUtc);
        var duration = calendarEvent.EndUtc - calendarEvent.StartUtc;

        var newLocalStart = targetDate.ToDateTime(TimeOnly.FromTimeSpan(localStart.TimeOfDay));
        var newStartUtc = timeZone.ToUtc(newLocalStart);

        await _events.UpdateAsync(calendarEvent with { StartUtc = newStartUtc, EndUtc = newStartUtc + duration });

        return true;
    }

    private static MonthChip ToChip(CalendarItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Color = item.Color,
        StartUtc = item.StartUtc,
        IsReminder = item.Kind == CalendarItemKind.Reminder,
        IsTask = item.Kind == CalendarItemKind.Task,
    };
}
