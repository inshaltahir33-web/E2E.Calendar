using CommunityToolkit.Mvvm.ComponentModel;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "E2E Calendar";

    [ObservableProperty]
    private string _status = "Ready";
}
