using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public abstract class CalendarPageViewModel : ObservableObject
{
    protected CalendarPageViewModel(CalendarStateViewModel state, EventQueryService query)
    {
        State = state;
        Query = query;

        State.PropertyChanged += OnStatePropertyChanged;
    }

    public CalendarStateViewModel State { get; }

    protected EventQueryService Query { get; }

    public abstract Task RefreshAsync();

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
