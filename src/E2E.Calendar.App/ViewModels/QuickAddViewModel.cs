using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.QuickAdd;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Platform;

namespace E2E.Calendar.App.ViewModels;

/// <summary>
/// Backs the Quick Add window: tokenises a phrase into a preview and saves it
/// as either a calendar event or a reminder.
/// </summary>
public sealed partial class QuickAddViewModel : ObservableObject
{
    private static readonly IReadOnlyList<CalendarOption> FallbackCalendars =
        [new CalendarOption { Id = "cal-local", Name = "Calendar" }];

    private readonly IClock _clock;
    private readonly EventService _events;
    private readonly ReminderEngine _engine;
    private readonly ReminderNotificationService _scheduler;
    private readonly ICalendarRepository _calendarRepository;
    private readonly IEventQueryCache _queryCache;

    private ParsedItem? _parsed;
    private bool _reminderMode;

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _kindLabel = "Event";

    [ObservableProperty]
    private string _titlePreview = string.Empty;

    [ObservableProperty]
    private string _whenPreview = string.Empty;

    [ObservableProperty]
    private string _wherePreview = string.Empty;

    [ObservableProperty]
    private string _whenIcon = "\uE787";

    [ObservableProperty]
    private string _whereIcon = "\uE81D";

    [ObservableProperty]
    private string _alertPreview = string.Empty;

    [ObservableProperty]
    private string _alertIcon = "\uEA8F";

    [ObservableProperty]
    private string _calendarPreview = string.Empty;

    [ObservableProperty]
    private bool _hasLocation;

    [ObservableProperty]
    private bool _hasAlert;

    [ObservableProperty]
    private bool _isPriority;

    [ObservableProperty]
    private string _priorityPreview = string.Empty;

    public QuickAddViewModel(
        IClock clock,
        EventService events,
        ReminderEngine engine,
        ReminderNotificationService scheduler,
        ICalendarRepository calendarRepository,
        IEventQueryCache queryCache)
    {
        _clock = clock;
        _events = events;
        _engine = engine;
        _scheduler = scheduler;
        _calendarRepository = calendarRepository;
        _queryCache = queryCache;

        CalendarChoices = new ObservableCollection<CalendarOption>(FallbackCalendars);
    }

    public ObservableCollection<CalendarOption> CalendarChoices { get; }

    [ObservableProperty]
    private CalendarOption? _selectedCalendar;

    public event EventHandler? Saved;

    /// <summary>
    /// Selects what this open captures and clears the previous phrase, so every open
    /// starts empty and a "New reminder" always saves a reminder.
    /// </summary>
    public void Prepare(bool asReminder)
    {
        _reminderMode = asReminder;
        Input = string.Empty;
        Reparse();
    }

    public async Task LoadAsync()
    {
        // The view model is shared between opens, so the last phrase must never
        // survive into the next window.
        Input = string.Empty;

        var calendars = await _calendarRepository.GetAllAsync();

        CalendarChoices.Clear();

        foreach (var calendar in calendars)
        {
            CalendarChoices.Add(new CalendarOption
            {
                Id = calendar.Id,
                Name = calendar.Name,
            });
        }

        if (CalendarChoices.Count == 0)
        {
            CalendarChoices.Add(FallbackCalendars[0]);
        }

        SelectedCalendar = CalendarChoices[0];

        Reparse();
    }

