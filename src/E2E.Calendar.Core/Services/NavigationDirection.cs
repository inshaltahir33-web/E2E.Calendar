using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Shared helper for determining the navigation direction between two dates.
/// </summary>
public static class NavigationDirection
{
    public static NavDirection Get(DateOnly from, DateOnly to)
    {
        if (to > from)
        {
            return NavDirection.Next;
        }

        if (to < from)
        {
            return NavDirection.Previous;
        }

        return NavDirection.None;
    }
}
