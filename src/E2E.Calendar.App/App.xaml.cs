using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Events;
using E2E.Calendar.App.ViewModels;

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
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

    private static string ResolveLogFilePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "E2E.Calendar",
            "logs");

        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "e2e-calendar-.log");
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled exception");
        e.Handled = true;
    }
}
