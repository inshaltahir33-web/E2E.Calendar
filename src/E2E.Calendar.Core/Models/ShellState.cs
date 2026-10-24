using System.ComponentModel;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public enum ShellMode
{
    Calendar,
    Todo,
    Search,
    Settings,
}

public sealed record ShellFilters(
    string? Query = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Tags = null,
    string? Category = null);

public interface IShellState : INotifyPropertyChanged
{
    ShellMode Mode { get; }
    CalendarViewMode CalendarView { get; }
    DateOnly SelectedDate { get; }
    string? TodoFilter { get; }
    bool IsPaneOpen { get; }
    ShellFilters ActiveFilters { get; }
    string Title { get; }

    void ShowCalendar(CalendarViewMode view, DateOnly selectedDate);
    void ShowTodo(string? filter);
    void ShowSearch(ShellFilters filters);
    void ShowSettings();
    void SetPaneOpen(bool isOpen);
    void SetSelectedDate(DateOnly date);
    void SetFilters(ShellFilters filters);
}
