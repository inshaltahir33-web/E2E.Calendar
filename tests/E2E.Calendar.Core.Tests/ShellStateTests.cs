using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ShellStateTests
{
    [Fact]
    public void Calendar_navigation_updates_the_single_shell_state()
    {
        var state = new ShellState();
        var changed = new List<string?>();
        state.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        state.ShowCalendar(CalendarViewMode.Month, new DateOnly(2026, 10, 25));

        state.Mode.Should().Be(ShellMode.Calendar);
        state.CalendarView.Should().Be(CalendarViewMode.Month);
        state.SelectedDate.Should().Be(new DateOnly(2026, 10, 25));
        state.Title.Should().Be("October 2026");
        changed.Should().Contain(nameof(IShellState.CalendarView));
    }

    [Fact]
    public void Filters_and_todo_selection_are_observable()
    {
        var state = new ShellState();
        state.ShowSearch(new ShellFilters("planning", Tags: "work"));
        state.Mode.Should().Be(ShellMode.Search);
        state.ActiveFilters.Query.Should().Be("planning");

        state.ShowTodo("inbox");
        state.Mode.Should().Be(ShellMode.Todo);
        state.TodoFilter.Should().Be("inbox");
    }
}
