using System;
using System.IO;
using System.Text;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Data;
using E2E.Calendar.Data.Repos;
using E2E.Calendar.Platform;
using E2E.Calendar.Sync.Google;
using E2E.Calendar.Sync.Microsoft;
using E2E.Calendar.Sync.Ics;
using E2E.Calendar.Sync.CalDav;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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
    private MainWindow? _mainWindow;
    private Views.QuickAddView? _quickAddWindow;
    private Services.HotkeyService? _hotkey;
    private CrashReportingService? _crashReporter;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;

        RespectReducedMotion();
    }

    public static new App Current => (App)Application.Current;

    public MainWindow? MainWindow => _mainWindow;

    private void RespectReducedMotion()
    {
        try
        {
            var settings = new Windows.UI.ViewManagement.UISettings();

            if (!settings.AnimationsEnabled)
            {
                this.Resources["TransitionDuration"] = "0:0:0";
            }
        }
        catch (Exception)
        {
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _host = BuildHost();
            _crashReporter = _host.Services.GetRequiredService<CrashReportingService>();
            await _host.StartAsync();

            var settings = _host.Services.GetRequiredService<SettingsService>();
            var appearance = _host.Services.GetRequiredService<AppearanceService>();
            var sync = _host.Services.GetRequiredService<SyncCoordinator>();

            await settings.LoadAsync();
            await sync.LoadAccountsAsync();

            _mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _window = _mainWindow;

            if (_mainWindow.Content is Microsoft.UI.Xaml.FrameworkElement root)
            {
                appearance.Attach(root);
            }

            _mainWindow.Activate();

            _hotkey = _host.Services.GetRequiredService<Services.HotkeyService>();
            _hotkey.Pressed += OnQuickAddHotkey;

            _mainWindow.InitializeQuickAddHotkey(_hotkey);

            if (Environment.GetEnvironmentVariable("E2E_QUICKADD_SELFTEST") == "1")
            {
                _mainWindow.DispatcherQueue.TryEnqueue(ShowQuickAdd);
            }
        }
        catch (Exception ex)
        {
            // Write exception to crash.txt in the app's base directory for easy inspection
            // when running via 'dotnet run' (console output is not visible in WinUI 3)
            try
            {
                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "crash.txt"),
                    ex.ToString());
            }
            catch
            {
                // If we can't write the file, continue with existing error handling
            }

            _crashReporter?.Report(ex, "AppStartup");
            Log.Fatal(ex, "Application startup failed");

            // Show graceful fallback instead of white screen crash
            // In production, this would show a ContentDialog with options to view report or start anyway
            // Since UI may not be fully initialized, just log the error handling
            try
            {
                // Attempt UI feedback if window is available
                if (_mainWindow is not null)
                {
                    
                }
            }
            catch
            {
                // UI not ready - continue with close
            }

            _mainWindow?.Close();
            _mainWindow = null;
            _window = null;
        }
    }

    private void OnQuickAddHotkey(object? sender, EventArgs e) => ShowQuickAdd();

    public void ShowQuickAdd()
    {
        ArgumentNullException.ThrowIfNull(_host);

        if (_quickAddWindow is not null)
        {
            _quickAddWindow.Activate();

            return;
        }

        var view = _host.Services.GetRequiredService<Views.QuickAddView>();

        _quickAddWindow = view;

        view.Activate();
    }

    internal Task OnQuickAddClosedAsync()
    {
        _quickAddWindow = null;

        return Task.CompletedTask;
    }

    internal static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddUserSecrets<App>(optional: true))
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
                services.AddSingleton(new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30),
                });
                services.AddSingleton<IClock, SystemClock>();
                services.AddSingleton<ISecretStore, CredentialStore>();
                services.AddSingleton<IDialogService>(serviceProvider => new DialogService(
                    () => App.Current.MainWindow?.Content?.XamlRoot,
                    serviceProvider.GetRequiredService<ILogger<DialogService>>()));
                // WidgetAgendaCardService (v1.1 Windows 11 widgets) is not surfaced in the UI yet,
                // so it is intentionally not registered. Re-register when the widget host is wired up.
                services.AddSingleton<WeatherService>();
                services.AddSingleton<CrashReportingService>();
                services.AddHostedService<UpdateCheckService>();

                services.AddSingleton(serviceProvider => new Db(
                    ResolveDatabasePath(),
                    serviceProvider.GetRequiredService<ISecretStore>()));

                services.AddSingleton<IEventRepository, EventRepository>();
                services.AddSingleton<IAccountRepository, AccountRepository>();
                services.AddSingleton<ICalendarRepository, CalendarRepository>();
                services.AddSingleton<IReminderRepository, ReminderRepository>();
                services.AddSingleton<ITaskRepository, TaskRepository>();
                services.AddSingleton<IAlertRepository, AlertRepository>();
                services.AddSingleton<ISettingsRepository, SettingsRepository>();
                services.AddSingleton<IOutbox, Outbox>();
                services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
                services.AddSingleton<SchedulingAssistantService>();
                services.AddSingleton<EventAttachmentService>();
                services.AddSingleton<ConflictResolutionService>();
                services.AddSingleton<ScheduleExportService>();

                services.AddSingleton<IRecurrenceExpander, RecurrenceExpander>();
                services.AddSingleton<EventQueryService>();
                // ProductivityAnalyticsService has no consumer yet (v1.1 metrics), so it is not registered.
                services.AddSingleton<IEventQueryCache>(serviceProvider => serviceProvider.GetRequiredService<EventQueryService>());
                services.AddSingleton<EventService>(serviceProvider => new EventService(
                    serviceProvider.GetRequiredService<IEventRepository>(),
                    serviceProvider.GetRequiredService<IOutbox>(),
                    serviceProvider.GetRequiredService<IClock>(),
                    null,
                    serviceProvider.GetRequiredService<IEventQueryCache>(),
                    serviceProvider.GetRequiredService<IAlertRepository>()));

                services.AddSingleton<IEventService>(serviceProvider => serviceProvider.GetRequiredService<EventService>());
                services.AddSingleton<Services.HotkeyService>(serviceProvider => new Services.HotkeyService(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
                services.AddSingleton<ViewModels.QuickAddViewModel>();
                services.AddTransient<Views.QuickAddView>();

                services.AddSingleton<SettingsService>();
                services.AddSingleton<ISettingsService>(serviceProvider => serviceProvider.GetRequiredService<SettingsService>());
                services.AddSingleton<IShellState, ShellState>();
                services.AddSingleton<ViewModels.SettingsViewModel>();
                services.AddSingleton<AppearanceService>();
                services.AddSingleton<CalendarService>();
                services.AddSingleton<AgendaService>();
                services.AddSingleton<RecurrenceEditService>();
                services.AddSingleton<EventTemplateService>();
                services.AddSingleton<ReminderQuickAdd>();
                services.AddSingleton<TodoService>();
                services.AddSingleton<ReminderPlanner>();
                services.AddSingleton<IEntitlementService, FreeEntitlementService>();
                // Google two-way sync stays opt-in until our Google OAuth app is approved.
                // Enable with: dotnet build -p:GoogleSyncEnabled=true
#if GoogleSyncEnabled
                services.AddSingleton<GoogleCalendarProvider>();
#endif
                services.AddSingleton<GraphCalendarProvider>();
                services.AddSingleton<CalDavProvider>();
#if GoogleSyncEnabled
                services.AddSingleton<E2E.Calendar.Core.Sync.ISyncProvider>(serviceProvider =>
                    serviceProvider.GetRequiredService<GoogleCalendarProvider>());
#endif
                services.AddSingleton<E2E.Calendar.Core.Sync.ISyncProvider>(serviceProvider =>
                    serviceProvider.GetRequiredService<GraphCalendarProvider>());
                services.AddSingleton<E2E.Calendar.Core.Sync.ISyncProvider>(serviceProvider =>
                    serviceProvider.GetRequiredService<CalDavProvider>());
                services.AddSingleton<SyncCoordinator>();
                services.AddSingleton<IcsService>();
                services.AddSingleton<CalendarStateViewModel>();
                services.AddSingleton<NavigationService>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MonthViewModel>();
                services.AddSingleton<WeekViewModel>();
                services.AddSingleton<DayViewModel>();
                services.AddSingleton<AgendaViewModel>();
                services.AddSingleton<MiniCalendarViewModel>();
                services.AddSingleton<EventEditorViewModel>();
                services.AddSingleton<MainWindow>();

                services.AddHostedService<AppStartupService>();
                services.AddHostedService<NavigationRegistrationService>();
                services.AddHostedService<ReminderNotificationService>();

#if DEBUG
                if (string.Equals(
                        Environment.GetEnvironmentVariable("E2E_DEMO"),
                        "1",
                        StringComparison.Ordinal))
                {
                    services.AddSingleton<IDemoDataSeeder, DemoDataSeeder>();
                }
#endif
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
        _crashReporter?.Report(e.Exception, "ui");
        Log.Fatal(e.Exception, "Unhandled exception");
        e.Handled = true;
    }
}
