using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ConflictResolutionServiceTests
{
    [Fact]
    public void Preserves_dirty_local_event_when_remote_changed()
    {
        var local = Event("local") with { Dirty = true, Title = "Local edit" };
        var remote = Event("remote") with { Title = "Remote edit" };

        new ConflictResolutionService().Resolve(local, remote).Should().Be(local);
    }

    [Fact]
    public void Accepts_remote_event_when_local_cache_is_clean()
    {
        var local = Event("local");
        var remote = Event("remote") with { Title = "Remote edit", Dirty = true };

        var result = new ConflictResolutionService().Resolve(local, remote);

        result.Title.Should().Be("Remote edit");
        result.Dirty.Should().BeFalse();
    }

    private static CalendarEvent Event(string id) => new()
    {
        Id = id,
        CalendarId = "calendar",
        Title = "Event",
        StartUtc = DateTimeOffset.UtcNow,
        EndUtc = DateTimeOffset.UtcNow.AddHours(1),
        TimeZoneId = "UTC",
        CreatedUtc = DateTimeOffset.UtcNow,
        UpdatedUtc = DateTimeOffset.UtcNow,
    };
}
