using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace E2E.Calendar.App.Views;

/// <summary>
/// Borderless, always-on-top quick capture window. It is invoked from the tray
/// hotkey or the command palette and closes as soon as the item is saved.
/// </summary>
public sealed partial class QuickAddView : Window
{
    private const int WindowWidth = 560;
    private const int WindowHeight = 300;

    private readonly QuickAddViewModel _viewModel;
    private readonly QuickAddViewRoot _root;
    private bool _closing;

    public QuickAddView(QuickAddViewModel viewModel)
    {
        _viewModel = viewModel;

        _root = new QuickAddViewRoot(viewModel);
        Content = _root;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(_root);

        SystemBackdrop = new DesktopAcrylicBackdrop();

        ConfigurePresenter();

        _root.SaveRequested += OnSaveRequested;
        _root.CancelRequested += OnCancelRequested;
        _root.CycleCalendarRequested += OnCycleCalendarRequested;

        _viewModel.Saved += OnSaved;

        _root.Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public QuickAddViewModel ViewModel => _viewModel;

    private void ConfigurePresenter()
    {
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }

        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var work = display.WorkArea;

        AppWindow.MoveAndResize(new RectInt32(
            work.X + ((work.Width - WindowWidth) / 2),
            work.Y + ((work.Height - WindowHeight) / 3),
            WindowWidth,
            WindowHeight));
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();

        _root.FocusInput();

        _ = Motion.SlideAndFadeInAsync(_root, 12, Motion.FastDurationMs);
    }

    private void OnCycleCalendarRequested(object? sender, EventArgs e) => _viewModel.CycleCalendarCommand.Execute(null);

    private void OnCancelRequested(object? sender, EventArgs e) => Close();

    private void OnSaveRequested(object? sender, EventArgs e) => _ = SaveAsync();

    private async Task SaveAsync()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;

        var saved = await _viewModel.SaveAsync();

        if (!saved)
        {
            _closing = false;
        }
    }

    private void OnSaved(object? sender, EventArgs e) => Close();

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        _viewModel.Saved -= OnSaved;
        _root.SaveRequested -= OnSaveRequested;
        _root.CancelRequested -= OnCancelRequested;
        _root.CycleCalendarRequested -= OnCycleCalendarRequested;
        Closed -= OnClosed;

        await App.Current.OnQuickAddClosedAsync();
    }
}
