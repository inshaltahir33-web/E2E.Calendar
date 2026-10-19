using System.Collections.ObjectModel;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;

namespace E2E.Calendar.App.ViewModels;

public sealed class AgendaViewModel : CalendarPageViewModel
{
    private readonly AgendaService _agenda;

    public AgendaViewModel(CalendarStateViewModel state, EventQueryService query, IEventQueryCache cache, AgendaService agenda)
        : base(state, query, cache)
    {
        _agenda = agenda;
    }

    public ObservableCollection<AgendaDayViewModel> Days { get; } = new();

    public override async Task RefreshAsync()
    {
        var period = State.Period;
        var days = await _agenda.GetDaysAsync(period.FromUtc, period.ToUtc);

        Days.Clear();

        foreach (var day in days)
        {
            Days.Add(new AgendaDayViewModel
            {
                Header = day.Header,
                Items = day.Items
                    .Select(item => new AgendaItemViewModel
                    {
                        Title = item.Title,
                        Color = item.Color,
                        IsDone = item.IsDone,
                        IsTickable = item.IsTickable,
                        TimeLabel = item.TimeLabel,
                    })
                    .ToList(),
            });
        }
    }
}
