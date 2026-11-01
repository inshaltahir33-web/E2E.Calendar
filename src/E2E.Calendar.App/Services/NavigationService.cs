using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.App.Services;

public sealed class NavigationService
{
    private readonly Dictionary<string, Type> _views = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<object?, object?>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _pages = new(StringComparer.OrdinalIgnoreCase);
    private readonly IShellState _shellState;

    public NavigationService(IShellState shellState)
    {
        _shellState = shellState;
    }
    public Frame? Frame { get; set; }

    public string? CurrentViewKey { get; private set; }

    /// <summary>
    /// True while the frame shows Day, Week, Month or Agenda. The top bar paints its selected
    /// tab from this, so a tab can never stay lit once Reminders / Settings is open.
    /// </summary>
    public bool IsCalendarView => IsCalendarViewKey(CurrentViewKey ?? string.Empty);

    /// <summary>
    /// The last Day/Week/Month/Agenda view that was opened, so pages like Settings can toggle
    /// back to the calendar instead of jumping to a fixed default view.
    /// </summary>
    public string LastCalendarViewKey { get; private set; } = NavigationKeys.Week;

    public event EventHandler<string>? Navigated;

    public IReadOnlyCollection<string> RegisteredViews => _views.Keys.Concat(_factories.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public void Register(string key, Type viewType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(viewType);

        _views[key] = viewType;
    }

    public void RegisterFactory(string key, Func<object?, object?> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        _factories[key] = factory;
    }

    public bool NavigateTo(string key, object? parameter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (IsCalendarViewKey(key))
        {
            LastCalendarViewKey = key;
        }

        CurrentViewKey = key;
        UpdateShellState(key, parameter);

        if (_factories.TryGetValue(key, out var factory))
        {
            if (!_pages.TryGetValue(key, out var page) || page is null)
            {
                page = factory(parameter);
                _pages[key] = page;
            }

            if (page is FrameworkElement element && Frame is not null)
            {
                Frame.Content = element;
            }
        }
        else if (_views.TryGetValue(key, out var viewType) && Frame is not null)
        {
            Frame.Navigate(viewType, parameter);
        }

        Navigated?.Invoke(this, key);

        return _views.ContainsKey(key) || _factories.ContainsKey(key);
    }

    public static bool IsCalendarViewKey(string key) =>
        key is NavigationKeys.Day or NavigationKeys.Week or NavigationKeys.Month or NavigationKeys.Agenda;

    private void UpdateShellState(string key, object? parameter)
    {
        if (key.Equals(NavigationKeys.Settings, StringComparison.OrdinalIgnoreCase))
        {
            _shellState.ShowSettings();
        }
        else if (key.Equals(NavigationKeys.Tasks, StringComparison.OrdinalIgnoreCase))
        {
            _shellState.ShowTodo(parameter as string);
        }
        else if (key.Equals(NavigationKeys.Reminders, StringComparison.OrdinalIgnoreCase))
        {
            _shellState.ShowTodo(parameter as string);
        }
        else
        {
            var view = key switch
            {
                NavigationKeys.Day => CalendarViewMode.Day,
                NavigationKeys.Month => CalendarViewMode.Month,
                NavigationKeys.Agenda => CalendarViewMode.Agenda,
                _ => CalendarViewMode.Week,
            };
            _shellState.ShowCalendar(view, _shellState.SelectedDate);
        }
    }
}
