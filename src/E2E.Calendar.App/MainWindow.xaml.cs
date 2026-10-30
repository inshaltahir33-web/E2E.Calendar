using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;
using E2E.Calendar.Sync.Ics;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Graphics;
using WinRT.Interop;
using Windows.UI;

namespace E2E.Calendar.App;

public sealed partial class MainWindow : Window
{
    private const double CompactWidthThreshold = 900;
    private const int SidebarCollapseDurationMs = 167;

    private readonly NavigationService _navigation;
    private readonly CalendarService _calendars;
    private readonly IReminderRepository _reminders;
    private readonly EventService _events;
    private readonly IEventRepository _eventRepository;
    private readonly EventQueryService _query;
    private readonly IClock _clock;
    private readonly ILogger<MainWindow> _logger;
    private readonly EventEditorViewModel _editor;
    private readonly SettingsService _settings;
    private readonly IShellState _shellState;
    private readonly SyncCoordinator _sync;
    private readonly IcsService _ics;
    private readonly ScheduleExportService _scheduleExport;
    private readonly TodoService _todo;
    private readonly IDialogService _dialogs;
    private Views.EventEditorView? _editorView;
    private bool _syncInProgress;
    private bool _rsvpInProgress;
    private bool _isClosingEditor;

    private readonly Dictionary<CalendarItem, string> _searchResults = new();
    private readonly DispatcherQueueTimer _searchDebounce;
    private CancellationTokenSource? _searchCts;
    private EventSearchCriteria _searchCriteria = new();

    private HotkeyService? _hotkey;
    private IntPtr _hotkeyWindowHandle;
    private IntPtr _previousWindowProc;
    private HotkeyWindowProc? _hotkeyWindowProc;

    public void InitializeQuickAddHotkey(HotkeyService hotkey)
    {
        _hotkey = hotkey;

        _hotkeyWindowHandle = WindowNative.GetWindowHandle(this);
        _hotkeyWindowProc = HotkeyWindowProcedure;
        var procPointer = Marshal.GetFunctionPointerForDelegate(_hotkeyWindowProc);
        _previousWindowProc = SetWindowLongPtr(_hotkeyWindowHandle, GwlWndProc, procPointer);

        // App owns the hotkey subscription; the window only ensures the native
        // registration is released when the window closes.
        this.AppWindow.Closing += (_, _) =>
        {
            if (_hotkeyWindowHandle != IntPtr.Zero && _previousWindowProc != IntPtr.Zero)
            {
                SetWindowLongPtr(_hotkeyWindowHandle, GwlWndProc, _previousWindowProc);
                _previousWindowProc = IntPtr.Zero;
            }

            _hotkey?.Dispose();
        };
    }

    private IntPtr HotkeyWindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (_hotkey is not null && _hotkey.HandleMessage((int)message, wParam))
        {
            return IntPtr.Zero;
        }

