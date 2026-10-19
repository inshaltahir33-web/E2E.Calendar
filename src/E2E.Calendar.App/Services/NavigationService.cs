using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Services;

public sealed class NavigationService
{
    private readonly Dictionary<string, Type> _views = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<object?, object?>> _factories = new(StringComparer.OrdinalIgnoreCase);

    public Frame? Frame { get; set; }

    public string? CurrentViewKey { get; private set; }

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

        CurrentViewKey = key;

        if (_factories.TryGetValue(key, out var factory))
        {
            if (factory(parameter) is FrameworkElement page)
            {
                if (Frame is not null)
                {
                    Frame.Content = page;
                }
            }
        }
        else if (_views.TryGetValue(key, out var viewType) && Frame is not null)
        {
            Frame.Navigate(viewType, parameter);
        }

        Navigated?.Invoke(this, key);

        return _views.ContainsKey(key) || _factories.ContainsKey(key);
    }
}
