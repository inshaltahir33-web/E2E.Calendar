using System.Text.Json;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Platform;

public sealed class WidgetAgendaCardService
{
    private readonly IEventRepository _events;
    private readonly IClock _clock;

    public WidgetAgendaCardService(IEventRepository events, IClock clock)
    {
        _events = events;
        _clock = clock;
    }

    public async Task<string> CreateTodayCardAsync(CancellationToken cancellationToken = default)
    {
        var localDate = DateOnly.FromDateTime(_clock.LocalTimeZone.ToLocal(_clock.UtcNow));
        var from = _clock.LocalTimeZone.ToUtc(localDate.ToDateTime(TimeOnly.MinValue));
        var to = _clock.LocalTimeZone.ToUtc(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var events = await _events.GetInRangeAsync(null, from, to, cancellationToken);

        var body = events
            .Where(item => !item.Deleted)
            .OrderBy(item => item.StartUtc)
            .Take(8)
            .Select(CreateEventBlock)
            .ToArray();

        if (body.Length == 0)
        {
            body =
            [
                new
                {
                    type = "TextBlock",
                    text = "No more events today",
                    wrap = true,
                    weight = "Bolder",
                },
            ];
        }

        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            speak = body.Length == 1 ? "No more events today" : "Today's upcoming events",
            body,
        };

        return JsonSerializer.Serialize(card);
    }

    private object CreateEventBlock(CalendarEvent item)
    {
        var zone = item.TimeZoneId.TryToTimeZoneInfo(out var eventZone)
            ? eventZone
            : _clock.LocalTimeZone;
        var time = item.AllDay
            ? "All day"
            : $"{zone.ToLocal(item.StartUtc):t} - {zone.ToLocal(item.EndUtc):t}";

        return new
        {
            type = "ColumnSet",
            columns = new object[]
            {
                new
                {
                    type = "Column",
                    width = "auto",
                    items = new[]
                    {
                        new { type = "TextBlock", text = " ", color = "Accent", width = "4px" },
                    },
                },
                new
                {
                    type = "Column",
                    width = "stretch",
                    items = new object[]
                    {
                        new { type = "TextBlock", text = time, size = "Small", isSubtle = true },
                        new { type = "TextBlock", text = item.Title, wrap = true, weight = "Bolder" },
                    },
                },
            },
        };
    }
}
