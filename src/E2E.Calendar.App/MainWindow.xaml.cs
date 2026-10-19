using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using E2E.Calendar.App.ViewModels;

namespace E2E.Calendar.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        this.InitializeComponent();
        this.ViewModel = viewModel;

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            this.SystemBackdrop = new MicaBackdrop();
        }
    }

    public MainWindowViewModel ViewModel { get; }
}
