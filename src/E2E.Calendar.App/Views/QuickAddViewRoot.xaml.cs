using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace E2E.Calendar.App.Views;

/// <summary>
/// The inner content of the Quick Add window. It lives in its own control so the
/// compiled bindings resolve against a <see cref="FrameworkElement"/> root.
/// </summary>
public sealed partial class QuickAddViewRoot : Microsoft.UI.Xaml.Controls.UserControl
{
    public QuickAddViewRoot(QuickAddViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();
    }

    public QuickAddViewModel ViewModel { get; }

    public event EventHandler? SaveRequested;

    public event EventHandler? CancelRequested;

    public event EventHandler? CycleCalendarRequested;

    public global::Microsoft.UI.Xaml.Controls.TextBox InputControl => InputBox;

    public void FocusInput() => InputBox.Focus(FocusState.Programmatic);

    private void OnInputTextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e) =>
        ViewModel.Reparse();

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                e.Handled = true;
                SaveRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Windows.System.VirtualKey.Escape:
                e.Handled = true;
                CancelRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Windows.System.VirtualKey.Tab:
                e.Handled = true;
                CycleCalendarRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnCalendarChipClick(object sender, RoutedEventArgs e) =>
        CycleCalendarRequested?.Invoke(this, EventArgs.Empty);

    private void OnCancelClick(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
}
