using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;

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

    public ObservableCollection<AgendaDayViewModel> Days { get; } = new();

    public bool IsEmpty => Days.Count == 0;

    public Visibility EmptyStateVisibility => IsEmpty ? Visibility.Visible : Visibility.Collapsed;

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
        var timeZone = _clock.LocalTimeZone;
        var fromUtc = timeZone.ToUtc(_from.ToDateTime(TimeOnly.MinValue));
        var toUtc = timeZone.ToUtc(_to.ToDateTime(TimeOnly.MinValue));

        var days = await _agenda.GetDaysAsync(fromUtc, toUtc);
        var calendars = await _calendars.GetAllAsync();
        var calendarNames = calendars.ToDictionary(calendar => calendar.Id, calendar => calendar.Name);
        var culture = CultureInfo.CurrentCulture;
        var today = Today();

        var allItems = days.SelectMany(day => day.Items).ToList();

        Days.Clear();

        for (var date = _from; date <= _to; date = date.AddDays(1))
        {
            var dayItems = allItems
                .Where(item => SpansDay(item, date, timeZone))
                .OrderBy(item => item.StartUtc)
                .ThenBy(item => item.Title, StringComparer.Ordinal)
                .ToList();

            if (dayItems.Count == 0)
            {
                continue;
            }

            var weekday = culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek);
            var dayMonth = date.ToDateTime(TimeOnly.MinValue).ToString("d MMM", culture);
            var isToday = date == today;

            Days.Add(new AgendaDayViewModel
            {
                Header = isToday ? "Today" : weekday,
                Weekday = weekday,
                DayText = isToday ? $"{weekday} {dayMonth}" : dayMonth,
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

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyStateVisibility));

        if (Days.Count > 0)
        {
            StickyHeader = Days[0].Header + " · " + Days[0].DayText;
        }
    }

    private static bool SpansDay(CalendarItem item, DateOnly date, TimeZoneInfo timeZone)
    {
        var start = DateOnly.FromDateTime(timeZone.ToLocal(item.StartUtc));

        if (!item.AllDay && item.EndUtc - item.StartUtc <= TimeSpan.FromDays(1))
        {
            return start == date;
        }

        var end = DateOnly.FromDateTime(timeZone.ToLocal(item.EndUtc));

        return date >= start && date < end;
    }
}
