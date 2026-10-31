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

    [Fact]
    public async Task Creating_a_calendar_raises_the_change_event_and_reaches_the_shared_list()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Calendar>());

        var calendars = new CalendarService(_repository);
        await calendars.GetAllAsync();

        var changes = new List<CalendarListChangedEventArgs>();
        calendars.Changed += (_, change) => changes.Add(change);

        var created = await calendars.CreateLocalAsync("Student", CalendarPalette.Blue);

        changes.Should().ContainSingle();
        changes[0].Kind.Should().Be(CalendarListChangeKind.Created);
        changes[0].CalendarId.Should().Be(created.Id);

        // Every surface binds to this one list, so the new calendar is visible without a reload.
        calendars.Calendars.Should().ContainSingle(calendar => calendar.Id == created.Id);

        // Surfaces that snapshot (sidebar rows, editor dropdown) re-read through GetAllAsync.
        var snapshot = await calendars.GetAllAsync();

        snapshot.Should().ContainSingle(calendar =>
            calendar.Id == created.Id &&
            calendar.Name == "Student" &&
            calendar.Color == CalendarPalette.Blue &&
            calendar.Visible);
    }

    [Fact]
    public async Task Renaming_and_recolouring_a_calendar_raise_updates_and_refresh_the_shared_list()
    {
        var stored = new Calendar
        {
            Id = "cal-1",
            Name = "Personal",
            Color = CalendarPalette.Blue,
            Visible = true,
            IsLocal = true,
        };

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { stored });

        var calendars = new CalendarService(_repository);
        await calendars.GetAllAsync();

        var changes = new List<CalendarListChangedEventArgs>();
        calendars.Changed += (_, change) => changes.Add(change);

        (await calendars.RenameAsync("cal-1", "Home")).Should().NotBeNull();
        (await calendars.SetColorAsync("cal-1", CalendarPalette.Teal)).Should().NotBeNull();

        changes.Should().HaveCount(2);
        changes.Should().OnlyContain(change =>
            change.Kind == CalendarListChangeKind.Updated && change.CalendarId == "cal-1");

        calendars.Calendars.Should().ContainSingle();
        calendars.Calendars[0].Name.Should().Be("Home");
        calendars.Calendars[0].Color.Should().Be(CalendarPalette.Teal);
    }

    [Fact]
    public async Task Deleting_a_calendar_raises_the_change_event_and_removes_it_from_the_shared_list()
    {
        var stored = new Calendar { Id = "cal-1", Name = "Personal", Visible = true, IsLocal = true };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { stored });

        var calendars = new CalendarService(_repository);
        (await calendars.GetAllAsync()).Should().HaveCount(1);

        var changes = new List<CalendarListChangedEventArgs>();
        calendars.Changed += (_, change) => changes.Add(change);

        await calendars.DeleteAsync("cal-1");

        changes.Should().ContainSingle();
        changes[0].Kind.Should().Be(CalendarListChangeKind.Deleted);
        changes[0].CalendarId.Should().Be("cal-1");
        calendars.Calendars.Should().BeEmpty();
        (await calendars.GetAllAsync()).Should().BeEmpty();
    }
}
