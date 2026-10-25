using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.App.Services;
using E2E.Calendar.Core.Models;

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
    private bool _isPremium;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private string _lastDeletedEventId = string.Empty;

    [ObservableProperty]
    private string _selectedTargetKey = string.Empty;

    public ObservableCollection<CalendarListItem> Calendars { get; } = new();

    public ObservableCollection<NavigationListItem> ReminderLists { get; } = new();

    public ObservableCollection<NavigationListItem> TaskLists { get; } = new();

    public MainWindowViewModel()
    {
        ReminderLists.Add(new NavigationListItem { Label = "Today", Glyph = CalendarGlyphs.Today, TargetKey = NavigationKeys.Reminders, TargetParameter = "Reminders|Today" });
        ReminderLists.Add(new NavigationListItem { Label = "Scheduled", Glyph = CalendarGlyphs.Scheduled, TargetKey = NavigationKeys.Reminders, TargetParameter = "Reminders|Scheduled" });
        ReminderLists.Add(new NavigationListItem { Label = "Overdue", Glyph = CalendarGlyphs.Overdue, TargetKey = NavigationKeys.Reminders, TargetParameter = "Reminders|Overdue" });
        ReminderLists.Add(new NavigationListItem { Label = "Completed", Glyph = CalendarGlyphs.Completed, TargetKey = NavigationKeys.Reminders, TargetParameter = "Reminders|Completed" });
        ReminderLists.Add(new NavigationListItem { Label = "Flagged", Glyph = CalendarGlyphs.Flagged, TargetKey = NavigationKeys.Reminders, TargetParameter = "Reminders|Flagged" });

        TaskLists.Add(new NavigationListItem { Label = "Inbox", Glyph = CalendarGlyphs.Inbox, TargetKey = NavigationKeys.Tasks, TargetParameter = "Tasks|Inbox" });
        TaskLists.Add(new NavigationListItem { Label = "Work", Glyph = CalendarGlyphs.Work, TargetKey = NavigationKeys.Tasks, TargetParameter = "Tasks|Work" });
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

    public void SelectTarget(NavigationListItem selectedItem)
    {
        SelectedTargetKey = selectedItem.TargetParameter ?? selectedItem.TargetKey;

        foreach (var item in ReminderLists.Concat(TaskLists))
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
        }
    }

    partial void OnLastDeletedEventIdChanged(string value)
    {
        OnPropertyChanged(nameof(CanUndoLastDelete));
    }
}
