namespace E2E.Calendar.Core.Services;

using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

public sealed class CalendarService
{
    public const string PersonalCalendarId = "cal-personal";
    public const string WorkCalendarId = "cal-work";

    private readonly ICalendarRepository _calendars;

    public CalendarService(ICalendarRepository calendars)
    {
        ArgumentNullException.ThrowIfNull(calendars);
        _calendars = calendars;
    }

    public Task<IReadOnlyList<Calendar>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _calendars.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Calendar>> EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _calendars.GetAllAsync(cancellationToken);

        if (existing.Count > 0)
        {
            return existing;
        }

        var personal = new Calendar
        {
            Id = PersonalCalendarId,
            Name = "Personal",
            Color = CalendarPalette.Personal,
            Visible = true,
            ReadOnly = false,
            IsLocal = true,
        };

        var work = new Calendar
        {
            Id = WorkCalendarId,
            Name = "Work",
            Color = CalendarPalette.Work,
            Visible = true,
            ReadOnly = false,
            IsLocal = true,
        };

        await _calendars.UpsertAsync(personal, cancellationToken);
        await _calendars.UpsertAsync(work, cancellationToken);

        return new[] { personal, work };
    }
}
