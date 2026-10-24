using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class ConflictResolutionService
{
    public CalendarEvent Resolve(CalendarEvent local, CalendarEvent remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        if (local.Dirty)
        {
            return local;
        }

        return remote with { Dirty = false };
    }
}
