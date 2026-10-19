using E2E.Calendar.App.Controls;
using E2E.Calendar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public partial class WeekView : Page
{
    public WeekView(WeekViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();
    }

    public WeekViewModel ViewModel { get; }

    private void OnItemActivated(object sender, TimeGridItemEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"Activated {args.Item.Id}");
    }

    private void OnSlotCreated(object sender, TimeGridSlotEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"Slot {args.StartUtc:u} - {args.EndUtc:u}");
    }
}
