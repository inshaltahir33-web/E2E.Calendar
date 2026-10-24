using System.ComponentModel;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class ShellState : IShellState
{
    private ShellMode _mode = ShellMode.Calendar;
    private CalendarViewMode _calendarView = CalendarViewMode.Week;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private string? _todoFilter;
    private bool _isPaneOpen = true;
    private ShellFilters _activeFilters = new();
    private string _title = "Calendar";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ShellMode Mode
    {
        get => _mode;
        private set => Set(ref _mode, value);
    }

    public CalendarViewMode CalendarView
    {
        get => _calendarView;
        private set => Set(ref _calendarView, value);
    }

    public DateOnly SelectedDate
    {
        get => _selectedDate;
        private set => Set(ref _selectedDate, value);
    }

    public string? TodoFilter
    {
        get => _todoFilter;
        private set => Set(ref _todoFilter, value);
    }

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        private set => Set(ref _isPaneOpen, value);
    }

    public ShellFilters ActiveFilters
    {
        get => _activeFilters;
        private set => Set(ref _activeFilters, value);
    }

    public string Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    public void ShowCalendar(CalendarViewMode view, DateOnly selectedDate)
    {
        Mode = ShellMode.Calendar;
        CalendarView = view;
        SelectedDate = selectedDate;
        Title = "Calendar";
    }

    public void ShowTodo(string? filter)
    {
        Mode = ShellMode.Todo;
        TodoFilter = filter;
        Title = "Tasks";
    }

    public void ShowSearch(ShellFilters filters)
    {
        Mode = ShellMode.Search;
        ActiveFilters = filters;
        Title = "Search";
    }

    public void ShowSettings()
    {
        Mode = ShellMode.Settings;
        Title = "Settings";
    }

    public void SetPaneOpen(bool isOpen) => IsPaneOpen = isOpen;

    public void SetFilters(ShellFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ActiveFilters = filters;
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
