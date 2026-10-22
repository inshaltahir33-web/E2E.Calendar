using System.Collections.ObjectModel;
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
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App;

public sealed partial class MainWindow : Window
{
    private const double CompactWidthThreshold = 900;
    private const int SidebarCollapseDurationMs = 167;

    private readonly NavigationService _navigation;
    private readonly CalendarService _calendars;
    private readonly IReminderRepository _reminders;
    private readonly EventService _events;
    private readonly EventQueryService _query;
    private readonly IClock _clock;
    private readonly ILogger<MainWindow> _logger;
    private readonly EventEditorViewModel _editor;
    private readonly SettingsService _settings;

    private readonly Dictionary<CalendarItem, string> _searchResults = new();
    private readonly DispatcherQueueTimer _searchDebounce;
    private CancellationTokenSource? _searchCts;

    private bool _lastAutoCollapsed;
    private HotkeyService? _hotkey;

    public void InitializeQuickAddHotkey(HotkeyService hotkey)
    {
        _hotkey = hotkey;

        // App owns the hotkey subscription; the window only ensures the native
        // registration is released when the window closes.
        this.AppWindow.Closing += (_, _) => _hotkey?.Dispose();
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        CalendarStateViewModel stateViewModel,
        NavigationService navigation,
        CalendarService calendars,
        EventService events,
        EventQueryService query,
        IClock clock,
        ILogger<MainWindow> logger,
        EventEditorViewModel editor,
        WeekViewModel weekViewModel,
        MonthViewModel monthViewModel,
        MiniCalendarViewModel miniCalendar,
        IReminderRepository reminders,
        SettingsService settings)
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

        ViewModel = viewModel;
        StateViewModel = stateViewModel;
        _navigation = navigation;
        _calendars = calendars;
        _events = events;
        _query = query;
        _clock = clock;
        _logger = logger;
        _editor = editor;
        WeekViewModel = weekViewModel;
        MonthViewModel = monthViewModel;
        MiniCalendar = miniCalendar;
        _reminders = reminders;
        _settings = settings;

        this.InitializeComponent();

        _searchDebounce = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _searchDebounce.Interval = TimeSpan.FromMilliseconds(250);
        _searchDebounce.IsRepeating = false;
        _searchDebounce.Tick += OnSearchDebounceTick;

        _navigation.Frame = ContentFrame;
        _navigation.Navigated += OnNavigated;

        StateViewModel.PropertyChanged += OnStateViewModelPropertyChanged;

        EditorHost.Children.Add(new Views.EventEditorView(editor));
        MiniCalendarHost.Children.Add(new Controls.MiniCalendarView(miniCalendar));

        editor.Opened += OnEditorOpened;
        editor.CloseRequested += OnEditorCloseRequested;
        editor.SaveRequested += OnEditorSaveRequested;
        editor.DeleteRequested += OnEditorDeleteRequested;

        EditorHost.KeyDown += OnEditorKeyDown;

