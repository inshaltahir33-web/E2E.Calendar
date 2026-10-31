using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class MonthViewModel : CalendarPageViewModel
{
    private const int MaxLinesPerCell = 3;

    private readonly EventService _events;
    private readonly IEventRepository _repository;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly CalendarItemQueryService _items;
    private readonly IClock _clock;

    [ObservableProperty]
    private IReadOnlyList<MonthDay> _days = Array.Empty<MonthDay>();

    [ObservableProperty]
    private IReadOnlyList<MonthWeek> _weeks = Array.Empty<MonthWeek>();

    [ObservableProperty]
    private IReadOnlyList<int> _weekNumbers = Array.Empty<int>();

    public MonthViewModel(
        CalendarStateViewModel state,
        EventQueryService query,
        IEventQueryCache cache,
        CalendarItemQueryService items,
        EventService events,
        IEventRepository repository,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock)
        : base(state, query, cache)
    {
        _events = events;
        _repository = repository;
        _reminders = reminders;
        _tasks = tasks;
        _items = items;
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
            var items = await _items.GetItemsAsync(
                timeZone.ToUtc(gridStart.ToDateTime(TimeOnly.MinValue)),
                timeZone.ToUtc(gridEnd.ToDateTime(TimeOnly.MinValue)));

            // Recurring occurrences share the series id, so key the layout by the
            // occurrence itself to keep one bar/chip per occurrence.
            var byOccurrence = new Dictionary<string, CalendarItem>(items.Count, StringComparer.Ordinal);
            var occurrences = new List<LayoutOccurrence>(items.Count);

            foreach (var item in items)
            {
                var key = $"{item.Id}|{item.StartUtc.UtcTicks}";

                byOccurrence[key] = item;
                occurrences.Add(CalendarLayout.ToOccurrence(item, timeZone) with { Id = key });
            }

            // The month grid runs through the same layout the Day and Week views use,
            // so a date lists the same items everywhere. Banners span whole week rows;
            // timed items become single-line chips under them.
            var layout = CalendarLayout.Build(
                occurrences,
                gridStart,
                gridEnd.AddDays(-1),
                State.WeekStart,
                maxBannerLanes: MaxLinesPerCell,
                maxColumns: Math.Max(1, items.Count));

            var weeks = new List<MonthWeek>(layout.Weeks.Count);

            for (var row = 0; row < layout.Weeks.Count; row++)
            {
                var week = layout.Weeks[row];

                weeks.Add(new MonthWeek(
                    row,
                    week.Banners
                        .OrderBy(banner => banner.Lane)
                        .ThenBy(banner => banner.StartDay)
                        .Select(banner => ToBanner(banner, week.Start, byOccurrence))
                        .ToList()));
            }

            var today = DateOnly.FromDateTime(timeZone.ToLocal(_clock.UtcNow));

            var days = new List<MonthDay>(42);

            for (var index = 0; index < 42; index++)
            {
                var date = gridStart.AddDays(index);
                var week = layout.Weeks[index / 7];
                var dayLayout = layout.Days[index];

                var visibleBanners = week.Banners
                    .Where(banner => banner.StartDay <= date && date <= banner.EndDay)
                    .ToList();
                var hiddenBanners = week.HiddenPlacements
                    .Where(banner => banner.StartDay <= date && date <= banner.EndDay)
                    .ToList();

                var lineOffset = visibleBanners.Count == 0
                    ? 0
                    : visibleBanners.Max(banner => banner.Lane) + 1;

                var timed = dayLayout.Items
                    .OrderBy(placement => placement.VisualStart)
                    .ThenBy(placement => placement.Occurrence.Id, StringComparer.Ordinal)
                    .ToList();

                var visibleTimed = timed
                    .Take(Math.Max(0, MaxLinesPerCell - lineOffset))
                    .ToList();

                var allItems = visibleBanners
                    .Concat(hiddenBanners)
                    .Select(banner => banner.Occurrence.Id)
                    .Concat(timed.Select(placement => placement.Occurrence.Id))
                    .Concat(dayLayout.Hidden.Select(placement => placement.Occurrence.Id))
                    .Distinct(StringComparer.Ordinal)
                    .Select(id => byOccurrence[id])
                    .OrderBy(item => item.StartUtc)
                    .ThenBy(item => item.Title, StringComparer.Ordinal)
                    .ToList();

                days.Add(new MonthDay
                {
                    Date = date,
                    DayNumber = date.Day,
                    IsToday = date == today,
                    IsCurrentMonth = date.Month == firstOfMonth.Month && date.Year == firstOfMonth.Year,
                    IsWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                    IsSelected = date == State.SelectedDate,
                    Chips = visibleTimed
                        .Select(placement => ToChip(byOccurrence[placement.Occurrence.Id]))
                        .ToList(),
                    AllItems = allItems,
                    MoreCount = hiddenBanners.Count + (timed.Count - visibleTimed.Count) + dayLayout.Hidden.Count,
                    EventCount = allItems.Count,
                    LineOffset = lineOffset,
                });
            }

            var weekNumbers = new int[6];

            for (var week = 0; week < weekNumbers.Length; week++)
            {
                var weekStart = gridStart.AddDays(week * 7);
                weekNumbers[week] = ISOWeek.GetWeekOfYear(weekStart.ToDateTime(TimeOnly.MinValue));
            }

            Weeks = weeks;
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
        var timeZone = _clock.LocalTimeZone;

        if (await _repository.GetByIdAsync(itemId) is { } calendarEvent)
        {
            var localStart = timeZone.ToLocal(calendarEvent.StartUtc);
            var duration = calendarEvent.EndUtc - calendarEvent.StartUtc;
            var newStartUtc = timeZone.ToUtc(targetDate.ToDateTime(TimeOnly.FromTimeSpan(localStart.TimeOfDay)));

            await _events.UpdateAsync(calendarEvent with { StartUtc = newStartUtc, EndUtc = newStartUtc + duration });

            return true;
        }

        if (await _reminders.GetByIdAsync(itemId) is { DueUtc: { } reminderDue } reminder)
        {
            var newDueUtc = timeZone.ToUtc(targetDate.ToDateTime(TimeOnly.FromTimeSpan(timeZone.ToLocal(reminderDue).TimeOfDay)));

            await _reminders.UpsertAsync(reminder with { DueUtc = newDueUtc, UpdatedUtc = _clock.UtcNow });

            return true;
        }

        if (await _tasks.GetByIdAsync(itemId) is { DueUtc: { } taskDue } task)
        {
            var newDueUtc = timeZone.ToUtc(targetDate.ToDateTime(TimeOnly.FromTimeSpan(timeZone.ToLocal(taskDue).TimeOfDay)));

            await _tasks.UpsertAsync(task with { DueUtc = newDueUtc });

            return true;
        }

        return false;
    }

    private static MonthBanner ToBanner(
        BannerPlacement banner,
        DateOnly weekStart,
        IReadOnlyDictionary<string, CalendarItem> byOccurrence)
    {
        var item = byOccurrence.GetValueOrDefault(banner.Occurrence.Id);

        return new MonthBanner(
            banner.Occurrence.Id,
            item?.Title ?? string.Empty,
            item?.Color,
            banner.StartDay.DayNumber - weekStart.DayNumber,
            banner.EndDay.DayNumber - weekStart.DayNumber,
            banner.Lane,
            banner.ContinuesFromPrev,
            banner.ContinuesToNext);
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
