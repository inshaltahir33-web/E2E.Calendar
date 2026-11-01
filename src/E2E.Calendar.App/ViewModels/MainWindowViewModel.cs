using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.App.Services;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using CalendarModel = E2E.Calendar.Core.Models.Calendar;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "E2E Calendar";

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _syncStatus = "Not synced yet";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private string _lastDeletedEventId = string.Empty;

    [ObservableProperty]
    private string _selectedTargetKey = string.Empty;

    public ObservableCollection<CalendarListItem> Calendars { get; } = new();

    /// <summary>The shared calendar list, bound directly by calendar pickers such as search filters.</summary>
    public ReadOnlyObservableCollection<CalendarModel> SharedCalendars { get; }

    public ObservableCollection<NavigationListItem> ReminderLists { get; } = new();

    public ObservableCollection<NavigationListItem> TaskLists { get; } = new();

    /// <summary>Distinct tags found on stored events. Populated by the window from the event store.</summary>
    public ObservableCollection<NavigationListItem> Tags { get; } = new();

    public MainWindowViewModel(CalendarService calendars)
    {
        ArgumentNullException.ThrowIfNull(calendars);

        SharedCalendars = calendars.Calendars;

        ReminderLists.Add(new NavigationListItem { Label = TodoService.Today, Glyph = CalendarGlyphs.Today, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = TodoService.Next7Days, Glyph = CalendarGlyphs.Upcoming, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = TodoService.Scheduled, Glyph = CalendarGlyphs.Scheduled, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = TodoService.NoDate, Glyph = CalendarGlyphs.NoDate, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = TodoService.Overdue, Glyph = CalendarGlyphs.Overdue, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = "Completed", Glyph = CalendarGlyphs.Completed, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = "Flagged", Glyph = CalendarGlyphs.Flagged, TargetKey = NavigationKeys.Reminders });

        TaskLists.Add(new NavigationListItem { Label = "Inbox", Glyph = CalendarGlyphs.Inbox, TargetKey = NavigationKeys.Tasks });
    }

    /// <summary>Rebuilds the LISTS section from stored lists, keeping Inbox first and the selection.</summary>
    public void SetTaskLists(IReadOnlyList<TaskList> lists)
    {
        var selected = TaskLists.FirstOrDefault(item => item.IsSelected);

        TaskLists.Clear();
        TaskLists.Add(new NavigationListItem
        {
            Label = TodoService.Inbox,
            Glyph = CalendarGlyphs.Inbox,
            TargetKey = NavigationKeys.Tasks,
        });

        foreach (var list in lists)
        {
            TaskLists.Add(new NavigationListItem
            {
                Label = list.Name,
                Glyph = CalendarGlyphs.List,
                TargetKey = NavigationKeys.Tasks,
                ListId = list.Id,
                IsUserList = true,
            });
        }

        if (selected is not null)
        {
            SelectItem(TaskLists.FirstOrDefault(item =>
                string.Equals(item.ListId, selected.ListId, StringComparison.Ordinal)));
        }
    }

    public bool CanUndoLastDelete => !string.IsNullOrEmpty(LastDeletedEventId);

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    public void RecordDeletion(string eventId)
    {
        LastDeletedEventId = eventId;
        OnPropertyChanged(nameof(CanUndoLastDelete));
    }

    /// <summary>Selects exactly one sidebar row by identity (several rows share a TargetKey).</summary>
    public void SelectItem(NavigationListItem? selected)
    {
        SelectedTargetKey = selected?.TargetKey ?? string.Empty;

        foreach (var item in ReminderLists.Concat(TaskLists).Concat(Tags))
        {
            item.IsSelected = ReferenceEquals(item, selected);
        }
    }

    /// <summary>Clears the sidebar selection (used when the user returns to a calendar view).</summary>
    public void ClearNavigationSelection() => SelectItem(null);

    public void SelectTarget(string targetKey)
    {
        SelectItem(ReminderLists.Concat(TaskLists).Concat(Tags).FirstOrDefault(item =>
            string.Equals(item.TargetKey, targetKey, StringComparison.Ordinal)));
    }

    /// <summary>Rebuilds the tag list from the distinct tags stored on events.</summary>
    public void SetTags(IEnumerable<string> tags)
    {
        Tags.Clear();

        foreach (var tag in tags)
        {
            Tags.Add(new NavigationListItem
            {
                Label = tag,
                Glyph = CalendarGlyphs.Tag,
                TargetKey = NavigationKeys.Tags,
            });
        }
    }

    partial void OnLastDeletedEventIdChanged(string value)
    {
        OnPropertyChanged(nameof(CanUndoLastDelete));
    }
}
