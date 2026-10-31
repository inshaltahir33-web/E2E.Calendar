using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class EventTemplateRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Created = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase _database = new();

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static EventTemplate Template(string id, string name, params int[] alerts) => new()
    {
        Id = id,
        Name = name,
        Title = "Client call",
        DurationMinutes = 30,
        CalendarId = "cal-work",
        Location = "Room 4",
        Notes = "<p>Bring the contract</p>",
        AlertOffsetsMinutes = alerts,
        CreatedUtc = Created,
    };

    [Fact]
    public async Task A_new_database_has_no_templates()
    {
        var repo = new EventTemplateRepository(_database.Database);

        var templates = await repo.GetAllAsync();

        templates.Should().BeEmpty();
    }

    [Fact]
    public async Task Saved_template_round_trips_every_field()
    {
        var repo = new EventTemplateRepository(_database.Database);

        await repo.SaveAsync(Template("tpl-1", "Client call", 10, 60));

        var template = (await repo.GetAllAsync()).Should().ContainSingle().Subject;
        template.Id.Should().Be("tpl-1");
        template.Name.Should().Be("Client call");
        template.Title.Should().Be("Client call");
        template.DurationMinutes.Should().Be(30);
        template.CalendarId.Should().Be("cal-work");
        template.Location.Should().Be("Room 4");
        template.Notes.Should().Be("<p>Bring the contract</p>");
        template.AlertOffsetsMinutes.Should().Equal(10, 60);
        template.CreatedUtc.Should().Be(Created);
    }

    [Fact]
    public async Task Template_without_alerts_round_trips_an_empty_list()
    {
        var repo = new EventTemplateRepository(_database.Database);

        await repo.SaveAsync(Template("tpl-1", "Deep work"));

        var template = (await repo.GetAllAsync()).Single();
        template.AlertOffsetsMinutes.Should().BeEmpty();
    }

    [Fact]
    public async Task Saving_an_existing_id_updates_the_template()
    {
        var repo = new EventTemplateRepository(_database.Database);

        await repo.SaveAsync(Template("tpl-1", "Client call", 10));
        await repo.SaveAsync(Template("tpl-1", "Client call v2", 5, 15) with { DurationMinutes = 45 });

        var templates = await repo.GetAllAsync();
        templates.Should().ContainSingle();
        templates[0].Name.Should().Be("Client call v2");
        templates[0].DurationMinutes.Should().Be(45);
        templates[0].AlertOffsetsMinutes.Should().Equal(5, 15);
    }

    [Fact]
    public async Task Templates_are_listed_by_name()
    {
        var repo = new EventTemplateRepository(_database.Database);

        await repo.SaveAsync(Template("tpl-1", "standup"));
        await repo.SaveAsync(Template("tpl-2", "Gym"));

        (await repo.GetAllAsync()).Select(template => template.Name).Should().Equal("Gym", "standup");
    }

    [Fact]
    public async Task Delete_removes_only_the_requested_template()
    {
        var repo = new EventTemplateRepository(_database.Database);

        await repo.SaveAsync(Template("tpl-1", "Gym"));
        await repo.SaveAsync(Template("tpl-2", "Standup"));

        await repo.DeleteAsync("tpl-1");

        (await repo.GetAllAsync()).Select(template => template.Id).Should().Equal("tpl-2");
    }
}
