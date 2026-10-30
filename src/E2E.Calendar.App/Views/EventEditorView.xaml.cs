using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace E2E.Calendar.App.Views;

public partial class EventEditorView : UserControl
{
    private string? _notesSnapshot;
    private bool _syncingNotes = true;

    public EventEditorView(EventEditorViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Opened += OnOpened;

        UpdateAllDayVisibility();
        UpdateColorSwatch();
        UpdateHeader();
        UpdateDeleteVisibility();
        LoadNotesIntoEditor();
    }

    public EventEditorViewModel ViewModel { get; }

    private void OnOpened(object? sender, EventArgs e)
    {
        UpdateAllDayVisibility();
        UpdateColorSwatch();
        UpdateHeader();
        UpdateDeleteVisibility();
        ResetScrollOffset();
        LoadNotesIntoEditor();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EventEditorViewModel.IsAllDay):
                UpdateAllDayVisibility();
                break;
            case nameof(EventEditorViewModel.Color):
            case nameof(EventEditorViewModel.ColorLabel):
            case nameof(EventEditorViewModel.CalendarId):
                UpdateColorSwatch();
                break;
            case nameof(EventEditorViewModel.IsNew):
                UpdateHeader();
                UpdateDeleteVisibility();
                break;
            case nameof(EventEditorViewModel.Notes):
                LoadNotesIntoEditor();
                break;
        }
    }

    /// <summary>Pushes the view model's sanitised HTML notes into the rich-text editor.</summary>
    private void LoadNotesIntoEditor()
    {
        var html = ViewModel.Notes;
        if (string.Equals(html, _notesSnapshot, StringComparison.Ordinal))
        {
            return;
        }

        _notesSnapshot = html;

        // RichEditBox raises TextChanged asynchronously, so stay guarded until the
        // document has settled; otherwise loading would mark the editor dirty.
        _syncingNotes = true;
        NotesBox.Document.SetText(TextSetOptions.FormatRtf, NotesText.HtmlToRtf(html));
        DispatcherQueue.TryEnqueue(() => _syncingNotes = false);
    }

    private void OnNotesTextChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingNotes)
        {
            return;
        }

        NotesBox.Document.GetText(TextGetOptions.FormatRtf, out var rtf);
        var html = NotesText.RtfToHtml(rtf);
        if (string.Equals(html, _notesSnapshot, StringComparison.Ordinal))
        {
            return;
        }

        _notesSnapshot = html;
        ViewModel.Notes = html;
    }

    private void UpdateAllDayVisibility()
    {
        TimedPickers.Visibility = ViewModel.IsAllDay ? Visibility.Collapsed : Visibility.Visible;
        AllDayPickers.Visibility = ViewModel.IsAllDay ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateColorSwatch()
    {
        var color = ViewModel.Color ?? ViewModel.SelectedCalendar?.Color;
        ColorSwatch.Background = CalendarColors.Bar(this, color);
    }

    private void UpdateHeader()
    {
        HeaderText.Text = ViewModel.IsNew ? "New event" : "Edit event";
    }

    private void UpdateDeleteVisibility()
    {
        DeleteButton.Visibility = ViewModel.IsNew ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ResetScrollOffset()
    {
        try
        {
            EditorScrollViewer.ChangeView(0, 0, 1, true);
        }
        catch
        {
        }
    }

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
