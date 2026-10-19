namespace E2E.Calendar.Data.Tests;

using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

public sealed class CalendarRepositoryTests : IAsyncLifetime
{
    private readonly TempDatabase _database = new();
    private CalendarRepository _calendars = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _calendars = new CalendarRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static Calendar CreateCalendar(
        string id = "cal-1",
        string name = "Personal",
        string? color = "#0078D4",
        bool visible = true,
        bool isLocal = true) =>
        new()
        {
            Id = id,
            AccountId = null,
            RemoteId = null,
            Name = name,
            Color = color,
            Visible = visible,
            ReadOnly = false,
            IsLocal = isLocal,
            SyncToken = null,
        };

    [Fact]
    public async Task A_calendar_round_trips_through_the_repository()
    {
        var original = CreateCalendar();

        await _calendars.UpsertAsync(original);
        var loaded = await _calendars.GetByIdAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(original.Id);
        loaded.Name.Should().Be(original.Name);
        loaded.Color.Should().Be(original.Color);
        loaded.Visible.Should().BeTrue();
        loaded.ReadOnly.Should().BeFalse();
        loaded.IsLocal.Should().BeTrue();
    }

    [Fact]
    public async Task A_calendar_that_does_not_exist_is_returned_as_null()
    {
        var loaded = await _calendars.GetByIdAsync("missing");

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task All_calendars_can_be_listed()
    {
        await _calendars.UpsertAsync(CreateCalendar(id: "cal-b", name: "Work"));
        await _calendars.UpsertAsync(CreateCalendar(id: "cal-a", name: "Personal"));

        var calendars = await _calendars.GetAllAsync();

        calendars.Select(calendar => calendar.Name).Should().Equal("Personal", "Work");
    }

    [Fact]
    public async Task Upserting_a_calendar_updates_it_in_place()
    {
        await _calendars.UpsertAsync(CreateCalendar());

        await _calendars.UpsertAsync(CreateCalendar(name: "Personal (renamed)", visible: false));

        var loaded = await _calendars.GetByIdAsync("cal-1");

        loaded!.Name.Should().Be("Personal (renamed)");
        loaded.Visible.Should().BeFalse();
    }

    [Fact]
    public async Task A_calendar_without_a_colour_is_stored_as_null()
    {
        await _calendars.UpsertAsync(CreateCalendar(color: null));

        var loaded = await _calendars.GetByIdAsync("cal-1");

        loaded!.Color.Should().BeNull();
    }
}