        Snackbar.ActionInvoked += OnSnackbarAction;
        PlaceholderState.ActionInvoked += OnPlaceholderAction;

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(TitleBarElement);

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            this.SystemBackdrop = new MicaBackdrop();
        }

        this.SizeChanged += OnWindowSizeChanged;
        RootGrid.Loaded += OnLoaded;
    }

    public MainWindowViewModel ViewModel { get; }

    public CalendarStateViewModel StateViewModel { get; }

    public WeekViewModel WeekViewModel { get; }

    public MonthViewModel MonthViewModel { get; }

    public MiniCalendarViewModel MiniCalendar { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Motion.Refresh();

        await RefreshCalendarsAsync();
        _navigation.NavigateTo(NavigationKeys.Week);

        if (!_settings.Current.OnboardingComplete)
        {
            await Views.OnboardingView.ShowAsync(RootGrid.XamlRoot, _settings);
        }
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        var collapsed = args.Size.Width < CompactWidthThreshold;

        if (collapsed != _lastAutoCollapsed)
        {
            _lastAutoCollapsed = collapsed;
            ApplySidebarCollapsed(collapsed);
        }
    }

    private void OnPaneToggleClick(object sender, RoutedEventArgs e)
    {
        ApplySidebarCollapsed(!ViewModel.IsSidebarCollapsed);
    }

    private void ApplySidebarCollapsed(bool collapsed)
    {
        ViewModel.IsSidebarCollapsed = collapsed;

        var targetWidth = collapsed
            ? Tokens.Double("SidebarCollapsedWidth", 48d)
            : Tokens.Double("SidebarWidth", 272d);

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
        if (sender is not FrameworkElement element || element.Tag is not string targetKey)
        {
            return;
        }

        ViewModel.SelectTarget(targetKey);
        _navigation.NavigateTo(targetKey);
    }

    private void OnTodayClick(object sender, RoutedEventArgs e) => StateViewModel.GoToTodayCommand.Execute(null);

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
            StateViewModel.SetViewModeCommand.Execute(mode);
        }
    }

    private void OnNewSplitButtonClick(SplitButton sender, SplitButtonClickEventArgs args) => OpenNewEvent();

    private void OnNewSplitButtonClick(object sender, RoutedEventArgs e) => OpenNewEvent();

    private void OnNewEventClick(object sender, RoutedEventArgs e) => OpenNewEvent();

    private void OnNewReminderClick(object sender, RoutedEventArgs e) => App.Current.ShowQuickAdd();

    private void OnNewTaskClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.Tasks);

    private async void OpenNewEvent()
    {
        var now = _clock.UtcNow;
        var start = now.AddMinutes(30 - (now.Minute % 30));
        await _editor.OpenNewAsync(start, start.AddHours(1));
    }

    private void OnDayViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Day, NavigationKeys.Day);

    private void OnWeekViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Week, NavigationKeys.Week);

    private void OnMonthViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Month, NavigationKeys.Month);

    private void OnAgendaViewClick(object sender, RoutedEventArgs e) => ShowView(CalendarViewMode.Agenda, NavigationKeys.Agenda);

    private void OnTasksClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.Tasks);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.Settings);

    private void OnPremiumClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.Premium);

    private void ShowView(CalendarViewMode mode, string key)
    {
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
    }

    private async void OnSyncClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SyncStatus = "Syncing…";

        try
        {
            await RefreshCalendarsAsync();

            ViewModel.SyncStatus = $"Synced just now · {_calendarProviderSummary()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed");
            ViewModel.SyncStatus = "Sync failed";
            ShowStatus("Sync failed. We will retry automatically.", InfoBarSeverity.Error);
        }
    }

    private string _calendarProviderSummary() =>
        ViewModel.Calendars.Count == 0 ? "No calendars" : "Local";

    private async Task RefreshCalendarsAsync()
    {
        var calendars = await _calendars.GetAllAsync();

        var items = new ObservableCollection<CalendarListItem>(calendars.Select(calendar => new CalendarListItem
        {
            Id = calendar.Id,
            Name = calendar.Name,
            Color = calendar.Color ?? CalendarPalette.Other,
            ColorBrush = ColorHelper.FromHex(calendar.Color),
            IsVisible = calendar.Visible,
        }));

        ViewModel.Calendars.Clear();

        foreach (var item in items)
        {
            ViewModel.Calendars.Add(item);
        }

        await RefreshReminderCountsAsync();
    }

    private async Task RefreshReminderCountsAsync()
    {
        var now = _clock.UtcNow;
        var timeZone = _clock.LocalTimeZone;
        var today = DateOnly.FromDateTime(timeZone.ToLocal(now));
        var endOfToday = timeZone.ToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var reminders = await _reminders.GetInRangeAsync(now.AddYears(-5), endOfToday.AddYears(5));

        var open = reminders.Where(reminder => reminder.DoneUtc is null).ToList();

        SetCount("Today", open.Count(reminder => reminder.DueUtc is not null &&
            DateOnly.FromDateTime(timeZone.ToLocal(reminder.DueUtc.Value)) == today));

        SetCount("Scheduled", open.Count(reminder => reminder.DueUtc is not null && reminder.DueUtc > now));

        SetCount("Overdue", open.Count(reminder => reminder.DueUtc is not null && reminder.DueUtc < now));

        SetCount("Completed", reminders.Count(reminder => reminder.DoneUtc is not null));

        SetCount("Flagged", open.Count(reminder => reminder.Flagged));
    }

    private void SetCount(string label, int count)
    {
        var item = ViewModel.ReminderLists.FirstOrDefault(entry => entry.Label == label);

        if (item is not null)
        {
            item.Count = count;
        }
    }

    private void OnEditorOpened(object? sender, EventArgs e)
    {
        EditorHost.Visibility = Visibility.Visible;
        _ = Motion.SlideAndFadeInAsync(EditorHost, 8);
    }

    private async void OnEditorCloseRequested(object? sender, EventArgs e)
    {
        if (_editor.IsDirty && !await ConfirmDiscardAsync())
        {
            return;
        }

        _editor.DiscardChanges();
        EditorHost.Visibility = Visibility.Collapsed;
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

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void OnEditorSaveRequested(object? sender, EventArgs e)
    {
        Core.Services.RecurrenceEditScope? scope = null;

        if (_editor.IsRecurring)
        {
            scope = await Views.RecurrenceEditDialog.ShowAsync(RootGrid.XamlRoot);

            if (scope is null)
            {
                return;
            }
        }

        if (await _editor.SaveAsync(scope))
        {
            EditorHost.Visibility = Visibility.Collapsed;
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
            EditorHost.Visibility = Visibility.Collapsed;
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

    private void OnPlaceholderAction(object? sender, EventArgs e) =>
        OpenNewEvent();

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

        var now = _clock.UtcNow;
        var items = await _query.GetItemsAsync(now.AddYears(-1), now.AddYears(1), cancellationToken: token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        _searchResults.Clear();

        foreach (var item in items
            .Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.StartUtc)
            .Take(8))
        {
            _searchResults[item] = FormatSearchResult(item);
        }

        SearchBox.ItemsSource = _searchResults.Values.ToList();
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

        flyout.ShowAt(list, new FlyoutShowOptions { Position = e.GetPosition(list) });
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

        foreach (var hex in CalendarPalette.All)
        {
            var entry = new MenuFlyoutItem { Text = hex };
            entry.Click += async (_, _) => await SetCalendarColorAsync(item.Id, hex);
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
        var hasView = _navigation.RegisteredViews.Contains(key);

        _logger.LogInformation("Navigated to {ViewKey} (implemented: {IsImplemented})", key, hasView);

        ViewModel.Status = hasView ? key : $"{key} is not available yet";
        PlaceholderState.Visibility = hasView ? Visibility.Collapsed : Visibility.Visible;
        ContentFrame.Visibility = hasView ? Visibility.Visible : Visibility.Collapsed;

        if (hasView)
        {
            _ = Motion.FadeInAsync(ContentFrame);
        }
    }

    private void OnStateViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
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

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.None && list.SelectedItem is PaletteCommand command)
        {
            command.Execute();
        }
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
