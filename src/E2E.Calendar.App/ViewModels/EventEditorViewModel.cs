using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class EventEditorViewModel : ObservableObject
{
    private readonly EventService _events;
    private readonly RecurrenceEditService _recurrenceEdit;
    private readonly EventTemplateService _templateService;
    private readonly IEventRepository _eventRepository;
    private readonly IAlertRepository _alerts;
    private readonly ICalendarRepository _calendarRepository;
    private readonly IEventQueryCache _queryCache;
    private readonly IClock _clock;

    private CalendarEvent? _master;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isAllDay;

    [ObservableProperty]
    private DateTimeOffset _startUtc;

    [ObservableProperty]
    private DateTimeOffset _endUtc;

    [ObservableProperty]
    private string _timeZoneId = "UTC";

    [ObservableProperty]
    private string? _location;

    [ObservableProperty]
    private string? _meetingUrl;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _calendarId;

    [ObservableProperty]
    private string? _color;

    [ObservableProperty]
    private string? _url;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private bool _isRecurring;

    [ObservableProperty]
    private string _eventId = string.Empty;

    [ObservableProperty]
    private RecurrenceFrequency _frequency = RecurrenceFrequency.None;

    [ObservableProperty]
    private int _interval = 1;

    [ObservableProperty]
    private MonthlyPattern _monthlyPattern = MonthlyPattern.ByDate;

    [ObservableProperty]
    private int _monthlyDay = 1;

    [ObservableProperty]
    private int _monthlyWeekdayOrdinal = 1;

    [ObservableProperty]
    private DayOfWeek _monthlyWeekday = DayOfWeek.Monday;

    [ObservableProperty]
    private int _yearlyMonth = 1;

    [ObservableProperty]
    private RecurrenceEnd _recurrenceEnd = RecurrenceEnd.Never;

    [ObservableProperty]
    private DateTimeOffset? _repeatEndDate;

    [ObservableProperty]
    private int _occurrenceCount = 10;

    [ObservableProperty]
    private ObservableCollection<DayOfWeek> _weeklyDays = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableTimeZones = new();

    [ObservableProperty]
    private ObservableCollection<CalendarOption> _calendarOptions = new();

    [ObservableProperty]
    private ObservableCollection<string> _palette = new(CalendarPalette.All);

    [ObservableProperty]
    private ObservableCollection<EventTemplate> _templates = new();

    [ObservableProperty]
    private ObservableCollection<AlertRow> _alertRows = new();

    [ObservableProperty]
    private MeetingLink? _detectedMeeting;

    public EventEditorViewModel(
        EventService events,
        RecurrenceEditService recurrenceEdit,
        EventTemplateService templates,
        IEventRepository eventRepository,
        IAlertRepository alerts,
        ICalendarRepository calendars,
        IEventQueryCache queryCache,
        IClock clock)
    {
        _events = events;
        _recurrenceEdit = recurrenceEdit;
        _templateService = templates;
        _eventRepository = eventRepository;
        _alerts = alerts;
        _calendarRepository = calendars;
        _queryCache = queryCache;
        _clock = clock;

        _startUtc = clock.UtcNow;
        _endUtc = clock.UtcNow.AddHours(1);
        _timeZoneId = clock.LocalTimeZone.ToIanaId();

        AvailableTimeZones = new ObservableCollection<string>(
        [
            "UTC",
            clock.LocalTimeZone.ToIanaId(),
            "Asia/Kolkata",
            "Europe/London",
            "Europe/Berlin",
            "America/New_York",
            "America/Los_Angeles",
            "Australia/Sydney",
        ]);

        Templates = new ObservableCollection<EventTemplate>(templates.GetAvailable());
    }

    public DateTimeOffset? OccurrenceStartUtc { get; set; }

    public event EventHandler? Opened;

    public event EventHandler? CloseRequested;

    public event EventHandler? SaveRequested;

    public event EventHandler? DeleteRequested;

    public async Task OpenAsync(CalendarItem item)
    {
        await LoadByIdAsync(item.Id);

        Opened?.Invoke(this, EventArgs.Empty);
    }

    public async Task OpenNewAsync(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        await LoadNewAsync(startUtc, endUtc);

        Opened?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void RequestSave() => SaveRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void RequestDelete() => DeleteRequested?.Invoke(this, EventArgs.Empty);

    public IReadOnlyList<string> FrequencyOptions { get; } =
        ["Does not repeat", "Daily", "Weekly", "Monthly", "Yearly"];

    public IReadOnlyList<string> MonthlyPatternOptions { get; } =
        ["Day of the month", "Nth weekday"];

    public IReadOnlyList<string> RecurrenceEndOptions { get; } =
        ["Never", "On a date", "After a number of occurrences"];

    public int FrequencyIndex
    {
        get => (int)Frequency;
        set => Frequency = (RecurrenceFrequency)value;
    }

    public int MonthlyPatternIndex
    {
        get => (int)MonthlyPattern;
        set => MonthlyPattern = (MonthlyPattern)value;
    }

    public int RecurrenceEndIndex
    {
        get => (int)RecurrenceEnd;
        set => RecurrenceEnd = (RecurrenceEnd)value;
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasDetectedMeeting => DetectedMeeting is not null;

    public string DetectedMeetingLabel => DetectedMeeting?.JoinLabel ?? string.Empty;

    public string DetectedMeetingUrl => DetectedMeeting?.Url ?? string.Empty;

    public string ColorLabel => Color ?? "No colour";

    public CalendarOption? SelectedCalendar
    {
        get => CalendarOptions.FirstOrDefault(option => option.Id == CalendarId);
        set
        {
            if (value is not null)
            {
                CalendarId = value.Id;
            }
        }
    }

    [RelayCommand]
    public void NextColor()
    {
        MoveColor(1);
    }

    [RelayCommand]
    public void PreviousColor()
    {
        MoveColor(-1);
    }

    private void MoveColor(int step)
    {
        var palette = Palette;

        if (palette.Count == 0)
        {
            return;
        }

        var index = Color is null ? -1 : palette.ToList().IndexOf(Color);

        if (index < 0)
        {
            Color = palette[0];
        }
        else
        {
            Color = palette[(index + step + palette.Count) % palette.Count];
        }

        OnPropertyChanged(nameof(ColorLabel));
    }

    public bool IsWeekly => Frequency == RecurrenceFrequency.Weekly;

    public bool IsMonthly => Frequency == RecurrenceFrequency.Monthly;

    public bool IsYearly => Frequency == RecurrenceFrequency.Yearly;

    public bool IsRepeating => Frequency != RecurrenceFrequency.None;

    public bool IsEndOnDate => RecurrenceEnd == RecurrenceEnd.OnDate;

    public bool IsEndAfterCount => RecurrenceEnd == RecurrenceEnd.AfterCount;

    public string MapSearchUrl =>
        $"https://www.bing.com/maps?q={Uri.EscapeDataString(Location ?? string.Empty)}";

    public Uri? MapSearchUri =>
        Uri.TryCreate(MapSearchUrl, UriKind.Absolute, out var uri) ? uri : null;

    public Uri? DetectedMeetingUri =>
        Uri.TryCreate(DetectedMeetingUrl, UriKind.Absolute, out var uri) ? uri : null;

    public IReadOnlyList<string> WeekdayOptions { get; } =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public bool IsMonthlyByWeekday => MonthlyPattern == MonthlyPattern.ByWeekday;

    public int MonthlyWeekdayIndex
    {
        get => (int)MonthlyWeekday;
        set => MonthlyWeekday = (DayOfWeek)value;
    }

    public double IntervalValue
    {
        get => Interval;
        set => Interval = (int)value;
    }

    public double MonthlyDayValue
    {
        get => MonthlyDay;
        set => MonthlyDay = (int)value;
    }

    public double MonthlyWeekdayOrdinalValue
    {
        get => MonthlyWeekdayOrdinal;
        set => MonthlyWeekdayOrdinal = (int)value;
    }

    public double YearlyMonthValue
    {
        get => YearlyMonth;
        set => YearlyMonth = (int)value;
    }

    public double OccurrenceCountValue
    {
        get => OccurrenceCount;
        set => OccurrenceCount = (int)value;
    }

    public string RecurrenceSummary =>
        RecurrenceRuleBuilder.Describe(new RecurrenceRuleOptions
        {
            Frequency = Frequency,
            Interval = Interval,
            WeeklyDays = WeeklyDays.OrderBy(day => day).ToList(),
            MonthlyPattern = MonthlyPattern,
            MonthlyDay = MonthlyDay,
            MonthlyWeekdayOrdinal = MonthlyWeekdayOrdinal,
            MonthlyWeekday = MonthlyWeekday,
            YearlyMonth = YearlyMonth,
            End = RecurrenceEnd,
            EndDate = RepeatEndDate is { } end ? DateOnly.FromDateTime(end.DateTime) : null,
            Count = OccurrenceCount,
        });

    private TimeZoneInfo EventTimeZone =>
        TimeZoneId.TryToTimeZoneInfo(out var timeZone) ? timeZone : TimeZoneInfo.Utc;

    public DateTimeOffset StartDate
    {
        get => TimeZoneInfo.ConvertTimeFromUtc(StartUtc.UtcDateTime, EventTimeZone);
        set => StartUtc = EventTimeZone.ToUtc(value.DateTime);
    }

    public TimeSpan StartTime
    {
        get => EventTimeZone.ToLocal(StartUtc).TimeOfDay;
        set => StartUtc = EventTimeZone.ToUtc(StartDate.DateTime + value);
    }

    public DateTimeOffset EndDate
    {
        get => TimeZoneInfo.ConvertTimeFromUtc(EndUtc.UtcDateTime, EventTimeZone);
        set => EndUtc = EventTimeZone.ToUtc(value.DateTime);
    }

    public TimeSpan EndTime
    {
        get => EventTimeZone.ToLocal(EndUtc).TimeOfDay;
        set => EndUtc = EventTimeZone.ToUtc(EndDate.DateTime + value);
    }

    public async Task LoadNewAsync(DateTimeOffset? slotStartUtc = null, DateTimeOffset? slotEndUtc = null)
    {
        IsNew = true;
        _master = null;
        EventId = string.Empty;
        Title = string.Empty;
        Location = null;
        MeetingUrl = null;
        Notes = null;
        Url = null;
        ErrorMessage = null;
        DetectedMeeting = null;
        IsAllDay = false;
        IsRecurring = false;
        Frequency = RecurrenceFrequency.None;
        RecurrenceEnd = RecurrenceEnd.Never;
        WeeklyDays = new ObservableCollection<DayOfWeek>();
        AlertRows = new ObservableCollection<AlertRow>();
        OccurrenceStartUtc = null;

        var start = slotStartUtc ?? _clock.UtcNow;
        var end = slotEndUtc ?? start.AddHours(1);

        StartUtc = start;
        EndUtc = end;

        await LoadCalendarsAsync();
    }

    public async Task LoadAsync(CalendarEvent calendarEvent, DateTimeOffset? occurrenceStartUtc = null)
    {
        IsNew = false;
        _master = calendarEvent;
        EventId = calendarEvent.Id;
        Title = calendarEvent.Title;
        Location = calendarEvent.Location;
        MeetingUrl = calendarEvent.MeetingUrl;
        Notes = calendarEvent.Notes;
        Url = calendarEvent.Url;
        Color = calendarEvent.Color;
        CalendarId = calendarEvent.CalendarId;
        TimeZoneId = calendarEvent.TimeZoneId;
        IsAllDay = calendarEvent.AllDay;
        StartUtc = calendarEvent.StartUtc;
        EndUtc = calendarEvent.EndUtc;
        ErrorMessage = null;
        OccurrenceStartUtc = occurrenceStartUtc;

        IsRecurring = !string.IsNullOrWhiteSpace(calendarEvent.RRule);

        if (IsRecurring && RecurrenceRuleBuilder.TryParse(calendarEvent.RRule, out var options))
        {
            Frequency = options.Frequency;
            Interval = options.Interval;
            WeeklyDays = new ObservableCollection<DayOfWeek>(options.WeeklyDays);
            MonthlyPattern = options.MonthlyPattern;
            MonthlyDay = options.MonthlyDay;
            MonthlyWeekdayOrdinal = options.MonthlyWeekdayOrdinal;
            MonthlyWeekday = options.MonthlyWeekday;
            YearlyMonth = options.YearlyMonth;
            RecurrenceEnd = options.End;
            RepeatEndDate = options.EndDate is { } until ? new DateTimeOffset(until.ToDateTime(TimeOnly.MinValue)) : null;
            OccurrenceCount = options.Count ?? 10;
        }

        DetectedMeeting = MeetingLinkDetector.Detect(MeetingUrl ?? Notes);

        await LoadCalendarsAsync();
        await LoadAlertsAsync(calendarEvent.Id);
    }

    public async Task LoadByIdAsync(string id, DateTimeOffset? occurrenceStartUtc = null)
    {
        var calendarEvent = await _eventRepository.GetByIdAsync(id);

        if (calendarEvent is not null)
        {
            await LoadAsync(calendarEvent, occurrenceStartUtc);
        }
    }

    public async Task<bool> UpdateTimesAsync(CalendarItem item, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var calendarEvent = await _eventRepository.GetByIdAsync(item.Id);

        if (calendarEvent is null)
        {
            return false;
        }

        await _events.UpdateAsync(calendarEvent with { StartUtc = startUtc, EndUtc = endUtc });

        return true;
    }

    public async Task<bool> CopyAsync(CalendarItem item, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var calendarEvent = await _eventRepository.GetByIdAsync(item.Id);

        if (calendarEvent is null)
        {
            return false;
        }

        var copy = calendarEvent with
        {
            Id = Guid.NewGuid().ToString("N"),
            StartUtc = startUtc,
            EndUtc = endUtc,
            RemoteId = null,
            ETag = null,
            RecurrenceMasterId = null,
            RecurrenceIdUtc = null,
            CreatedUtc = _clock.UtcNow,
            UpdatedUtc = _clock.UtcNow,
            Dirty = true,
        };

        await _eventRepository.UpsertAsync(copy);
        _queryCache.Invalidate();

        return true;
    }

    public void SetWeeklyDay(DayOfWeek day, bool selected)
    {
        if (selected)
        {
            if (!WeeklyDays.Contains(day))
            {
                WeeklyDays.Add(day);
            }
        }
        else
        {
            WeeklyDays.Remove(day);
        }

        OnPropertyChanged(nameof(IsMonday));
        OnPropertyChanged(nameof(IsTuesday));
        OnPropertyChanged(nameof(IsWednesday));
        OnPropertyChanged(nameof(IsThursday));
        OnPropertyChanged(nameof(IsFriday));
        OnPropertyChanged(nameof(IsSaturday));
        OnPropertyChanged(nameof(IsSunday));
        OnPropertyChanged(nameof(RecurrenceSummary));
    }

    public bool IsMonday
    {
        get => WeeklyDays.Contains(DayOfWeek.Monday);
        set => SetWeeklyDay(DayOfWeek.Monday, value);
    }

    public bool IsTuesday
    {
        get => WeeklyDays.Contains(DayOfWeek.Tuesday);
        set => SetWeeklyDay(DayOfWeek.Tuesday, value);
    }

    public bool IsWednesday
    {
        get => WeeklyDays.Contains(DayOfWeek.Wednesday);
        set => SetWeeklyDay(DayOfWeek.Wednesday, value);
    }

    public bool IsThursday
    {
        get => WeeklyDays.Contains(DayOfWeek.Thursday);
        set => SetWeeklyDay(DayOfWeek.Thursday, value);
    }

    public bool IsFriday
    {
        get => WeeklyDays.Contains(DayOfWeek.Friday);
        set => SetWeeklyDay(DayOfWeek.Friday, value);
    }

    public bool IsSaturday
    {
        get => WeeklyDays.Contains(DayOfWeek.Saturday);
        set => SetWeeklyDay(DayOfWeek.Saturday, value);
    }

    public bool IsSunday
    {
        get => WeeklyDays.Contains(DayOfWeek.Sunday);
        set => SetWeeklyDay(DayOfWeek.Sunday, value);
    }

    public bool IsWeeklyDaySelected(DayOfWeek day) => WeeklyDays.Contains(day);

    public string BuildRecurrenceRule() =>
        RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
        {
            Frequency = Frequency,
            Interval = Interval,
            WeeklyDays = WeeklyDays.OrderBy(day => day).ToList(),
            MonthlyPattern = MonthlyPattern,
            MonthlyDay = MonthlyDay,
            MonthlyWeekdayOrdinal = MonthlyWeekdayOrdinal,
            MonthlyWeekday = MonthlyWeekday,
            YearlyMonth = YearlyMonth,
            End = RecurrenceEnd,
            EndDate = RepeatEndDate is { } end ? DateOnly.FromDateTime(end.DateTime) : null,
            Count = OccurrenceCount,
        }) ?? string.Empty;

    public void ApplyTemplate(EventTemplate template)
    {
        Title = template.Title;
        EndUtc = StartUtc.AddMinutes(template.DurationMinutes);
        Color = template.Color;

        if (AlertRows.Count == 0)
        {
            AlertRows.Add(new AlertRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OwnerType = OwnerType.Event,
                OwnerId = EventId,
                Option = AlertOption.All.FirstOrDefault(option => option.Minutes == template.LeadTimeMinutes)
                    ?? AlertOption.All[0],
            });
        }
    }

    [RelayCommand]
    public void AddAlert()
    {
        AlertRows.Add(new AlertRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerType = OwnerType.Event,
            OwnerId = EventId,
        });
    }

    [RelayCommand]
    public void RemoveAlert(AlertRow? row)
    {
        if (row is not null)
        {
            AlertRows.Remove(row);
        }
    }

    [RelayCommand]
    public void DetectMeetingLink()
    {
        DetectedMeeting = MeetingLinkDetector.Detect(MeetingUrl ?? Notes);
    }

    [RelayCommand]
    public void ClearMeetingLink()
    {
        DetectedMeeting = null;
        MeetingUrl = null;
    }

    public async Task<bool> SaveAsync(RecurrenceEditScope? scope = null)
    {
        var errors = EventValidator.Validate(Title, StartUtc, EndUtc, Url, MeetingUrl);

        if (errors.Count > 0)
        {
            ErrorMessage = string.Join(" ", errors);
            return false;
        }

        ErrorMessage = null;

        var rule = Frequency == RecurrenceFrequency.None ? null : BuildRecurrenceRule();

        var edited = new CalendarEvent
        {
            Id = EventId,
            CalendarId = CalendarId ?? "cal-local",
            Title = Title,
            Notes = Notes,
            Location = Location,
            Url = Url,
            MeetingUrl = MeetingUrl,
            StartUtc = StartUtc,
            EndUtc = EndUtc,
            AllDay = IsAllDay,
            TimeZoneId = TimeZoneId,
            RRule = rule,
            Color = Color,
            CreatedUtc = StartUtc,
            UpdatedUtc = StartUtc,
        };

        CalendarEvent saved;

        if (IsNew)
        {
            saved = await _events.CreateAsync(new EventDraft
            {
                CalendarId = edited.CalendarId,
                Title = edited.Title,
                Notes = edited.Notes,
                Location = edited.Location,
                Url = edited.Url,
                StartUtc = edited.StartUtc,
                EndUtc = edited.EndUtc,
                AllDay = edited.AllDay,
                TimeZoneId = edited.TimeZoneId,
                RRule = edited.RRule,
                Color = edited.Color,
            });
        }
        else if (IsRecurring && scope is { } editScope && _master is not null)
        {
            await _recurrenceEdit.ApplyAsync(_master, OccurrenceStartUtc ?? StartUtc, edited, editScope);
            _queryCache.Invalidate();
            saved = edited;
        }
        else
        {
            saved = await _events.UpdateAsync(edited);
        }

        await SaveAlertsAsync(saved.Id);

        return true;
    }

    public async Task DeleteAsync()
    {
        if (string.IsNullOrEmpty(EventId))
        {
            return;
        }

        await _events.SoftDeleteAsync(EventId);
    }

    private async Task LoadCalendarsAsync()
    {
        var calendars = await _calendarRepository.GetAllAsync();

        CalendarOptions = new ObservableCollection<CalendarOption>(calendars.Select(calendar => new CalendarOption
        {
            Id = calendar.Id,
            Name = calendar.Name,
        }));

        CalendarId ??= calendars.FirstOrDefault()?.Id;
    }

    private async Task LoadAlertsAsync(string ownerId)
    {
        var alerts = await _alerts.GetAlertsForOwnerAsync(OwnerType.Event, ownerId);

        AlertRows = new ObservableCollection<AlertRow>(alerts.Select(alert => new AlertRow
        {
            Id = alert.Id,
            OwnerType = alert.OwnerType,
            OwnerId = alert.OwnerId,
            Option = AlertOption.All.FirstOrDefault(option => option.Minutes == alert.TriggerOffsetMinutes)
                ?? new AlertOption($"{alert.TriggerOffsetMinutes} minutes before", alert.TriggerOffsetMinutes ?? 0),
        }));
    }

    private async Task SaveAlertsAsync(string ownerId)
    {
        foreach (var row in AlertRows)
        {
            await _alerts.UpsertAlertAsync(new Alert
            {
                Id = row.Id,
                OwnerType = OwnerType.Event,
                OwnerId = ownerId,
                TriggerOffsetMinutes = row.OffsetMinutes,
                Priority = Priority.Normal,
            });
        }
    }

    partial void OnMonthlyPatternChanged(MonthlyPattern value)
    {
        OnPropertyChanged(nameof(IsMonthlyByWeekday));
        OnPropertyChanged(nameof(RecurrenceSummary));
    }

    partial void OnFrequencyChanged(RecurrenceFrequency value)
    {
        OnPropertyChanged(nameof(IsWeekly));
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(IsYearly));
        OnPropertyChanged(nameof(IsRepeating));
        OnPropertyChanged(nameof(RecurrenceSummary));
    }

    partial void OnRecurrenceEndChanged(RecurrenceEnd value)
    {
        OnPropertyChanged(nameof(IsEndOnDate));
        OnPropertyChanged(nameof(IsEndAfterCount));
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnDetectedMeetingChanged(MeetingLink? value)
    {
        OnPropertyChanged(nameof(HasDetectedMeeting));
        OnPropertyChanged(nameof(DetectedMeetingLabel));
        OnPropertyChanged(nameof(DetectedMeetingUrl));
    }

    partial void OnColorChanged(string? value) => OnPropertyChanged(nameof(ColorLabel));

    partial void OnTimeZoneIdChanged(string value)
    {
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(EndDate));
        OnPropertyChanged(nameof(EndTime));
    }
}
