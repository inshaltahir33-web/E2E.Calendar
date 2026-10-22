using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public abstract partial class CalendarPageViewModel : ObservableObject
{
    /// <summary>
    /// A skeleton is only worth showing when the query is slow enough to be
    /// noticed, so the loading state waits this long before it appears.
    /// </summary>
    protected const int LoadingDelayMs = 150;

    private readonly IEventQueryCache _cache;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    [ObservableProperty]
    private bool _isLoading;

    private DateOnly _lastPeriodKey = DateOnly.MinValue;

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

    /// <summary>
    /// Raised after the content for a new period has been rendered, so the
    /// view can play the period-change transition.
    /// </summary>
    public event EventHandler? PeriodChanged;

    /// <summary>
    /// Runs a range query and raises <see cref="IsLoading"/> only when the
    /// query takes longer than <see cref="LoadingDelayMs"/>. Refreshes are
    /// serialised so rapid period changes cannot interleave and leave the view
    /// showing a result for a period the user already navigated away from.
    /// </summary>
    protected async Task LoadAsync(Func<Task> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await _refreshGate.WaitAsync();

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var pending = query();

            var remaining = LoadingDelayMs - (int)stopwatch.ElapsedMilliseconds;

            if (remaining > 0)
            {
                var completed = await Task.WhenAny(pending, Task.Delay(remaining));

                if (completed == pending)
                {
                    await pending;
                    return;
                }
            }

            IsLoading = true;

            try
            {
                await pending;
            }
            finally
            {
                IsLoading = false;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Runs a range query for the visible period and notifies the view when
    /// the period changed so it can animate the transition.
    /// </summary>
    protected async Task LoadPeriodAsync(Func<Task> query)
    {
        var key = State.LocalDate;
        var changed = _lastPeriodKey != DateOnly.MinValue && _lastPeriodKey != key;

        _lastPeriodKey = key;

        await LoadAsync(query);

        if (changed)
        {
            PeriodChanged?.Invoke(this, EventArgs.Empty);
        }
    }

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
