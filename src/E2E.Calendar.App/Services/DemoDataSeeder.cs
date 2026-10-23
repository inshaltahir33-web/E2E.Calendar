using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.Services;

/// <summary>
/// DEBUG-only demo content. Never registered in Release builds.
/// </summary>
public sealed class DemoDataSeeder : IDemoDataSeeder
{
    private const string IdPrefix = "demo-";

    private readonly ICalendarRepository _calendars;
    private readonly IEventRepository _events;
    private readonly IReminderRepository _reminders;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;

    public DemoDataSeeder(
        ICalendarRepository calendars,
        IEventRepository events,
        IReminderRepository reminders,
        ITaskRepository tasks,
        IClock clock)
    {
        _calendars = calendars;
        _events = events;
        _reminders = reminders;
        _tasks = tasks;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await ClearAsync(cancellationToken);

        var now = _clock.UtcNow;
        var localZone = _clock.LocalTimeZone;
        var today = DateOnly.FromDateTime(localZone.ToLocal(now));
        var monday = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 0));

        const string personal = "Personal";
        const string work = "Work";
        const string family = "Family";

        var existing = await _calendars.GetAllAsync(cancellationToken);

        var personalId = await ResolveCalendarAsync(existing, personal, CalendarPalette.Personal, cancellationToken);
        var workId = await ResolveCalendarAsync(existing, work, CalendarPalette.Work, cancellationToken);
        var familyId = await ResolveCalendarAsync(existing, family, CalendarPalette.Family, cancellationToken);

        for (var week = -1; week <= 1; week++)
        {
            var weekStart = monday.AddDays(week * 7);

            await SeedWeekAsync(weekStart, personalId, workId, familyId, now, cancellationToken);
        }

        // Recurring series are seeded once. Seeding one master per demo week
        // made every weekly occurrence expand once per master, which showed the
        // same standup/gym slot two or three times in Day, Week and Agenda.
        var seriesStart = monday.AddDays(-7);

        foreach (var series in DemoSchedule.Recurring(seriesStart))
        {
            var calendarId = series.Calendar switch
            {
                "Work" => workId,
                "Personal" => personalId,
                "Family" => familyId,
                _ => personalId,
            };

            await AddRecurringEventAsync(
                calendarId,
                series.Title,
                series.Date,
                series.Hour,
                series.Minute,
                series.DurationMinutes,
                series.RRule!,
                series.Color,
                now,
                cancellationToken);
        }

        await SeedRemindersAsync(now, localZone, cancellationToken);
        await SeedTasksAsync(now, localZone, cancellationToken);
    }

    private async Task<string> ResolveCalendarAsync(
        IReadOnlyList<Core.Models.Calendar> existing,
        string name,
        string color,
        CancellationToken cancellationToken)
    {
        var match = existing.FirstOrDefault(calendar =>
            string.Equals(calendar.Name, name, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            if (!match.Visible)
            {
                await _calendars.UpsertAsync(match with { Visible = true }, cancellationToken);
            }

            return match.Id;
        }

        var id = IdPrefix + "cal-" + name.ToLowerInvariant();

        await _calendars.UpsertAsync(new Core.Models.Calendar
        {
            Id = id,
            Name = name,
            Color = color,
            IsLocal = true,
            Visible = true,
        }, cancellationToken);

        return id;
    }

    private async Task SeedWeekAsync(
        DateOnly weekStart,
        string personal,
        string work,
        string family,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Three overlapping events at the same time on Wednesday.
        await AddEventAsync(work, "Design review", weekStart.AddDays(2), 10, 0, 90, CalendarPalette.Work, now, cancellationToken);
        await AddEventAsync(work, "1:1 with Priya", weekStart.AddDays(2), 10, 30, 60, CalendarPalette.Focus, now, cancellationToken);
        await AddEventAsync(personal, "Dentist", weekStart.AddDays(2), 10, 45, 45, CalendarPalette.Health, now, cancellationToken);

        // Short 15 minute event and a long 8 hour block.
        await AddEventAsync(work, "Quick sync", weekStart.AddDays(1), 9, 15, 15, CalendarPalette.Work, now, cancellationToken);
        await AddEventAsync(work, "Focus day: roadmap", weekStart.AddDays(3), 9, 0, 480, CalendarPalette.Focus, now, cancellationToken);

        // Edge of day events.
        await AddEventAsync(personal, "Late night deploy", weekStart.AddDays(4), 0, 30, 30, CalendarPalette.Other, now, cancellationToken);
        await AddEventAsync(personal, "Night shift handover", weekStart.AddDays(4), 23, 0, 60, CalendarPalette.Other, now, cancellationToken);

        // All day and multi day events.
        await AddAllDayEventAsync(family, "Birthday party", weekStart.AddDays(5), 1, CalendarPalette.Birthday, now, cancellationToken);
        await AddAllDayEventAsync(family, "School trip", weekStart.AddDays(1), 3, CalendarPalette.Holiday, now, cancellationToken);
        await AddAllDayEventAsync(personal, "Public holiday", weekStart.AddDays(0), 1, CalendarPalette.Holiday, now, cancellationToken);

        // Meeting link event.
        await AddEventAsync(
            work,
            "Product sync (Teams)",
            weekStart.AddDays(4),
            15,
            0,
            60,
            CalendarPalette.Work,
            now,
            cancellationToken,
            meetingUrl: "https://teams.microsoft.com/l/meetup-join/demo-product-sync");

        // A few regular events spread across the week.
        await AddEventAsync(work, "Sprint planning", weekStart.AddDays(0), 11, 0, 120, CalendarPalette.Work, now, cancellationToken);
        await AddEventAsync(work, "Customer call", weekStart.AddDays(1), 14, 0, 45, CalendarPalette.Social, now, cancellationToken);
        await AddEventAsync(personal, "Grocery run", weekStart.AddDays(1), 17, 30, 60, CalendarPalette.Other, now, cancellationToken);
        await AddEventAsync(personal, "Workout", weekStart.AddDays(2), 19, 0, 60, CalendarPalette.Health, now, cancellationToken);
        await AddEventAsync(family, "Family dinner", weekStart.AddDays(3), 19, 30, 120, CalendarPalette.Family, now, cancellationToken);
        await AddEventAsync(family, "Football match", weekStart.AddDays(6), 11, 0, 120, CalendarPalette.Family, now, cancellationToken);
        await AddEventAsync(work, "Retro", weekStart.AddDays(4), 16, 0, 60, CalendarPalette.Work, now, cancellationToken);
        await AddEventAsync(personal, "Coffee with Sam", weekStart.AddDays(2), 12, 30, 60, CalendarPalette.Social, now, cancellationToken);
        await AddEventAsync(work, "Interview: backend engineer", weekStart.AddDays(3), 13, 0, 90, CalendarPalette.Focus, now, cancellationToken);
        await AddEventAsync(personal, "Pharmacy", weekStart.AddDays(6), 10, 0, 30, CalendarPalette.Health, now, cancellationToken);
    }

    private async Task AddEventAsync(
        string calendarId,
        string title,
        DateOnly date,
        int hour,
        int minute,
        int durationMinutes,
        string color,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string? meetingUrl = null)
    {
        var zone = _clock.LocalTimeZone;
        var startLocal = date.ToDateTime(new TimeOnly(hour, minute));
        var start = zone.ToUtc(startLocal);

        await _events.UpsertAsync(new CalendarEvent
        {
            Id = IdPrefix + Guid.NewGuid().ToString("N")[..8],
            CalendarId = calendarId,
            Title = title,
            StartUtc = start,
            EndUtc = start.AddMinutes(durationMinutes),
            TimeZoneId = zone.ToIanaId(),
            Color = color,
            MeetingUrl = meetingUrl,
            CreatedUtc = now,
            UpdatedUtc = now,
        }, cancellationToken);
    }

    private async Task AddAllDayEventAsync(
        string calendarId,
        string title,
        DateOnly date,
        int dayCount,
        string color,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var zone = _clock.LocalTimeZone;
        var start = zone.ToUtc(date.ToDateTime(TimeOnly.MinValue));
        var end = zone.ToUtc(date.AddDays(dayCount).ToDateTime(TimeOnly.MinValue));

        await _events.UpsertAsync(new CalendarEvent
        {
            Id = IdPrefix + Guid.NewGuid().ToString("N")[..8],
            CalendarId = calendarId,
            Title = title,
            StartUtc = start,
            EndUtc = end,
            AllDay = true,
            TimeZoneId = zone.ToIanaId(),
            Color = color,
            CreatedUtc = now,
            UpdatedUtc = now,
        }, cancellationToken);
    }

    private async Task AddRecurringEventAsync(
        string calendarId,
        string title,
        DateOnly date,
        int hour,
        int minute,
        int durationMinutes,
        string rrule,
        string color,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var zone = _clock.LocalTimeZone;
        var start = zone.ToUtc(date.ToDateTime(new TimeOnly(hour, minute)));

        await _events.UpsertAsync(new CalendarEvent
        {
            Id = IdPrefix + Guid.NewGuid().ToString("N")[..8],
            CalendarId = calendarId,
            Title = title,
            StartUtc = start,
            EndUtc = start.AddMinutes(durationMinutes),
            RRule = rrule,
            TimeZoneId = zone.ToIanaId(),
            Color = color,
            CreatedUtc = now,
            UpdatedUtc = now,
        }, cancellationToken);
    }

    private async Task SeedRemindersAsync(
        DateTimeOffset now,
        TimeZoneInfo zone,
        CancellationToken cancellationToken)
    {
        var reminders = new (string Title, int OffsetMinutes, Priority Priority, string? RRule, bool Flagged)[]
        {
            ("Pay the rent", 2880, Priority.High, "FREQ=MONTHLY;BYMONTHDAY=1", true),
            ("Take medicine", -120, Priority.Normal, "FREQ=DAILY", true),
            ("Call the dentist", 60, Priority.Normal, null, false),
            ("Submit timesheet", -60, Priority.High, null, true),
            ("Water the plants", 180, Priority.Low, "FREQ=WEEKLY", false),
            ("Renew passport", 600, Priority.Normal, null, false),
        };

        foreach (var reminder in reminders)
        {
            await _reminders.UpsertAsync(new Reminder
            {
                Id = IdPrefix + Guid.NewGuid().ToString("N")[..8],
                Title = reminder.Title,
                DueUtc = now.AddMinutes(reminder.OffsetMinutes),
                TimeZoneId = zone.ToIanaId(),
                RRule = reminder.RRule,
                Priority = reminder.Priority,
                Flagged = reminder.Flagged,
                CreatedUtc = now,
                UpdatedUtc = now,
            }, cancellationToken);
        }
    }

    private async Task SeedTasksAsync(
        DateTimeOffset now,
        TimeZoneInfo zone,
        CancellationToken cancellationToken)
    {
        var tasks = new (string Title, int OffsetHours, bool HasTime, bool Done)[]
        {
            ("Write weekly report", 4, true, false),
            ("Review PR backlog", 8, true, false),
            ("Book dentist appointment", 24, false, false),
            ("Update onboarding docs", 30, false, true),
            ("Send invoice", 2, true, false),
            ("Plan weekend trip", 48, false, false),
            ("Reply to Sam", 0, false, true),
            ("Renew parking permit", 72, false, false),
        };

        foreach (var task in tasks)
        {
            await _tasks.UpsertAsync(new TaskItem
            {
                Id = IdPrefix + Guid.NewGuid().ToString("N")[..8],
                Title = task.Title,
                DueUtc = now.AddHours(task.OffsetHours),
                HasTime = task.HasTime,
                DoneUtc = task.Done ? now.AddHours(-1) : null,
            }, cancellationToken);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var events = await _events.GetInRangeAsync(null, now.AddYears(-2), now.AddYears(2), cancellationToken);

        foreach (var calendarEvent in events.Where(item => item.Id.StartsWith(IdPrefix, StringComparison.Ordinal)))
        {
            await _events.UpsertAsync(calendarEvent with { Deleted = true, UpdatedUtc = now }, cancellationToken);
        }

        var reminders = await _reminders.GetInRangeAsync(now.AddYears(-2), now.AddYears(2), cancellationToken);

        foreach (var reminder in reminders.Where(item => item.Id.StartsWith(IdPrefix, StringComparison.Ordinal)))
        {
            await _reminders.UpsertAsync(reminder with { Deleted = true, UpdatedUtc = now }, cancellationToken);
        }

        var tasks = await _tasks.GetAllAsync(cancellationToken);

        foreach (var task in tasks.Where(item => item.Id.StartsWith(IdPrefix, StringComparison.Ordinal)))
        {
            await _tasks.UpsertAsync(task with { Deleted = true }, cancellationToken);
        }

        var calendars = await _calendars.GetAllAsync(cancellationToken);

        foreach (var calendar in calendars.Where(item => item.Id.StartsWith(IdPrefix, StringComparison.Ordinal)))
        {
            await _calendars.UpsertAsync(calendar with { Visible = false }, cancellationToken);
        }
    }
}
