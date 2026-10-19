using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Services;

public sealed class NavigationService
{
    private readonly Dictionary<string, Type> _views = new(StringComparer.OrdinalIgnoreCase);

    public Frame? Frame { get; set; }

    public string? CurrentViewKey { get; private set; }

    public event EventHandler<string>? Navigated;

    public IReadOnlyCollection<string> RegisteredViews => _views.Keys;

    public void Register(string key, Type viewType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(viewType);

        _views[key] = viewType;
    }

    public bool NavigateTo(string key, object? parameter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        CurrentViewKey = key;

        if (_views.TryGetValue(key, out var viewType) && Frame is not null)
        {
            Frame.Navigate(viewType, parameter);
        }

        Navigated?.Invoke(this, key);

        return _views.ContainsKey(key);
    }
}
