using E2E.Calendar.App.Services;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Data;
using E2E.Calendar.Data.Repos;
using E2E.Calendar.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Events;

namespace E2E.Calendar.App;

public partial class App : Application
{
    private IHost? _host;
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _host = BuildHost();
        await _host.StartAsync();

        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    internal static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .UseSerilog((context, services, configuration) => configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    path: ResolveLogFilePath(),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IClock, SystemClock>();
                services.AddSingleton<ISecretStore, CredentialStore>();

                services.AddSingleton(serviceProvider => new Db(
                    ResolveDatabasePath(),
                    serviceProvider.GetRequiredService<ISecretStore>()));

                services.AddSingleton<IEventRepository, EventRepository>();
                services.AddSingleton<ICalendarRepository, CalendarRepository>();
                services.AddSingleton<IReminderRepository, ReminderRepository>();
                services.AddSingleton<ITaskRepository, TaskRepository>();
                services.AddSingleton<IAlertRepository, AlertRepository>();
                services.AddSingleton<IOutbox, Outbox>();

                services.AddSingleton<IRecurrenceExpander, RecurrenceExpander>();
                services.AddSingleton<EventQueryService>();
                services.AddSingleton<IEventQueryCache>(serviceProvider => serviceProvider.GetRequiredService<EventQueryService>());
                services.AddSingleton(serviceProvider => new EventService(
                    serviceProvider.GetRequiredService<IEventRepository>(),
                    serviceProvider.GetRequiredService<IOutbox>(),
                    serviceProvider.GetRequiredService<IClock>(),
                    null,
                    serviceProvider.GetRequiredService<IEventQueryCache>()));

                services.AddSingleton<CalendarService>();
                services.AddSingleton<CalendarStateViewModel>();
                services.AddSingleton<NavigationService>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();

                services.AddHostedService<AppStartupService>();
            })
            .Build();

    internal static string ResolveDataDirectory()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "E2E.Calendar");

        Directory.CreateDirectory(directory);

        return directory;
    }

    internal static string ResolveDatabasePath() =>
        Path.Combine(ResolveDataDirectory(), "calendar.db");

    private static string ResolveLogFilePath()
    {
        var directory = Path.Combine(ResolveDataDirectory(), "logs");

        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "e2e-calendar-.log");
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled exception");
        e.Handled = true;
    }
}
