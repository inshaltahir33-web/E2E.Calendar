using System;
using System.IO;
using System.Text;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
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
using WinRT.Interop;
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
    private TrayService? _tray;
    private PowerResumeService? _powerResume;
    private Microsoft.Windows.AppLifecycle.AppInstance? _appInstance;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;

        RespectReducedMotion();
    }

    public static new App Current => (App)Application.Current;

    public MainWindow? MainWindow => _mainWindow;

    internal bool IsExiting { get; private set; }

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
            // Single instance: if another instance is already running, hand off activation
            // to it and exit this process without starting the host.
            _appInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("E2E.Calendar");
            if (!_appInstance.IsCurrent)
            {
                await _appInstance.RedirectActivationToAsync(
                    Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs());
                Environment.Exit(0);
                return;
            }

            _host = BuildHost();
            _crashReporter = _host.Services.GetRequiredService<CrashReportingService>();

            // Register toast activation before anything can show a toast, so Snooze/Done
            // work both at startup (app launched by a toast) and while running.
            var toastActivation = _host.Services.GetRequiredService<ToastActivationService>();
            toastActivation.OpenRequested += OnToastOpenRequested;
            toastActivation.OverdueRequested += OnToastOverdueRequested;
            toastActivation.Register();

            await _host.StartAsync();

            var settings = _host.Services.GetRequiredService<SettingsService>();
            var appearance = _host.Services.GetRequiredService<AppearanceService>();
            var sync = _host.Services.GetRequiredService<SyncCoordinator>();

            await settings.LoadAsync();
            await sync.LoadAccountsAsync();

            _mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _window = _mainWindow;

            // When another process redirects its activation to us, surface the window.
            if (_appInstance is not null)
            {
                _appInstance.Activated += OnAppInstanceActivated;
            }

            if (_mainWindow.Content is Microsoft.UI.Xaml.FrameworkElement root)
            {
                appearance.Attach(root);
            }

            // The tray owns the app's lifetime: closing the window only hides it, so the
            // reminder scheduler (a hosted service) keeps running. A login launch goes
            // straight to the tray; if the icon cannot be created we still show the
            // window so the app is never unreachable.
            _tray = _host.Services.GetRequiredService<TrayService>();
            var trayStarted = _tray.Start();
            _ = _host.Services.GetRequiredService<TaskbarJumpListService>().InitializeAsync();
            var startInTray = trayStarted && IsStartupLaunch();

            // The power window must live on the UI thread so its window messages are pumped;
            // Resumed then fires there and the scheduler re-checks its plan immediately.
            _powerResume = _host.Services.GetRequiredService<PowerResumeService>();
            _powerResume.Resumed += OnPowerResumed;

            if (!_powerResume.Start())
            {
                Log.Warning("Wake-from-sleep notifications could not be registered.");
            }

            if (startInTray)
            {
                _mainWindow.AppWindow.Hide();
            }
            else
            {
                _mainWindow.Activate();
            }

            _hotkey = _host.Services.GetRequiredService<Services.HotkeyService>();
            _hotkey.Pressed += OnQuickAddHotkey;

            var windowHandle = WindowNative.GetWindowHandle(_mainWindow);

            // Store purchase UI is parented to the main window; the call is fire-and-forget
            // so licensing (and its network read) never delays showing the window.
            _ = _host.Services.GetRequiredService<StoreEntitlementService>().InitializeAsync(windowHandle);

            if (!_hotkey.TryRegister(windowHandle))
            {
                Log.Warning("Global hotkey Win+Alt+C could not be registered.");
            }

            _mainWindow.InitializeQuickAddHotkey(_hotkey);

            if (Environment.GetEnvironmentVariable("E2E_QUICKADD_SELFTEST") == "1")
            {
                _mainWindow.DispatcherQueue.TryEnqueue(() => ShowQuickAdd());
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

    private void OnToastOpenRequested(object? sender, EventArgs e)
    {
        // Toast activation arrives on a background (COM) thread; the window must be
        // touched from the UI thread.
        _mainWindow?.DispatcherQueue.TryEnqueue(ShowMainWindow);
    }

    private void OnToastOverdueRequested(object? sender, EventArgs e)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            _mainWindow?.ShowOverdue();
        });
    }

    private void OnPowerResumed(object? sender, EventArgs e)
    {
        // Raised on the UI thread that owns the power window; the catch-up itself is async.
        _ = _host?.Services.GetRequiredService<ReminderNotificationService>().ResumeCatchUpAsync();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.AppWindow.Show();
        _mainWindow.Activate();
    }

    private void OnAppInstanceActivated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments e)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(ShowMainWindow);
    }

    public void ShowNewEvent()
    {
        ShowMainWindow();

        if (_mainWindow is { } mainWindow)
        {
            mainWindow.DispatcherQueue.TryEnqueue(mainWindow.BeginNewEvent);
        }
    }

    /// <summary>
    /// "Remind me in N minutes" from the New menu: creates a one-shot standalone
    /// reminder at now+N and wakes the scheduler so it is planned immediately.
    /// </summary>
    public async Task StartCountdownAsync(int minutes)
    {
        if (_host is null)
        {
            return;
        }

        var clock = _host.Services.GetRequiredService<IClock>();
        var engine = _host.Services.GetRequiredService<ReminderEngine>();

        await engine.CreateCountdownAsync(minutes, clock.UtcNow, clock.LocalTimeZone.ToIanaId());
        _host.Services.GetRequiredService<ReminderNotificationService>().Wake();
    }

    public async Task QuitAsync()
    {
        if (IsExiting)
        {
            return;
        }

        IsExiting = true;

        _tray?.Dispose();
        _tray = null;

        _powerResume?.Dispose();
        _powerResume = null;

        _quickAddWindow?.Close();
        _quickAddWindow = null;

        if (_host is not null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "The host did not stop cleanly.");
            }

            _host.Dispose();
            _host = null;
        }

        _mainWindow?.Close();
        _mainWindow = null;
        _window = null;

        Exit();
    }

    private static bool IsStartupLaunch()
    {
        try
        {
            // Packaged apps get a dedicated activation kind from the startup task.
            if (Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent()
                    .GetActivatedEventArgs().Kind ==
                Microsoft.Windows.AppLifecycle.ExtendedActivationKind.StartupTask)
            {
                return true;
            }
        }
        catch (Exception)
        {
            // No package identity (unpackaged dev build) or lifecycle unavailable.
        }

        // Win32 startup tasks also pass -startup on the command line.
        return Environment.GetCommandLineArgs()
            .Any(argument => string.Equals(argument, "-startup", StringComparison.OrdinalIgnoreCase));
    }

    public void ShowQuickAdd(bool reminderMode = false)
    {
        ArgumentNullException.ThrowIfNull(_host);

        if (_quickAddWindow is not null)
        {
            _quickAddWindow.PrepareNewItem(reminderMode);
            _quickAddWindow.Activate();

            return;
        }

        var view = _host.Services.GetRequiredService<Views.QuickAddView>();
        view.PrepareNewItem(reminderMode);

        // The quick add window is hosted outside the main window, so it does not inherit
        // the per-window RequestedTheme that AppearanceService applies to the root element.
        if (view.Content is FrameworkElement quickAddRoot &&
            _mainWindow?.Content is FrameworkElement root)
        {
            quickAddRoot.RequestedTheme = root.ActualTheme;
        }

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
                services.AddSingleton<HolidayCalendarService>();
                services.AddHostedService<HolidayOverrideRefreshService>();
                services.AddSingleton<CrashReportingService>();
                services.AddSingleton<StartupTaskService>();
                services.AddSingleton<TaskbarJumpListService>();
                services.AddSingleton<TrayIconService>();
                services.AddSingleton<TrayService>();
                services.AddSingleton<PowerResumeService>();
                services.AddSingleton<FocusAssistService>();
                services.AddTransient<Views.TrayFlyoutWindow>();
                services.AddHostedService<UpdateCheckService>();

                // Registered as a singleton first so ToastActivationService can wake the
                // reminder loop after a toast Snooze/Done.
                services.AddSingleton<ReminderNotificationService>();
                services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<ReminderNotificationService>());
                services.AddSingleton<DailyDigestNotificationService>();
                services.AddSingleton<ToastActivationService>();

                services.AddSingleton(serviceProvider => new Db(
                    ResolveDatabasePath(),
                    serviceProvider.GetRequiredService<ISecretStore>(),
                    serviceProvider.GetRequiredService<ILogger<Db>>()));

                services.AddSingleton<IEventRepository, EventRepository>();
                services.AddSingleton<IAccountRepository, AccountRepository>();
                services.AddSingleton<ICalendarRepository, CalendarRepository>();
                services.AddSingleton<ICalendarSubscriptionRepository, CalendarSubscriptionRepository>();
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
                services.AddSingleton<CalendarItemQueryService>();
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
                services.AddSingleton<EventDeletionService>();
                services.AddSingleton<IEventTemplateStore, EventTemplateRepository>();
                services.AddSingleton<ReminderQuickAdd>();
                services.AddSingleton<TodoService>();
                services.AddSingleton<ReminderPlanner>();
                services.AddSingleton<ReminderEngine>();
                services.AddSingleton<DailyDigestService>();
                // Store licensing reads the WinRT license; FreeEntitlementService stays the
                // fallback for sideloaded/unpackaged builds and offline launches.
                services.AddSingleton<FreeEntitlementService>();
                services.AddSingleton<StoreEntitlementService>();
                services.AddSingleton<IEntitlementService>(serviceProvider =>
                    serviceProvider.GetRequiredService<StoreEntitlementService>());
                services.AddSingleton<IPaywallService, PaywallService>();
                services.AddTransient<PaywallViewModel>();
                services.AddTransient<Views.PaywallDialog>();
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
                services.AddSingleton<IcsSubscriptionService>();
                services.AddSingleton<CalendarStateViewModel>();
                services.AddSingleton<NavigationService>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MonthViewModel>();
                services.AddSingleton<WeekViewModel>();
                services.AddSingleton<DayViewModel>();
                services.AddSingleton<AgendaViewModel>();
                services.AddSingleton<MiniCalendarViewModel>();
                services.AddSingleton<EventEditorViewModel>();
                services.AddSingleton<Services.EventDetailsPresenter>();
                services.AddSingleton<Services.EventDeletionCoordinator>();
                services.AddSingleton<MainWindow>();

                services.AddHostedService<AppStartupService>();
                services.AddHostedService<NavigationRegistrationService>();
                services.AddHostedService<ReminderNotificationService>();
                // Starts after the migration in AppStartupService; keeps polling the
                // digest time while the app sits in the tray.
                services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<DailyDigestNotificationService>());
                // Starts after AppStartupService so the database is migrated before the
                // first subscription refresh pass runs.
                services.AddHostedService<IcsSubscriptionRefreshService>();

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
