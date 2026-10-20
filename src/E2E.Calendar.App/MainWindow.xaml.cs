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
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App;

public sealed partial class MainWindow : Window
{
    private const double CompactWidthThreshold = 900;

    private readonly NavigationService _navigation;
    private readonly CalendarService _calendars;
    private readonly EventService _events;
    private readonly IClock _clock;
    private readonly ILogger<MainWindow> _logger;
    private readonly EventEditorViewModel _editor;

    private bool _lastAutoCollapsed;

    public MainWindow(
        MainWindowViewModel viewModel,
        CalendarStateViewModel stateViewModel,
        NavigationService navigation,
        CalendarService calendars,
        EventService events,
        IClock clock,
        ILogger<MainWindow> logger,
        EventEditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(stateViewModel);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(calendars);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(editor);

        ViewModel = viewModel;
        StateViewModel = stateViewModel;
        _navigation = navigation;
        _calendars = calendars;
        _events = events;
        _clock = clock;
        _logger = logger;
        _editor = editor;

        this.InitializeComponent();

        _navigation.Frame = ContentFrame;
        _navigation.Navigated += OnNavigated;

        StateViewModel.PropertyChanged += OnStateViewModelPropertyChanged;

        EditorHost.Children.Add(new Views.EventEditorView(editor));

        editor.Opened += OnEditorOpened;
        editor.CloseRequested += OnEditorCloseRequested;
        editor.SaveRequested += OnEditorSaveRequested;
        editor.DeleteRequested += OnEditorDeleteRequested;

        Snackbar.ActionInvoked += OnSnackbarAction;

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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshCalendarsAsync();
        _navigation.NavigateTo(NavigationKeys.Week);
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

        SidebarHost.Width = collapsed
            ? Tokens.Double("SidebarCollapsedWidth", 48d)
            : Tokens.Double("SidebarWidth", 272d);
        SidebarScroll.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarIconRail.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
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

    private void OnMiniCalendarSelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
    {
        if (args.AddedDates.Count == 0)
        {
            return;
        }

        var selected = args.AddedDates[0];
        var localDate = DateOnly.FromDateTime(selected.Date);
        var localStart = localDate.ToDateTime(TimeOnly.MinValue);

        StateViewModel.CurrentDate = _clock.LocalTimeZone.ToUtc(localStart);
    }

    private void OnNewSplitButtonClick(SplitButton sender, SplitButtonClickEventArgs args) =>
        _navigation.NavigateTo(NavigationKeys.NewEvent);

    private void OnNewClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.NewEvent);

    private void OnNewEventClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.NewEvent);

    private void OnNewReminderClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.NewReminder);

    private void OnNewTaskClick(object sender, RoutedEventArgs e) => _navigation.NavigateTo(NavigationKeys.NewTask);

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

        await RefreshCalendarsAsync();

        ViewModel.SyncStatus = $"Synced just now · {_calendarProviderSummary()}";
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
    }

    private void OnEditorOpened(object? sender, EventArgs e)
    {
        EditorHost.Visibility = Visibility.Visible;
    }

    private void OnEditorCloseRequested(object? sender, EventArgs e)
    {
        EditorHost.Visibility = Visibility.Collapsed;
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
    }

    private async void OnEditorDeleteRequested(object? sender, EventArgs e)
    {
        await _editor.DeleteAsync();

        ViewModel.RecordDeletion(_editor.EventId);
        EditorHost.Visibility = Visibility.Collapsed;
        ViewModel.Status = "Event deleted";

        Snackbar.Show("Event deleted", "Undo");
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
        ViewPlaceholder.Visibility = hasView ? Visibility.Collapsed : Visibility.Visible;
        ContentFrame.Visibility = hasView ? Visibility.Visible : Visibility.Collapsed;
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
        _navigation.NavigateTo(NavigationKeys.NewEvent);
    }

    private void OnNewReminderAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputActive())
        {
            return;
        }

        args.Handled = true;
        _navigation.NavigateTo(NavigationKeys.NewReminder);
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
            new("New event", () => _navigation.NavigateTo(NavigationKeys.NewEvent)),
            new("New reminder", () => _navigation.NavigateTo(NavigationKeys.NewReminder)),
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
