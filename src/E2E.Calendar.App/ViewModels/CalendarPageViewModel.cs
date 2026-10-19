using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public abstract class CalendarPageViewModel : ObservableObject
{
    private readonly IEventQueryCache _cache;

    protected CalendarPageViewModel(
        CalendarStateViewModel state,
        EventQueryService query,
        IEventQueryCache cache)
    {
        State = state;
        Query = query;
        _cache = cache;

        State.PropertyChanged += OnStatePropertyChanged;
        cache.Invalidated += OnCacheInvalidated;
    }

    public CalendarStateViewModel State { get; }

    protected EventQueryService Query { get; }

    public abstract Task RefreshAsync();

    private async void OnCacheInvalidated(object? sender, EventArgs e)
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh {GetType().Name}: {ex.Message}");
        }
    }

    private async void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(CalendarStateViewModel.CurrentDate) or nameof(CalendarStateViewModel.ViewMode)))
        {
            return;
        }

        try
        {
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh {GetType().Name}: {ex.Message}");
        }
    }
}
