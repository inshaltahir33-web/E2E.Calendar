using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace E2E.Calendar.App.Views;

public partial class EventEditorView : UserControl
{
    // "Wed, 21 Oct 2026" — CalendarDatePicker takes a DateTimeFormatter template, not a
    // .NET format string, so the pattern is spelled out here and applied to every date picker.
    private const string DateFormatPattern =
        "{dayofweek.abbreviated(3)}, {day.integer} {month.abbreviated} {year.full}";

    private readonly ThemeShadow _drawerShadow = new();
    private readonly IDialogService _dialogs;
    private string? _notesSnapshot;
    private bool _syncingNotes = true;

    public EventEditorView(EventEditorViewModel viewModel, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        ViewModel = viewModel;
        _dialogs = dialogs;

        InitializeComponent();

        ApplyDrawerShadow();
        ApplyDateFormat();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Opened += OnOpened;

        UpdateAllDayVisibility();
        UpdateColorSwatch();
        UpdateHeader();
        UpdateDeleteVisibility();
        LoadNotesIntoEditor();
    }

    /// <summary>The editor is a right-side drawer, so it floats above the content with a shadow.</summary>
    private void ApplyDrawerShadow()
    {
        EditorRoot.Shadow = _drawerShadow;
        EditorRoot.Translation = new System.Numerics.Vector3(0, 0, 32);
    }

    private void ApplyDateFormat()
    {
        StartDatePicker.DateFormat = DateFormatPattern;
        EndDatePicker.DateFormat = DateFormatPattern;
        AllDayStartDatePicker.DateFormat = DateFormatPattern;
        AllDayEndDatePicker.DateFormat = DateFormatPattern;
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

    /// <summary>Refocuses the title box when the editor is re-opened while already visible.</summary>
    public void FocusTitle() => TitleBox.Focus(FocusState.Programmatic);

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

    private async void OnSaveTemplateClick(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            Header = "Template name",
            PlaceholderText = "Gym 1h",
            Text = ViewModel.Title,
        };

        var dialog = new ContentDialog
        {
            Title = "Save as template",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await _dialogs.ShowAsync(dialog) != ContentDialogResult.Primary ||
            string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return;
        }

        await ViewModel.SaveAsTemplateAsync(nameBox.Text);
    }

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
}