        return CallWindowProc(_previousWindowProc, hwnd, message, wParam, lParam);
    }

    private const int GwlWndProc = -4;

    private delegate IntPtr HotkeyWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public MainWindow(
        MainWindowViewModel viewModel,
        CalendarStateViewModel stateViewModel,
        NavigationService navigation,
        CalendarService calendars,
        EventService events,
        IEventRepository eventRepository,
        EventQueryService query,
        IClock clock,
        ILogger<MainWindow> logger,
        EventEditorViewModel editor,
        WeekViewModel weekViewModel,
        MonthViewModel monthViewModel,
        MiniCalendarViewModel miniCalendar,
        IReminderRepository reminders,
        SettingsService settings,
        IShellState shellState,
        SyncCoordinator sync,
        IcsService ics,
        ScheduleExportService scheduleExport,
        TodoService todo,
        IDialogService dialogs)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(stateViewModel);
            ArgumentNullException.ThrowIfNull(navigation);
            ArgumentNullException.ThrowIfNull(calendars);
            ArgumentNullException.ThrowIfNull(events);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(weekViewModel);
            ArgumentNullException.ThrowIfNull(monthViewModel);
            ArgumentNullException.ThrowIfNull(miniCalendar);
            ArgumentNullException.ThrowIfNull(reminders);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(shellState);
            ArgumentNullException.ThrowIfNull(todo);
            ArgumentNullException.ThrowIfNull(dialogs);

            ViewModel = viewModel;
            StateViewModel = stateViewModel;
            _navigation = navigation;
            _calendars = calendars;
            _events = events;
            _eventRepository = eventRepository;
            _query = query;
            _clock = clock;
            _logger = logger;
            _editor = editor;
            WeekViewModel = weekViewModel;
            MonthViewModel = monthViewModel;
            MiniCalendar = miniCalendar;
            _reminders = reminders;
            _settings = settings;
            _shellState = shellState;
            _sync = sync;
            _ics = ics;
            _scheduleExport = scheduleExport;
            _todo = todo;
            _dialogs = dialogs;

            this.InitializeComponent();

            SyncMenuItem.Visibility = AppFeatures.SyncUiEnabled ? Visibility.Visible : Visibility.Collapsed;
            SyncStatusText.Visibility = AppFeatures.SyncUiEnabled ? Visibility.Visible : Visibility.Collapsed;
            InvitationInfoBar.Visibility = AppFeatures.SyncUiEnabled ? Visibility.Visible : Visibility.Collapsed;

            _searchDebounce = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _searchDebounce.Interval = TimeSpan.FromMilliseconds(250);
            _searchDebounce.IsRepeating = false;
            _searchDebounce.Tick += OnSearchDebounceTick;

            _navigation.Frame = ContentFrame;
            _navigation.Navigated += OnNavigated;

            StateViewModel.PropertyChanged += OnStateViewModelPropertyChanged;

            _editorView = new Views.EventEditorView(editor);
            EditorHost.Children.Add(_editorView);
            MiniCalendarHost.Children.Add(new Controls.MiniCalendarView(miniCalendar, dialogs));

            editor.Opened += OnEditorOpened;
            editor.CloseRequested += OnEditorCloseRequested;
            editor.SaveRequested += OnEditorSaveRequested;
            editor.DeleteRequested += OnEditorDeleteRequested;

            EditorHost.KeyDown += OnEditorKeyDown;

            Snackbar.ActionInvoked += OnSnackbarAction;

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(TitleBarElement);
            RootGrid.ActualThemeChanged += OnActualThemeChanged;
            UpdateTitleBarColors();

            // Sidebar counts follow every add / complete / delete made on the to-do page.
            _todo.Changed += (_, _) => _ = RefreshReminderCountsAsync();

            // Single-key shortcuts (N, R, T, D, W, M, A) must not fire while the user is typing in a text box.
            RootGrid.GotFocus += OnRootGotFocus;

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                this.SystemBackdrop = new MicaBackdrop();
            }

            this.SizeChanged += OnWindowSizeChanged;
            this.AppWindow.Closing += OnWindowClosingSaveBounds;
            RootGrid.Loaded += OnLoaded;
        }
        catch (Exception ex)
        {
            // XAML parse failures (bad StaticResource, unmatched binding paths, custom control
            // errors) and service-resolution failures are captured to the desktop crash log before
            // being rethrown, so the exact error is visible instead of a silent crash.
            var crashLogPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "desktop-crash.txt");
            System.IO.File.WriteAllText(crashLogPath, ex.ToString());

            _logger?.LogError(ex, "MainWindow: failed to initialize the window.");

            throw;
        }
    }

    public IShellState ShellState => _shellState;

    /// <summary>Exposed for x:Bind (New menu enabled state while the editor is open).</summary>
    public EventEditorViewModel EditorViewModel => _editor;

    /// <summary>Fixed drawer width. Never animated via layout — only its composition translation changes.</summary>
    private double EditorPaneWidth => Tokens.Double("EditorPaneWidth", 400d);

    private void OnActualThemeChanged(FrameworkElement sender, object args) =>
        UpdateTitleBarColors();

    private void UpdateTitleBarColors()
    {
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonForegroundColor = ResourceColor("App.TextPrimaryBrush");
        titleBar.ButtonBackgroundColor = ResourceColor("App.WindowBackgroundBrush");
        titleBar.ButtonHoverForegroundColor = ResourceColor("App.TextPrimaryBrush");
        titleBar.ButtonHoverBackgroundColor = ResourceColor("App.HoverBrush");
        titleBar.ButtonPressedForegroundColor = ResourceColor("App.TextPrimaryBrush");
        titleBar.ButtonPressedBackgroundColor = ResourceColor("App.PressedBrush");
        titleBar.ButtonInactiveForegroundColor = ResourceColor("App.TextSecondaryBrush");
        titleBar.ButtonInactiveBackgroundColor = ResourceColor("App.WindowBackgroundBrush");
    }

    private void OnRootGotFocus(object sender, RoutedEventArgs e)
    {
        var typing = e.OriginalSource is TextBox or PasswordBox or RichEditBox;

        foreach (var accelerator in RootGrid.KeyboardAccelerators)
        {
            if (accelerator.Modifiers == Windows.System.VirtualKeyModifiers.None)
            {
                accelerator.IsEnabled = !typing;
            }
        }
    }

    // Resolves against the window's ActualTheme. Application.Current.Resources[...] ignores the
    // per-window theme override and returned Light colours (white caption buttons) in a Dark window.
    private Color ResourceColor(string key) =>
        ThemeBrushes.Get(RootGrid, key) is SolidColorBrush brush ? brush.Color : default;

    private async void OnExportPdfClick(object sender, RoutedEventArgs e) =>
            await ExportScheduleAsync(ScheduleExportViewForState(), "pdf");

        private async void OnExportHtmlClick(object sender, RoutedEventArgs e) =>
            await ExportScheduleAsync(ScheduleExportViewForState(), "html");

        private ScheduleExportView ScheduleExportViewForState() =>
            StateViewModel.ViewMode switch
            {
                CalendarViewMode.Day => ScheduleExportView.Day,
                CalendarViewMode.Month => ScheduleExportView.Month,
                _ => ScheduleExportView.Week,
            };

        private async Task ExportScheduleAsync(ScheduleExportView view, string format)
        {
            try
            {
                var period = StateViewModel.Period;
                var document = await _scheduleExport.BuildAsync(view, period.FromUtc, period.ToUtc);
                var picker = new FileSavePicker
                {
                    SuggestedFileName = $"{view}-schedule",
                };
                picker.FileTypeChoices.Add(
                    format == "pdf" ? new KeyValuePair<string, IList<string>>("PDF document", [".pdf"]) :
                    new KeyValuePair<string, IList<string>>("HTML document", [".html"]));
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                var file = await picker.PickSaveFileAsync();
                if (file is null)
                {
                    return;
                }

                var bytes = format == "pdf"
                    ? _scheduleExport.ToPdf(document)
                    : System.Text.Encoding.UTF8.GetBytes(_scheduleExport.ToHtml(document));
                await FileIO.WriteBytesAsync(file, bytes);
                ShowStatus($"Exported {view.ToString().ToLowerInvariant()} schedule.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schedule export failed");
                ShowStatus("The schedule could not be exported.", InfoBarSeverity.Error);
            }
        }

    public MainWindowViewModel ViewModel { get; }

    public CalendarStateViewModel StateViewModel { get; }

    public WeekViewModel WeekViewModel { get; }

    public MonthViewModel MonthViewModel { get; }

    public MiniCalendarViewModel MiniCalendar { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Motion.Refresh();

            await RefreshCalendarsAsync();
            _navigation.NavigateTo(NavigationKeys.Week);

            if (!_settings.Current.OnboardingComplete)
            {
                await Views.OnboardingView.ShowAsync(_dialogs, RootGrid.XamlRoot, _settings);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial window load failed");
            ShowStatus("Calendar data could not be loaded. Try Sync now or restart the app.", InfoBarSeverity.Error);
        }
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        // MASTER §4: the window must resize cleanly down to a narrow width and the sidebar
        // must collapse to an icon rail. Below the threshold the full sidebar is replaced by
        // the rail; above it the full sidebar comes back.
        var collapsed = args.Size.Width < CompactWidthThreshold;

        if (collapsed != ViewModel.IsSidebarCollapsed)
        {
            ApplySidebarCollapsed(collapsed);
        }
    }

    public void RestoreWindowBounds()
    {
        try
        {
            var settings = _settings.Current;
            var width = settings.WindowWidth ?? 1280;
            var height = settings.WindowHeight ?? 800;
            int x = 0;
            int y = 0;
            var hasSaved = settings.WindowLeft.HasValue && settings.WindowTop.HasValue &&
                           settings.WindowWidth.HasValue && settings.WindowHeight.HasValue;

            if (hasSaved)
            {
                x = settings.WindowLeft!.Value;
                y = settings.WindowTop!.Value;
                var display = DisplayArea.GetFromRect(new RectInt32(x, y, width, height), DisplayAreaFallback.None);
                if (display is null)
                {
                    hasSaved = false;
                }
            }

            if (!hasSaved)
            {
                var area = DisplayArea.Primary.WorkArea;
                width = Math.Clamp(width, 800, Math.Max(800, area.Width));
                height = Math.Clamp(height, 600, Math.Max(600, area.Height));
                x = area.X + Math.Max(0, (area.Width - width) / 2);
                y = area.Y + Math.Max(0, (area.Height - height) / 2);
            }

            this.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
        catch
        {
            // Fall back to the OS default placement if anything fails.
        }
    }

    private void OnWindowClosingSaveBounds(AppWindow sender, AppWindowClosingEventArgs args)
    {
        try
        {
            var position = this.AppWindow.Position;
            var size = this.AppWindow.Size;
            var current = _settings.Current;
            _ = _settings.UpdateAsync(current with
            {
                WindowLeft = position.X,
                WindowTop = position.Y,
                WindowWidth = size.Width,
                WindowHeight = size.Height,
            });
        }
        catch
        {
        }
    }

    private void ApplySidebarCollapsed(bool collapsed)
    {
        ViewModel.IsSidebarCollapsed = collapsed;
        _shellState.SetPaneOpen(!collapsed);

        var targetWidth = collapsed
            ? Tokens.Double("SidebarCollapsedWidth", 56d)
            : Tokens.Double("SidebarWidth", 264d);

        SidebarColumn.Width = targetWidth;
        SidebarHost.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarIconRail.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;

        // Apply the final layout width in a single step. Animating the layout
        // Width would reflow the time grid on every frame and trigger a full
        // chip rebuild, so only the sidebar's own visual is animated instead.
        SidebarHost.Width = targetWidth;

        if (Motion.IsEnabled)
        {
            Motion.SlideInFromLeftAsync(
                collapsed ? (UIElement)SidebarIconRail : SidebarHost,
                SidebarCollapseDurationMs);
        }
    }

    private void OnSidebarRowClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is NavigationListItem item)
        {
            ViewModel.SelectItem(item);

            if (item.TargetKey == NavigationKeys.Tags)
            {
                ApplyTagFilter(item.Label);
                return;
            }

            _navigation.NavigateTo(item.TargetKey, item.Label);
            return;
        }

        if (element.Tag is string targetKey)
        {
            ViewModel.SelectTarget(targetKey);
            _navigation.NavigateTo(targetKey);
        }
    }

    private void OnTodayClick(object sender, RoutedEventArgs e) => StateViewModel.GoToTodayCommand.Execute(null);

    private void OnPreviousMonthClick(object sender, RoutedEventArgs e) => StateViewModel.PreviousMonthCommand.Execute(null);

    private void OnNextMonthClick(object sender, RoutedEventArgs e) => StateViewModel.NextMonthCommand.Execute(null);

    private void OnPreviousClick(object sender, RoutedEventArgs e) => StateViewModel.GoPreviousCommand.Execute(null);

    private void OnNextClick(object sender, RoutedEventArgs e) => StateViewModel.GoNextCommand.Execute(null);

    private void OnViewSwitcherSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is not SelectorBarItem item || item.Tag is not string tag)
        {
            return;
        }

        if (Enum.TryParse<CalendarViewMode>(tag, out var mode))
        {
            ShowView(mode, NavigationKeyFor(mode));
        }
    }

    private void OnNewSplitButtonClick(object sender, RoutedEventArgs e) => OpenNewEvent();

    private void OnNewEventClick(object sender, RoutedEventArgs e) => OpenNewEvent();

    /// <summary>
    /// Every "new event" entry point funnels here (sidebar New ▸ Event, N accelerator,
    /// command palette). The New flyout is hidden first so it can never float above the
    /// editor, and if the editor is already open the draft is updated in place — never a
    /// second instance.
    /// </summary>
    private async void OpenNewEvent()
    {
        NewMenuButton.Flyout?.Hide();

        if (_editor.IsOpen)
        {
            if (_editor.IsDirty && !await ConfirmDiscardAsync())
            {
                _editorView?.FocusTitle();
                return;
            }

            _editor.DiscardChanges();
        }

        var now = _clock.UtcNow;
        var start = now.AddMinutes(30 - (now.Minute % 30));
        await _editor.OpenNewAsync(start, start.AddHours(1));
    }

    private void OnNewReminderClick(object sender, RoutedEventArgs e) => App.Current.ShowQuickAdd();

    private void OnQuickAddClick(object sender, RoutedEventArgs e) => App.Current.ShowQuickAdd();

    private void OnNewTaskClick(object sender, RoutedEventArgs e) => OpenTodo(TodoService.Inbox);

    private void OpenTodo(string filter)
    {
        ViewModel.SelectItem(ViewModel.TaskLists.FirstOrDefault(item => item.Label == filter)
            ?? ViewModel.ReminderLists.FirstOrDefault(item => item.Label == filter));
        _navigation.NavigateTo(NavigationKeys.Tasks, filter);
    }

    private void OnDayViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Day, NavigationKeys.Day);

    private void OnWeekViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Week, NavigationKeys.Week);

    private void OnMonthViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Month, NavigationKeys.Month);

    private void OnAgendaViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Agenda, NavigationKeys.Agenda);

    private void OnTasksClick(object sender, RoutedEventArgs e) => OpenTodo(TodoService.Inbox);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.Settings);

    private void OnSidebarToggleClick(object sender, RoutedEventArgs e) =>
        ApplySidebarCollapsed(!ViewModel.IsSidebarCollapsed);

    private void ShowView(CalendarViewMode mode, string key)
    {
        ViewModel.ClearNavigationSelection();
        SelectViewInSwitcher(mode);

        if (StateViewModel.ViewMode != mode)
        {
            StateViewModel.SetViewModeCommand.Execute(mode);
        }
        else if (_navigation.CurrentViewKey != key)
        {
            _navigation.NavigateTo(key);
        }
    }

    private static string NavigationKeyFor(CalendarViewMode mode) => mode switch
    {
        CalendarViewMode.Day => NavigationKeys.Day,
        CalendarViewMode.Week => NavigationKeys.Week,
        CalendarViewMode.Month => NavigationKeys.Month,
        _ => NavigationKeys.Agenda,
    };

    private void SelectViewInSwitcher(CalendarViewMode mode)
    {
        if (ViewSwitcher.Items
            .OfType<SelectorBarItem>()
            .FirstOrDefault(item => Equals(item.Tag, mode.ToString())) is { } target)
        {
            ViewSwitcher.SelectedItem = target;
        }

        ApplyViewSwitcherSelection();
    }

    /// <summary>
    /// Selected switcher tab renders as an Accent filled pill with white text; every other tab
    /// stays transparent. All paths that change the view (selector, keyboard shortcuts and
    /// state-driven updates) funnel through SelectViewInSwitcher, so the highlight always follows.
    /// </summary>
    private void ApplyViewSwitcherSelection()
    {
        foreach (var item in ViewSwitcher.Items.OfType<SelectorBarItem>())
        {
            var selected = Equals(item, ViewSwitcher.SelectedItem);
            item.Background = selected
                ? ThemeBrushes.Get(RootGrid, "Accent")
                : ThemeBrushes.Get(RootGrid, "App.TransparentBrush");
            item.Foreground = selected
                ? ThemeBrushes.Get(RootGrid, "App.OnAccentBrush")
                : ThemeBrushes.Get(RootGrid, "TextPrimary");
        }
    }

    private async void OnSyncClick(object sender, RoutedEventArgs e)
    {
        if (_syncInProgress)
        {
            return;
        }

        _syncInProgress = true;
        ViewModel.SyncStatus = "Syncing…";

        try
        {
            await _sync.SyncAsync();
            await RefreshCalendarsAsync();

            ViewModel.SyncStatus = $"Synced just now · {_calendarProviderSummary()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed");
            ViewModel.SyncStatus = "Sync failed";
            ShowStatus("Sync failed. We will retry automatically.", InfoBarSeverity.Error);
        }
        finally
        {
            _syncInProgress = false;
        }
    }

    private string _calendarProviderSummary() =>
        ViewModel.Calendars.Any(item => item.Id != CalendarService.PersonalCalendarId && item.Id != CalendarService.WorkCalendarId)
            ? "Connected accounts"
            : ViewModel.Calendars.Count == 0 ? "No calendars" : "Local";

    private async Task RefreshCalendarsAsync()
    {
        var calendars = await _calendars.GetAllAsync();

        var items = new ObservableCollection<CalendarListItem>(calendars.Select(calendar => new CalendarListItem
        {
            Id = calendar.Id,
            Name = calendar.Name,
            Color = CalendarPalette.Normalize(calendar.Color),
            ColorBrush = CalendarColors.Bar(RootGrid, calendar.Color),
            IsVisible = calendar.Visible,
        }));

        ViewModel.Calendars.Clear();

        foreach (var item in items)
        {
            ViewModel.Calendars.Add(item);
        }

        await RefreshReminderCountsAsync();
        await RefreshTagsAsync();
    }

    private async Task RefreshReminderCountsAsync()
    {
        try
        {
            var counts = await _todo.GetCountsAsync();

            foreach (var item in ViewModel.ReminderLists.Concat(ViewModel.TaskLists))
            {
                if (counts.TryGetValue(item.Label, out var count))
                {
                    item.Count = count;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not refresh sidebar counts");
        }
    }

    /// <summary>Rebuilds the sidebar TAGS section from the distinct tags stored on events.</summary>
    private async Task RefreshTagsAsync()
    {
        try
        {
            var events = await _eventRepository.SearchAsync(new EventSearchCriteria());

            var tags = events
                .SelectMany(calendarEvent => (calendarEvent.Tags ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase);

            ViewModel.SetTags(tags);
            TagsSection.Visibility = ViewModel.Tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not refresh the tag list");
        }
    }

    /// <summary>Filters the event list by a tag by reusing the search pipeline.</summary>
    private async void ApplyTagFilter(string tag)
    {
        _searchCriteria = _searchCriteria with { Tags = new[] { tag } };
        SearchBox.Text = tag;

        await RefreshSearchResultsAsync(tag);

        if (_searchResults.Count == 0)
        {
            ShowStatus($"No events tagged \u201c{tag}\u201d", InfoBarSeverity.Informational);
            return;
        }

        SearchBox.IsSuggestionListOpen = true;
    }

    /// <summary>
    /// The single open path (raised by EventEditorViewModel.OpenAsync / OpenNewAsync).
    /// The drawer is a right-aligned overlay, so showing it cannot change the width,
    /// position or scroll offset of the sidebar or the calendar grid. If it is already
    /// visible (re-open while open) the draft is updated in place without re-animating.
    /// </summary>
    private void OnEditorOpened(object? sender, EventArgs e)
    {
        var alreadyOpen = EditorHost.Visibility == Visibility.Visible;

        CaptureEditorLayoutSnapshot();

        EditorScrim.Visibility = Visibility.Visible;
        EditorHost.IsHitTestVisible = true;
        EditorHost.Visibility = Visibility.Visible;
        InvitationInfoBar.IsOpen = _editor.LoadedEvent is { } loadedEvent &&
            _sync.HasPendingInvitation(loadedEvent);

        if (!alreadyOpen)
        {
            // Commit the drawer's layout before animating so the very first open uses the
            // same path and values as every later open (its right-aligned offset is known).
            EditorHost.UpdateLayout();

            _ = Motion.SlideInFromRightAsync(EditorHost, EditorPaneWidth, Motion.FastDurationMs);
        }

        _logger.LogInformation(
            "Event editor opened (new={IsNew}, visible={Visible}, translation={Translation})",
            _editor.IsNew,
            EditorHost.Visibility,
            EditorHost.Translation);

        _editorView?.FocusTitle();
    }

    /// <summary>
    /// The single close path for every dismissal (X, Cancel, Esc, scrim tap, Save,
    /// Delete). Hides the scrim, slides the drawer out with a translation + opacity
    /// animation and only then collapses it — the layout never reflows while the
    /// drawer is visible, so closing cannot flicker or shift the grid by a frame.
    /// </summary>
    private async Task CloseEditorAsync()
    {
        if (_isClosingEditor || EditorHost.Visibility != Visibility.Visible)
        {
            return;
        }

        _isClosingEditor = true;

        try
        {
            EditorHost.IsHitTestVisible = false;
            EditorScrim.Visibility = Visibility.Collapsed;
            InvitationInfoBar.IsOpen = false;

            await Motion.SlideOutToRightAsync(EditorHost, EditorPaneWidth, Motion.FastDurationMs);

            EditorHost.Visibility = Visibility.Collapsed;
            _editor.NotifyClosed();

            LogEditorLayoutInvariant();
        }
        finally
        {
            _isClosingEditor = false;
        }
    }

    private async void OnInvitationRespondClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Respond to invitation",
            Content = "Choose how you want to respond.",
            PrimaryButtonText = "Accept",
            SecondaryButtonText = "Tentative",
            CloseButtonText = "Decline",
            XamlRoot = RootGrid.XamlRoot,
        };

        var result = await _dialogs.ShowAsync(dialog);
        var status = result switch
        {
            ContentDialogResult.Primary => RsvpStatus.Accepted,
            ContentDialogResult.Secondary => RsvpStatus.Tentative,
            _ => RsvpStatus.Declined,
        };
        await RespondToInvitationAsync(status);
    }

    private async Task RespondToInvitationAsync(RsvpStatus status)
    {
        if (_rsvpInProgress || _editor.LoadedEvent is not { } calendarEvent)
        {
            return;
        }

        _rsvpInProgress = true;
        InvitationInfoBar.IsOpen = false;
        try
        {
            await _sync.RespondToInvitationAsync(calendarEvent, status);
            ShowStatus($"Invitation marked {status.ToString().ToLowerInvariant()}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invitation response failed");
            InvitationInfoBar.IsOpen = true;
            ShowStatus("The invitation response could not be sent. Please try again.", InfoBarSeverity.Error);
        }
        finally
        {
            _rsvpInProgress = false;
        }
    }

    private async void OnEditorCloseRequested(object? sender, EventArgs e)
    {
        if (_editor.IsDirty && !await ConfirmDiscardAsync())
        {
            return;
        }

        _editor.DiscardChanges();
        await CloseEditorAsync();
    }

    private void OnEditorScrimTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _editor.RequestCloseCommand.Execute(null);
    }

    private void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            _editor.RequestCloseCommand.Execute(null);

            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            _editor.RequestSaveCommand.Execute(null);
        }
    }

    /// <summary>
    /// Captures the sidebar and calendar-grid geometry when the editor opens. DEBUG-only:
    /// the drawer is a pure overlay so these values must be identical after close.
    /// </summary>
    [Conditional("DEBUG")]
    private void CaptureEditorLayoutSnapshot() =>
        _editorLayoutSnapshot = (
            SidebarColumn.ActualWidth,
            ContentFrame.ActualWidth,
            ContentFrame.ActualOffset,
            _shellState.IsPaneOpen);

    /// <summary>Asserts (via log) that opening + closing the editor restored the exact prior layout.</summary>
    [Conditional("DEBUG")]
    private void LogEditorLayoutInvariant()
    {
        if (_editorLayoutSnapshot is not { } before)
        {
            return;
        }

        var (sidebarWidth, contentWidth, contentOffset, paneOpen) =
            (SidebarColumn.ActualWidth, ContentFrame.ActualWidth, ContentFrame.ActualOffset, _shellState.IsPaneOpen);

        if (sidebarWidth.Equals(before.SidebarWidth) &&
            contentWidth.Equals(before.ContentWidth) &&
            contentOffset.Equals(before.ContentOffset) &&
            paneOpen == before.PaneOpen)
        {
            _logger.LogInformation(
                "Editor closed: layout invariant OK (sidebar {Sidebar:F0}px, content {Content:F0}px, pane open {PaneOpen})",
                sidebarWidth, contentWidth, paneOpen);
        }
        else
        {
            _logger.LogWarning(
                "Editor close changed the layout! sidebar {SidebarBefore:F0}->{SidebarAfter:F0}, " +
                "content {ContentBefore:F0}->{ContentAfter:F0}, offset {OffsetBefore}->{OffsetAfter}, " +
                "pane open {PaneBefore}->{PaneAfter}",
                before.SidebarWidth, sidebarWidth,
                before.ContentWidth, contentWidth,
                before.ContentOffset, contentOffset,
                before.PaneOpen, paneOpen);
        }

        _editorLayoutSnapshot = null;
    }

    private (double SidebarWidth, double ContentWidth, System.Numerics.Vector3 ContentOffset, bool PaneOpen)? _editorLayoutSnapshot;

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (RootGrid.XamlRoot is null)
        {
            return true;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Discard changes?",
            Content = "You have unsaved changes to this event.",
            PrimaryButtonText = "Discard",
            CloseButtonText = "Keep editing",
            DefaultButton = ContentDialogButton.Close,
        };

        AutomationProperties.SetName(dialog, "Discard changes");

        return await _dialogs.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    private async void OnEditorSaveRequested(object? sender, EventArgs e)
    {
        Core.Services.RecurrenceEditScope? scope = null;

        if (_editor.IsRecurring)
        {
            scope = await Views.RecurrenceEditDialog.ShowAsync(_dialogs, RootGrid.XamlRoot);

            if (scope is null)
            {
                return;
            }
        }

        if (await _editor.SaveAsync(scope))
        {
            await CloseEditorAsync();
            ViewModel.Status = "Event saved";
        }
        else if (!string.IsNullOrWhiteSpace(_editor.ErrorMessage))
        {
            ShowStatus(_editor.ErrorMessage, InfoBarSeverity.Error);
        }
    }

    private async void OnEditorDeleteRequested(object? sender, EventArgs e)
    {
        try
        {
            await _editor.DeleteAsync();

            ViewModel.RecordDeletion(_editor.EventId);
            await CloseEditorAsync();
            ViewModel.Status = "Event deleted";

            Snackbar.Show("Event deleted", "Undo");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete the event");
            ShowStatus("The event could not be deleted.", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private void OnStatusInfoBarClosed(InfoBar sender, object args)
    {
        StatusInfoBar.IsOpen = false;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        _searchCts?.Cancel();
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void OnSearchDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _ = RefreshSearchResultsAsync(SearchBox.Text);
    }

    private async Task RefreshSearchResultsAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(query))
        {
            _searchResults.Clear();
            SearchBox.ItemsSource = null;
            return;
        }

        var criteria = _searchCriteria with { Text = query };
        var items = await _query.SearchAsync(criteria, token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        _searchResults.Clear();

        foreach (var item in items
            .OrderBy(item => item.StartUtc)
            .Take(8))
        {
            _searchResults[item] = FormatSearchResult(item);
        }

        SearchBox.ItemsSource = _searchResults.Values.ToList();
    }

    private void OnSearchFiltersClick(object sender, RoutedEventArgs e)
    {
        SearchFromEnabled.IsChecked = _searchCriteria.FromUtc is not null;
        SearchToEnabled.IsChecked = _searchCriteria.ToUtc is not null;
        SearchFromDate.Date = _searchCriteria.FromUtc?.ToLocalTime() ?? _clock.UtcNow.ToLocalTime();
        SearchToDate.Date = _searchCriteria.ToUtc?.ToLocalTime() ?? _clock.UtcNow.ToLocalTime();
        SearchTagsBox.Text = string.Join(", ", _searchCriteria.Tags);
        SearchCategoryBox.Text = _searchCriteria.Category ?? string.Empty;
        ApplySearchDateFilterState();
    }

    /// <summary>Each checkbox enables only its own date picker.</summary>
    private void OnSearchDateFilterToggled(object sender, RoutedEventArgs e) => ApplySearchDateFilterState();

    private void ApplySearchDateFilterState()
    {
        SearchFromDate.IsEnabled = SearchFromEnabled.IsChecked == true;
        SearchToDate.IsEnabled = SearchToEnabled.IsChecked == true;
    }

    private void OnApplySearchFiltersClick(object sender, RoutedEventArgs e)
    {
        _searchCriteria = new EventSearchCriteria
        {
            FromUtc = SearchFromEnabled.IsChecked == true && SearchFromDate.Date is { } from
                ? new DateTimeOffset(from.Date, TimeSpan.Zero)
                : null,
            ToUtc = SearchToEnabled.IsChecked == true && SearchToDate.Date is { } to
                ? new DateTimeOffset(to.Date.AddDays(1), TimeSpan.Zero)
                : null,
            Tags = SearchTagsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Category = string.IsNullOrWhiteSpace(SearchCategoryBox.Text)
                ? null
                : SearchCategoryBox.Text.Trim(),
        };

        SearchFiltersButton.Flyout?.Hide();
        _searchDebounce.Stop();
        _ = RefreshSearchResultsAsync(SearchBox.Text);
    }

    private void OnClearSearchFiltersClick(object sender, RoutedEventArgs e)
    {
        _searchCriteria = new EventSearchCriteria();
        SearchFromEnabled.IsChecked = false;
        SearchToEnabled.IsChecked = false;
        SearchFromDate.Date = null;
        SearchToDate.Date = null;
        SearchTagsBox.Text = string.Empty;
        SearchCategoryBox.Text = string.Empty;
        ApplySearchDateFilterState();
        SearchFiltersButton.Flyout?.Hide();
        _searchDebounce.Stop();
        _ = RefreshSearchResultsAsync(SearchBox.Text);
    }

    /// <summary>
    /// The overflow menu exposes the same Show week numbers value as Settings, so the toggle
    /// writes through the settings service instead of keeping a second, divergent copy.
    /// </summary>
    private void OnShowWeekNumbersClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item)
        {
            return;
        }

        if (_settings.Current.ShowWeekNumbers == item.IsChecked)
        {
            return;
        }

        _ = _settings.UpdateAsync(_settings.Current with { ShowWeekNumbers = item.IsChecked });
    }

    private static string FormatSearchResult(CalendarItem item)
    {
        var when = item.HasTime
            ? item.StartUtc.ToLocalTime().ToString("ddd d MMM HH:mm", System.Globalization.CultureInfo.CurrentCulture)
            : item.StartUtc.ToLocalTime().ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture);

        return $"{item.Title} · {when}";
    }

    private void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is null && !string.IsNullOrWhiteSpace(sender.Text))
        {
            ShowStatus($"No events match “{sender.Text}”", InfoBarSeverity.Informational);
            return;
        }

        StatusInfoBar.IsOpen = false;
        _shellState.ShowSearch(new ShellFilters(Query: sender.Text));
    }

    private void OnSearchSuggestionSelected(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string label)
        {
            var match = _searchResults.FirstOrDefault(pair => pair.Value == label);

            if (match.Key is not null)
            {
                _ = _editor.OpenAsync(match.Key);
            }
        }
    }

    private void OnCalendarListRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not ListView list)
        {
            return;
        }

        var element = e.OriginalSource as FrameworkElement;
        var container = element is null ? null : FindAncestor<ListViewItem>(element);
        var item = container?.DataContext as CalendarListItem ?? list.SelectedItem as CalendarListItem;

        if (item is null)
        {
            return;
        }

        var flyout = BuildCalendarContextFlyout(item);

        OverlayFocus.ShowAt(flyout, list, new FlyoutShowOptions { Position = e.GetPosition(list) });
    }

    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private MenuFlyout BuildCalendarContextFlyout(CalendarListItem item)
    {
        var flyout = new MenuFlyout();

        var colour = new MenuFlyoutSubItem { Text = "Colour" };

        foreach (var paletteName in CalendarPalette.All)
        {
            var entry = new MenuFlyoutItem { Text = paletteName };
            entry.Click += async (_, _) => await SetCalendarColorAsync(item.Id, paletteName);
            colour.Items.Add(entry);
        }

        flyout.Items.Add(colour);

        var showOnly = new MenuFlyoutItem { Text = "Show only this calendar" };
        showOnly.Click += async (_, _) => await ShowOnlyCalendarAsync(item.Id);
        flyout.Items.Add(showOnly);

        var settings = new MenuFlyoutItem { Text = "Calendar settings" };
        settings.Click += (_, _) => _navigation.NavigateTo(NavigationKeys.Settings);
        flyout.Items.Add(settings);

        return flyout;
    }

    private async Task SetCalendarColorAsync(string calendarId, string color)
    {
        var calendars = await _calendars.GetAllAsync();
        var calendar = calendars.FirstOrDefault(item => item.Id == calendarId);

        if (calendar is null)
        {
            return;
        }

        await _calendars.UpsertAsync(calendar with { Color = color });

        await RefreshCalendarsAsync();
    }

    private async Task ShowOnlyCalendarAsync(string calendarId)
    {
        var calendars = await _calendars.GetAllAsync();

        foreach (var calendar in calendars)
        {
            await _calendars.UpsertAsync(calendar with { Visible = calendar.Id == calendarId });
        }

        await RefreshCalendarsAsync();
    }

    private async void OnSnackbarAction(object? sender, EventArgs e)
    {
        await UndoLastDeleteAsync();
    }

    private async Task UndoLastDeleteAsync()
    {
        if (!ViewModel.CanUndoLastDelete)
        {
            return;
        }

        try
        {
            await _events.UndoDeleteAsync(ViewModel.LastDeletedEventId);
            ViewModel.RecordDeletion(string.Empty);
            ViewModel.Status = "Event restored";
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            ViewModel.Status = "Nothing to undo";
        }
    }

    private void OnNavigated(object? sender, string key)    {
        _logger.LogInformation("Navigated to {ViewKey}", key);
        ViewModel.Status = key;
        ContentFrame.Visibility = Visibility.Visible;
        UpdateChromeForView(key);
        _ = Motion.FadeInAsync(ContentFrame);
    }

    /// <summary>
    /// Calendar views keep the period navigation and view switcher. Settings and the
    /// Tasks/Reminders page replace them with a single page title.
    /// </summary>
    private void UpdateChromeForView(string key)
    {
        var isCalendarView = key is NavigationKeys.Day
            or NavigationKeys.Week
            or NavigationKeys.Month
            or NavigationKeys.Agenda;

        PeriodNavigationPanel.Visibility = isCalendarView ? Visibility.Visible : Visibility.Collapsed;
        ViewSwitcher.Visibility = isCalendarView ? Visibility.Visible : Visibility.Collapsed;
        PageTitleText.Visibility = isCalendarView ? Visibility.Collapsed : Visibility.Visible;

        if (isCalendarView)
        {
            if (ViewSwitcher.SelectedItem is null)
            {
                SelectViewInSwitcher(StateViewModel.ViewMode);
            }

            return;
        }

        // Never leave a calendar tab selected while a non-calendar page is open.
        ViewSwitcher.SelectedItem = null;
        PageTitleText.Text = key switch
        {
            NavigationKeys.Settings => "Settings",
            NavigationKeys.Tasks => "Tasks",
            NavigationKeys.Reminders => "Reminders",
            NavigationKeys.Tags => "Tags",
            _ => "Calendar",
        };
    }

    private async void OnStateViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalendarStateViewModel.SelectedDate)
            or nameof(CalendarStateViewModel.LocalDate))
        {
            _shellState.SetSelectedDate(StateViewModel.SelectedDate);
            await SyncMiniCalendarAsync();
        }

        if (e.PropertyName != nameof(CalendarStateViewModel.ViewMode))
        {
            return;
        }

        var mode = StateViewModel.ViewMode;
        var key = NavigationKeyFor(mode);

        SelectViewInSwitcher(mode);

        if (_navigation.CurrentViewKey != key)
        {
            _navigation.NavigateTo(key);
        }

        await SyncMiniCalendarAsync();
    }

    private async Task SyncMiniCalendarAsync()
    {
        var date = StateViewModel.SelectedDate == default
            ? StateViewModel.LocalDate
            : StateViewModel.SelectedDate;

        if (MiniCalendar.VisibleYear != date.Year || MiniCalendar.VisibleMonth != date.Month)
        {
            MiniCalendar.VisibleYear = date.Year;
            MiniCalendar.VisibleMonth = date.Month;
        }

        await MiniCalendar.RefreshAsync();
    }

    private void OnNewAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        OpenNewEvent();
    }

    private void OnNewReminderAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        App.Current.ShowQuickAdd();
    }

    private void OnTodayAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        StateViewModel.GoToTodayCommand.Execute(null);
    }

    private void OnDayViewAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        ShowView(CalendarViewMode.Day, NavigationKeys.Day);
    }

    private void OnWeekViewAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        ShowView(CalendarViewMode.Week, NavigationKeys.Week);
    }

    private void OnMonthViewAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        ShowView(CalendarViewMode.Month, NavigationKeys.Month);
    }

    private void OnAgendaViewAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        ShowView(CalendarViewMode.Agenda, NavigationKeys.Agenda);
    }

    private void OnFindAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        SearchBox.Focus(FocusState.Programmatic);
    }

    private async void OnCommandPaletteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowCommandPaletteAsync();
    }

    private async Task ShowCommandPaletteAsync()
    {
        var commands = new List<PaletteCommand>
        {
            new("Quick add (Win+Alt+C)", () => App.Current.ShowQuickAdd()),
            new("New event", OpenNewEvent),
            new("New reminder", () => App.Current.ShowQuickAdd()),
            new("Go to today", () => StateViewModel.GoToTodayCommand.Execute(null)),
            new("Day view", () => ShowView(CalendarViewMode.Day, NavigationKeys.Day)),
            new("Week view", () => ShowView(CalendarViewMode.Week, NavigationKeys.Week)),
            new("Month view", () => ShowView(CalendarViewMode.Month, NavigationKeys.Month)),
            new("Agenda view", () => ShowView(CalendarViewMode.Agenda, NavigationKeys.Agenda)),
            new("Settings", () => _navigation.NavigateTo(NavigationKeys.Settings)),
            new("Import .ics calendar file", () => _ = ImportIcsAsync()),
            new("Export calendar to .ics file", () => _ = ExportIcsAsync()),
            new("Toggle weekends", () => WeekViewModel.ShowWeekends = !WeekViewModel.ShowWeekends),
        };

        var list = new ListView
        {
            ItemsSource = commands,
            SelectionMode = ListViewSelectionMode.Single,
        };

        AutomationProperties.SetName(list, "Command palette");

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Command palette",
            CloseButtonText = "Close",
            Content = list,
        };

        var result = await _dialogs.ShowAsync(dialog);

        if (result == ContentDialogResult.None && list.SelectedItem is PaletteCommand command)
        {
            command.Execute();
        }
    }

    private async Task ExportIcsAsync()
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedFileName = "calendar";
            picker.FileTypeChoices.Add("iCalendar file", new List<string> { ".ics" });
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var events = await _eventRepository.GetInRangeAsync(null, _clock.UtcNow.AddYears(-10), _clock.UtcNow.AddYears(10));
            await FileIO.WriteTextAsync(file, _ics.Export(events));
            ShowStatus("Calendar exported.", InfoBarSeverity.Success);
        }

    private async Task ImportIcsAsync()
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".ics");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var calendar = (await _calendars.GetAllAsync()).FirstOrDefault(item => item.IsLocal && !item.ReadOnly);
            if (calendar is null)
            {
                ShowStatus("Create a local calendar before importing.", InfoBarSeverity.Warning);
                return;
            }

            var imported = _ics.Import(await FileIO.ReadTextAsync(file), calendar.Id, _clock.UtcNow);
            foreach (var calendarEvent in imported)
            {
                await _events.UpdateAsync(calendarEvent);
            }

            await RefreshCalendarsAsync();
            ShowStatus($"Imported {imported.Count} event(s).", InfoBarSeverity.Success);
        }

    private async void OnUndoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        await UndoLastDeleteAsync();
    }

    private void OnPreviousAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        StateViewModel.GoPreviousCommand.Execute(null);
    }

    private void OnNextAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        StateViewModel.GoNextCommand.Execute(null);
    }

    private bool IsTextInputActive()
    {
        var xamlRoot = RootGrid.XamlRoot;

        if (xamlRoot is null)
        {
            return false;
        }

        return FocusManager.GetFocusedElement(xamlRoot) is TextBox or PasswordBox or RichEditBox;
    }

    private sealed record PaletteCommand(string Label, Action Execute)
    {
        public override string ToString() => Label;
    }
}
