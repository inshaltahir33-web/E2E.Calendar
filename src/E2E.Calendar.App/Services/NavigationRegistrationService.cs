using E2E.Calendar.App.ViewModels;
using E2E.Calendar.App.Views;
using E2E.Calendar.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace E2E.Calendar.App.Services;

public sealed class NavigationRegistrationService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly NavigationService _navigation;

    public NavigationRegistrationService(IServiceProvider services, NavigationService navigation)
    {
        _services = services;
        _navigation = navigation;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _navigation.RegisterFactory(
            NavigationKeys.Month,
            _ => new MonthView(
                _services.GetRequiredService<MonthViewModel>(),
                _services.GetRequiredService<CalendarStateViewModel>(),
                _navigation));

        _navigation.RegisterFactory(
            NavigationKeys.Week,
            _ => new WeekView(_services.GetRequiredService<WeekViewModel>()));

        _navigation.RegisterFactory(
            NavigationKeys.Day,
            _ => new DayView(_services.GetRequiredService<DayViewModel>()));

        _navigation.RegisterFactory(
            NavigationKeys.Agenda,
            _ => new AgendaView(_services.GetRequiredService<AgendaViewModel>()));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
