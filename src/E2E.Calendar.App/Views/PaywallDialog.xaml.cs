using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public sealed partial class PaywallDialog : ContentDialog
{
    public PaywallDialog(PaywallViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;

        InitializeComponent();

        Opened += OnOpened;
    }

    public PaywallViewModel ViewModel { get; }

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args) => await ViewModel.LoadAsync();

    private async void OnBuyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PaywallPlan plan })
        {
            return;
        }

        if (await ViewModel.PurchaseAsync(plan))
        {
            Hide();
        }
    }

    private async void OnRestoreClick(object sender, RoutedEventArgs e) => await ViewModel.RestoreAsync();
}
