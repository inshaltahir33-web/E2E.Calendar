using E2E.Calendar.Core.Models;
using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class TaskRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Due = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase _database = new();
    private TaskRepository _tasks = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _tasks = new TaskRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static TaskItem CreateTask(
        string id = "task-1",
        string? listId = "list-1",
        bool deleted = false) =>
        new()
        {
            Id = id,
            ListId = listId,
            Title = "Book the venue",
            Notes = "Call twice",
            DueUtc = Due,
            HasTime = true,
            Priority = Priority.Critical,
            RRule = null,
            DoneUtc = null,
            ParentId = null,
            LinkedEventId = "evt-1",
            Dirty = true,
            Deleted = deleted,
        };

    [Fact]
    public async Task A_task_round_trips_through_the_repository()
    {
        var original = CreateTask();

        await _tasks.UpsertAsync(original);
        var loaded = await _tasks.GetByIdAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.ListId.Should().Be("list-1");
        loaded.Title.Should().Be(original.Title);
        loaded.Notes.Should().Be(original.Notes);
        loaded.DueUtc.Should().Be(original.DueUtc);
        loaded.HasTime.Should().BeTrue();
        loaded.Priority.Should().Be(Priority.Critical);
        loaded.LinkedEventId.Should().Be("evt-1");
        loaded.Dirty.Should().BeTrue();
        loaded.Deleted.Should().BeFalse();
    }

    [Fact]
    public async Task All_tasks_can_be_listed()
    {
        await _tasks.UpsertAsync(CreateTask(id: "one"));
        await _tasks.UpsertAsync(CreateTask(id: "two", listId: "list-2"));

        var results = await _tasks.GetAllAsync();

        results.Select(t => t.Id).Should().BeEquivalentTo(["one", "two"]);
    }

    [Fact]
    public async Task Tasks_can_be_listed_by_list()
    {
        await _tasks.UpsertAsync(CreateTask(id: "one"));
        await _tasks.UpsertAsync(CreateTask(id: "two", listId: "list-2"));

        var results = await _tasks.GetByListAsync("list-2");

        results.Select(t => t.Id).Should().Equal("two");
    }

    [Fact]
    public async Task Deleted_tasks_are_hidden_from_the_listings()
    {
        await _tasks.UpsertAsync(CreateTask(id: "gone", deleted: true));

        var results = await _tasks.GetAllAsync();

        results.Should().BeEmpty();
    }
}
