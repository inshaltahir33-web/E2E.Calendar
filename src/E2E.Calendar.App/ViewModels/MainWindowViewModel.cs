using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E2E.Calendar.App.Services;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Abstractions;
using Microsoft.UI.Xaml;

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

    private readonly IAlertRepository _alerts;
    private readonly NavigationService _navigation;

    /// <summary>Distinct tags found on stored events. Populated by the window from the event store.</summary>
    public ObservableCollection<NavigationListItem> Tags { get; } = new();

    public MainWindowViewModel(IAlertRepository alerts, NavigationService navigation)
    {
        _alerts = alerts;
        _navigation = navigation;

        ReminderLists.Add(new NavigationListItem { Label = "Today", Glyph = CalendarGlyphs.Today, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = "Scheduled", Glyph = CalendarGlyphs.Scheduled, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = "Overdue", Glyph = CalendarGlyphs.Overdue, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = "Completed", Glyph = CalendarGlyphs.Completed, TargetKey = NavigationKeys.Reminders });
        ReminderLists.Add(new NavigationListItem { Label = "Flagged", Glyph = CalendarGlyphs.Flagged, TargetKey = NavigationKeys.Reminders });

        TaskLists.Add(new NavigationListItem { Label = "Inbox", Glyph = CalendarGlyphs.Inbox, TargetKey = NavigationKeys.Tasks });
        TaskLists.Add(new NavigationListItem { Label = "Work", Glyph = CalendarGlyphs.Work, TargetKey = NavigationKeys.Tasks });
    }

    /// <summary>Handles snooze protocol from toast action.</summary>
    /// <param name="instanceId">The alert instance ID.</param>
    /// <param name="duration">Snooze duration in minutes, or "tomorrow".

    /// <summary>Handles snooze protocol from toast action.</summary>
/// <param name="instanceId">The alert instance ID.</param>
/// <param name="duration">Snooze duration in minutes, or "tomorrow".</param>
public void HandleSnoozeProtocol(string instanceId, string duration)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;

            if (duration == "tomorrow")
            {
                // Snooze until tomorrow at the same time as the original reminder
                var alertInstance = _alerts.GetInstanceByIdAsync(instanceId).GetAwaiter().GetResult();
                if (alertInstance is not null && alertInstance.State == AlertState.Pending)
                {
                    // Use the occurrence UTC time as the original time reference
                    var originalTime = alertInstance.OccurrenceUtc;
                    // Create updated instance using with expression (init-only properties)
                    var updatedInstance = alertInstance with
                    {
                        State = AlertState.Snoozed,
                        SnoozedUntilUtc = originalTime.Date.AddDays(1).AddMinutes(
                            (originalTime - originalTime.Date).TotalMinutes)
                    };
                    _alerts.UpsertInstanceAsync(updatedInstance).GetAwaiter().GetResult();
                }
            }
            else
            {
                var minutes = int.Parse(duration);
                // Snooze for the specified minutes
                var alertInstance = _alerts.GetInstanceByIdAsync(instanceId).GetAwaiter().GetResult();
                if (alertInstance is not null && alertInstance.State == AlertState.Pending)
                {
                    // Create updated instance using with expression (init-only properties)
                    var updatedInstance = alertInstance with
                    {
                        State = AlertState.Snoozed,
                        SnoozedUntilUtc = now.AddMinutes(minutes)
                    };
                    _alerts.UpsertInstanceAsync(updatedInstance).GetAwaiter().GetResult();
                }
            }

            // Navigate back to the main view
            _navigation.NavigateTo(NavigationKeys.Week);
        }
        catch
        {
            // Log error but don't crash
        }
    }

    /// <summary>Handles done protocol from toast action.</summary>
    /// <param name="instanceId">The alert instance ID.</param>
    public void HandleDoneProtocol(string instanceId)
    {
        try
        {
            var alertInstance = _alerts.GetInstanceByIdAsync(instanceId).GetAwaiter().GetResult();
            if (alertInstance is not null)
            {
                alertInstance = alertInstance with { State = AlertState.Done, FiredUtc = DateTimeOffset.UtcNow };
                _alerts.UpsertInstanceAsync(alertInstance).GetAwaiter().GetResult();
            }

            // Navigate back to the main view
            _navigation.NavigateTo(NavigationKeys.Week);
        }
        catch
        {
            // Log error but don't crash
        }
    }

    /// <summary>Handles open protocol from toast action.</summary>
    /// <param name="instanceId">The alert instance ID.</param>
    public void HandleOpenProtocol(string instanceId)
    {
        try
        {
            var alertInstance = _alerts.GetInstanceByIdAsync(instanceId).GetAwaiter().GetResult();
            if (alertInstance is not null)
            {
                // Navigate to the event or reminder details
                // Determine owner type and navigate accordingly
                var ownerType = alertInstance.OwnerType;
                if (ownerType == OwnerType.Event)
                {
                    // Navigate to the event editor with this event
                    _navigation.NavigateTo(NavigationKeys.EventEditor);
                }
                else if (ownerType == OwnerType.Reminder)
                {
                    // Navigate to the reminder details
                    _navigation.NavigateTo(NavigationKeys.Reminders);
                }
                else if (ownerType == OwnerType.Task)
                {
                    // Navigate to the task details
                    _navigation.NavigateTo(NavigationKeys.Tasks);
                }
            }
        }
        catch
        {
            // Log error but don't crash
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
