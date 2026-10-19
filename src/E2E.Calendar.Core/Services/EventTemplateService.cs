using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class EventTemplateService
{
    private static readonly IReadOnlyList<EventTemplate> BuiltIn =
    [
        new EventTemplate
        {
            Id = "template-focus",
            Name = "Focus block",
            Title = "Focus",
            DurationMinutes = 90,
            LeadTimeMinutes = 5,
            Color = CalendarPalette.Focus,
        },
        new EventTemplate
        {
            Id = "template-standup",
            Name = "Quick standup",
            Title = "Standup",
            DurationMinutes = 15,
            LeadTimeMinutes = 5,
            Color = CalendarPalette.Work,
        },
    ];

    private readonly IEntitlementService _entitlements;

    public EventTemplateService(IEntitlementService entitlements)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        _entitlements = entitlements;
    }

    public IReadOnlyList<EventTemplate> GetAvailable()
    {
        var limit = _entitlements.LimitFor(Feature.CalendarTemplates);

        if (limit < 0)
        {
            return BuiltIn;
        }

        return BuiltIn.Take(limit).ToList();
    }
}