    public void Reparse()
    {
        var parsed = _reminderMode
            ? QuickAddParser.ParseAsReminder(Input, _clock)
            : QuickAddParser.Parse(Input, _clock);
        _parsed = parsed;

        KindLabel = parsed.Kind == ParsedItemKind.Reminder ? "Reminder" : "Event";
        TitlePreview = parsed.Title;
        WhenIcon = parsed.Kind == ParsedItemKind.Reminder ? "\uEA8F" : "\uE787";
        WhenPreview = FormatWhen(parsed);
        WherePreview = parsed.Location ?? string.Empty;
        HasLocation = !string.IsNullOrWhiteSpace(parsed.Location);
        AlertPreview = FormatAlerts(parsed);
        HasAlert = parsed.AlertOffsetsMinutes.Count > 0;
        IsPriority = parsed.Priority != Priority.Normal;
        PriorityPreview = parsed.Priority switch
        {
            Priority.Critical => "Critical",
            Priority.High => "High",
            Priority.Low => "Low",
            _ => string.Empty,
        };

        if (parsed.CalendarHint is { } hint)
        {
            CalendarPreview = hint;

            var match = CalendarChoices.FirstOrDefault(
                calendar => calendar.Name.Contains(hint, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                SelectedCalendar = match;
            }
        }
        else
        {
            CalendarPreview = SelectedCalendar?.Name ?? string.Empty;
        }
    }

    [RelayCommand]
    public void CycleCalendar()
    {
        if (CalendarChoices.Count <= 1)
        {
            return;
        }

        var index = SelectedCalendar is null ? -1 : CalendarChoices.IndexOf(SelectedCalendar);
        SelectedCalendar = CalendarChoices[(index + 1) % CalendarChoices.Count];

        if (_parsed?.CalendarHint is null)
        {
            CalendarPreview = SelectedCalendar.Name;
        }
    }

    public async Task<bool> SaveAsync()
    {
        Reparse();

        if (_parsed is not { } parsed)
        {
            return false;
        }

        var calendarId = SelectedCalendar?.Id
            ?? CalendarChoices.FirstOrDefault()?.Id
            ?? "cal-local";

        if (parsed.Kind == ParsedItemKind.Reminder)
        {
            var now = _clock.UtcNow;

            // Persist the reminder together with its alert so the planner can fire it;
            // "in 25 minutes" therefore becomes a real one-shot countdown reminder.
            await _engine.CreateStandaloneAsync(
                new Reminder
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = parsed.Title,
                    DueUtc = parsed.StartUtc,
                    TimeZoneId = _clock.LocalTimeZone.ToIanaId(),
                    Priority = parsed.Priority,
                    RRule = parsed.RRule,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                },
                alertOffsetMinutes: parsed.AlertOffsetsMinutes.Count > 0
                    ? parsed.AlertOffsetsMinutes[0]
                    : 0);

            _scheduler.Wake();
        }
        else
        {
            var created = await _events.CreateAsync(new EventDraft
            {
                CalendarId = calendarId,
                Title = parsed.Title,
                Location = parsed.Location,
                StartUtc = parsed.StartUtc,
                EndUtc = parsed.EndUtc,
                AllDay = parsed.AllDay,
                TimeZoneId = _clock.LocalTimeZone.ToIanaId(),
                RRule = parsed.RRule,
            });

            await _events.AddAlertsAsync(created.Id, parsed.AlertOffsetsMinutes);
            _queryCache.Invalidate();
        }

        Saved?.Invoke(this, EventArgs.Empty);

        return true;
    }

    private string FormatWhen(ParsedItem parsed)
    {
        var culture = CultureInfo.CurrentCulture;
        var date = parsed.Date == DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(_clock.UtcNow))
            ? "Today"
            : parsed.Date == DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(_clock.UtcNow)).AddDays(1)
                ? "Tomorrow"
                : parsed.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd d MMM", culture);

        if (parsed.AllDay)
        {
            return date;
        }

        if (parsed is { StartTime: { } start, EndTime: { } end })
        {
            return $"{date}, {FormatTime(start)}–{FormatTime(end)}";
        }

        return date;
    }

    private static string FormatTime(TimeOnly time) =>
        DateTime.Today.Add(time.ToTimeSpan()).ToString("t", CultureInfo.CurrentCulture);

    private static string FormatAlerts(ParsedItem parsed)
    {
        if (parsed.AlertOffsetsMinutes.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            " · ",
            parsed.AlertOffsetsMinutes.Select(minutes => minutes == 0
                ? "At start"
                : minutes % 60 == 0
                    ? $"{minutes / 60} h before"
                    : $"{minutes} min before"));
    }

    partial void OnInputChanged(string value) => Reparse();

    partial void OnSelectedCalendarChanged(CalendarOption? value)
    {
        if (_parsed?.CalendarHint is null)
        {
            CalendarPreview = value?.Name ?? string.Empty;
        }
    }
}
