using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class AgendaViewModel : CalendarPageViewModel
{
    private const int PageSizeInDays = 28;
    private const int InitialDaysBefore = 0;
    private const int InitialDaysAfter = 28;

    private readonly AgendaService _agenda;
    private readonly ICalendarRepository _calendars;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;

    private DateOnly _from;
    private DateOnly _to;

    [ObservableProperty]
    private string _stickyHeader = string.Empty;

    [ObservableProperty]
    private bool _hasEarlier;

    [ObservableProperty]
    private bool _hasLater;

    [ObservableProperty]
    private IReadOnlyList<AgendaDayViewModel> _days = Array.Empty<AgendaDayViewModel>();

    public AgendaViewModel(
        CalendarStateViewModel state,
        EventQueryService query,
        IEventQueryCache cache,
        AgendaService agenda,
        ICalendarRepository calendars,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock)
        : base(state, query, cache)
    {
        _agenda = agenda;
        _calendars = calendars;
        _reminders = reminders;
        _tasks = tasks;
        _clock = clock;
    }

    public bool IsEmpty => Days.Count == 0;

    partial void OnDaysChanged(IReadOnlyList<AgendaDayViewModel> value)
    {
        OnPropertyChanged(nameof(IsEmpty));
    }

    public override async Task RefreshAsync()
    {
        var today = Today();
        _from = today.AddDays(-InitialDaysBefore);
        _to = today.AddDays(InitialDaysAfter);

        await LoadRangeAsync();
    }

    public async Task LoadEarlierAsync()
    {
        _from = _from.AddDays(-PageSizeInDays);

        await LoadRangeAsync();
    }

    public async Task LoadLaterAsync()
    {
        _to = _to.AddDays(PageSizeInDays);

        await LoadRangeAsync();
    }

    public async Task ToggleCompletedAsync(AgendaItemViewModel item)
    {
        var completed = !item.IsCompleted;

        if (item.IsReminder)
        {
            var reminder = await _reminders.GetByIdAsync(item.Id);

            if (reminder is not null)
            {
                await _reminders.UpsertAsync(
                    reminder with { DoneUtc = completed ? _clock.UtcNow : null, UpdatedUtc = _clock.UtcNow });
            }
        }
        else if (item.IsTask)
        {
            var task = await _tasks.GetByIdAsync(item.Id);

            if (task is not null)
            {
                await _tasks.UpsertAsync(task with { DoneUtc = completed ? _clock.UtcNow : null });
            }
        }

        item.IsCompleted = completed;
    }

    private DateOnly Today() => DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(_clock.UtcNow));

    private async Task LoadRangeAsync()
    {
        await LoadPeriodAsync(LoadRangeCoreAsync);
    }

    private async Task LoadRangeCoreAsync()
    {
        var timeZone = _clock.LocalTimeZone;
        var fromUtc = timeZone.ToUtc(_from.ToDateTime(TimeOnly.MinValue));
        var toUtc = timeZone.ToUtc(_to.ToDateTime(TimeOnly.MinValue));

        var days = await _agenda.GetDaysAsync(fromUtc, toUtc);
        var calendars = await _calendars.GetAllAsync();
        var calendarNames = calendars.ToDictionary(calendar => calendar.Id, calendar => calendar.Name);
        var culture = CultureInfo.CurrentCulture;
        var today = Today();

        var allItems = days.SelectMany(day => day.Items).ToList();
        var multiDayItems = allItems.Where(item => !IsSingleDay(item)).ToList();

        var singleDayByDate = new Dictionary<DateOnly, List<CalendarItem>>();

        foreach (var item in allItems)
        {
            if (!IsSingleDay(item))
            {
                continue;
            }

            var date = DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc));

            if (!singleDayByDate.TryGetValue(date, out var bucket))
            {
                bucket = new List<CalendarItem>();
                singleDayByDate[date] = bucket;
            }

            bucket.Add(item);
        }

        var agendaDays = new List<AgendaDayViewModel>();

        for (var date = _from; date <= _to; date = date.AddDays(1))
        {
            var dayItems = new List<CalendarItem>();

            if (singleDayByDate.TryGetValue(date, out var singleDay))
            {
                dayItems.AddRange(singleDay);
            }

            foreach (var item in multiDayItems)
            {
                if (SpansDay(item, date, timeZone))
                {
                    dayItems.Add(item);
                }
            }

            if (dayItems.Count == 0)
            {
                continue;
            }

            dayItems.Sort(static (left, right) =>
            {
                var byStart = left.StartUtc.CompareTo(right.StartUtc);

                return byStart != 0
                    ? byStart
                    : string.CompareOrdinal(left.Title, right.Title);
            });

            var weekday = culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek);
            var dayMonth = date.ToDateTime(TimeOnly.MinValue).ToString("d MMM", culture);
            var isToday = date == today;

            agendaDays.Add(new AgendaDayViewModel
            {
                Header = weekday,
                Weekday = weekday,
                DayText = dayMonth,
                IsToday = isToday,
                Items = dayItems.Select(item => new AgendaItemViewModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    Location = item.Location,
                    CalendarName = item.CalendarId is not null && calendarNames.TryGetValue(item.CalendarId, out var name)
                        ? name
                        : item.CalendarName,
                    Color = item.Color,
                    TimeLabel = item.TimeLabel,
                    HasTime = item.HasTime,
                    IsReminder = item.Kind == CalendarItemKind.Reminder,
                    IsTask = item.Kind == CalendarItemKind.Task,
                    IsDone = item.IsDone,
                    IsCompleted = item.IsDone,
                }).ToList(),
            });
        }

        HasEarlier = _from < today;
        HasLater = _to > today.AddDays(InitialDaysAfter);

        Days = agendaDays;

        StickyHeader = agendaDays.Count > 0
            ? $"{agendaDays[0].Header} \u00B7 {agendaDays[0].DayText} \u00B7 {agendaDays[0].CountLabel}"
            : string.Empty;
    }

    private static bool IsSingleDay(CalendarItem item) =>
        !item.AllDay && item.EndUtc - item.StartUtc <= TimeSpan.FromDays(1);

    private static bool SpansDay(CalendarItem item, DateOnly date, TimeZoneInfo timeZone)
    {
        var start = DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc));

        if (IsSingleDay(item))
        {
            return start == date;
        }

        var end = DateOnly.FromDateTime(timeZone.ToLocal(item.EndUtc));

        return date >= start && date < end;
    }
}
