namespace E2E.Calendar.Core.Tests;

using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

public sealed class CalendarServiceTests
{
    private readonly ICalendarRepository _repository = Substitute.For<ICalendarRepository>();

    [Fact]
    public async Task The_default_calendars_are_seeded_on_a_first_run()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Calendar>());

        var calendars = await new CalendarService(_repository).EnsureDefaultsAsync();

        calendars.Should().HaveCount(2);
        calendars.Select(c => c.Name).Should().BeEquivalentTo(["Personal", "Work"]);

        await _repository.Received(1).UpsertAsync(
            Arg.Is<Calendar>(c => c.Id == CalendarService.PersonalCalendarId && c.Name == "Personal"),
            Arg.Any<CancellationToken>());

        await _repository.Received(1).UpsertAsync(
            Arg.Is<Calendar>(c => c.Id == CalendarService.WorkCalendarId && c.Name == "Work"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_seeded_calendars_are_local_visible_and_coloured()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Calendar>());

        var calendars = await new CalendarService(_repository).EnsureDefaultsAsync();

        calendars.Should().OnlyContain(calendar => calendar.IsLocal);
        calendars.Should().OnlyContain(calendar => calendar.Visible);
        calendars.Should().OnlyContain(calendar => !calendar.ReadOnly);
        calendars.Select(calendar => calendar.Color).Should().OnlyContain(color => !string.IsNullOrWhiteSpace(color));
    }

    [Fact]
    public async Task Existing_calendars_are_never_overwritten_by_the_seeding()
    {
        var existing = new Calendar { Id = "cal-custom", Name = "Holidays", IsLocal = true };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { existing });

        var calendars = await new CalendarService(_repository).EnsureDefaultsAsync();

        calendars.Should().HaveCount(1);
        calendars[0].Id.Should().Be("cal-custom");

        await _repository.DidNotReceive().UpsertAsync(Arg.Any<Calendar>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_calendars_can_be_listed()
    {
        var stored = new Calendar { Id = "cal-1", Name = "Personal", IsLocal = true };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { stored });

        var calendars = await new CalendarService(_repository).GetAllAsync();

        calendars.Should().HaveCount(1);
        calendars[0].Name.Should().Be("Personal");
    }
}
