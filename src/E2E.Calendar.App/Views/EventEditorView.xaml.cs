using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Views;

public partial class EventEditorView : UserControl
{
    public EventEditorView(EventEditorViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();
    }

    public EventEditorViewModel ViewModel { get; }

    private void OnCloseClick(object sender, RoutedEventArgs e) => ViewModel.RequestCloseCommand.Execute(null);

    private void OnSaveClick(object sender, RoutedEventArgs e) => ViewModel.RequestSaveCommand.Execute(null);

    private void OnDeleteClick(object sender, RoutedEventArgs e) => ViewModel.RequestDeleteCommand.Execute(null);

    private void OnAddAlertClick(object sender, RoutedEventArgs e) => ViewModel.AddAlertCommand.Execute(null);

    private void OnRemoveAlertClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is AlertRow row)
        {
            ViewModel.RemoveAlertCommand.Execute(row);
        }
    }

    private void OnDetectMeetingClick(object sender, RoutedEventArgs e) => ViewModel.DetectMeetingLinkCommand.Execute(null);

    private void OnClearMeetingClick(object sender, RoutedEventArgs e) => ViewModel.ClearMeetingLinkCommand.Execute(null);

    private void OnNextColorClick(object sender, RoutedEventArgs e) => ViewModel.NextColorCommand.Execute(null);

    private void OnPreviousColorClick(object sender, RoutedEventArgs e) => ViewModel.PreviousColorCommand.Execute(null);

    private void OnTemplateSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is EventTemplate template)
        {
            ViewModel.ApplyTemplate(template);
        }
    }
}
