using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Controls;

public sealed partial class SnackbarView : UserControl
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(10);

    private readonly DispatcherTimer _timer;

    public SnackbarView()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = DefaultDuration };
        _timer.Tick += OnTimerTick;

        Visibility = Visibility.Collapsed;
    }

    public event EventHandler? ActionInvoked;

    public event EventHandler? Dismissed;

    public void Show(string message, string actionLabel)
    {
        MessageText.Text = message;
        ActionButton.Content = actionLabel;
        Visibility = Visibility.Visible;
        _timer.Start();
    }

    public void Hide()
    {
        _timer.Stop();
        Visibility = Visibility.Collapsed;
    }

    private void OnTimerTick(object? sender, object e)
    {
        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        Hide();
        ActionInvoked?.Invoke(this, EventArgs.Empty);
    }
}
