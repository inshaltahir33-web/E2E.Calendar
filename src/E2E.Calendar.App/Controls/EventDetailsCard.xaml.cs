using System.Globalization;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace E2E.Calendar.App.Controls;

public sealed partial class EventDetailsCard : UserControl
{
    private Uri? _meetingUri;

    public EventDetailsCard()
    {
        InitializeComponent();
    }

    public event EventHandler? EditRequested;

    public event EventHandler? DeleteRequested;

    public void Populate(CalendarItem item, CalendarEvent? details)
    {
        TitleText.Text = item.Title;
        TimeText.Text = FormatWhen(item);

        var calendarName = item.CalendarName;
        CalendarText.Text = calendarName ?? string.Empty;
        CalendarText.Visibility = string.IsNullOrWhiteSpace(calendarName) ? Visibility.Collapsed : Visibility.Visible;
        ColorDot.Background = EventColorService.GetChipBrushes(item.Color, ActualTheme).Stripe;

        SetLocation(string.IsNullOrWhiteSpace(item.Location) ? details?.Location : item.Location);
        SetMeeting(details?.MeetingUrl);

        var notes = details?.Notes;
        NotesText.Text = notes ?? string.Empty;
        NotesHost.Visibility = string.IsNullOrWhiteSpace(notes) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetLocation(string? location)
    {
        var hasLocation = !string.IsNullOrWhiteSpace(location);
        LocationRow.Visibility = hasLocation ? Visibility.Visible : Visibility.Collapsed;
        LocationText.Text = location ?? string.Empty;
        MapLink.NavigateUri = hasLocation
            ? new Uri($"https://www.bing.com/maps?q={Uri.EscapeDataString(location!.Trim())}")
            : null;
    }

    private void SetMeeting(string? meetingUrl)
    {
        var isValid = Uri.TryCreate(meetingUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        _meetingUri = isValid ? uri : null;
        MeetingRow.Visibility = isValid ? Visibility.Visible : Visibility.Collapsed;
        MeetingText.Text = isValid ? meetingUrl! : string.Empty;
        ToolTipService.SetToolTip(MeetingText, isValid ? meetingUrl : null);
    }

    private async void OnJoinClick(object sender, RoutedEventArgs e)
    {
        if (_meetingUri is not null)
        {
            await Launcher.LaunchUriAsync(_meetingUri);
        }
    }

    private void OnEditClick(object sender, RoutedEventArgs e) => EditRequested?.Invoke(this, EventArgs.Empty);

    private void OnDeleteClick(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(this, EventArgs.Empty);

    private static string FormatWhen(CalendarItem item)
    {
        var culture = CultureInfo.CurrentCulture;
        var start = item.StartUtc.ToLocalTime();
        var end = item.EndUtc.ToLocalTime();

        if (item.AllDay || !item.HasTime)
        {
            return start.Date == end.Date
                ? $"{start.ToString("dddd, d MMMM yyyy", culture)} \u00B7 All day"
                : $"{start.ToString("ddd, d MMM", culture)} \u2013 {end.ToString("ddd, d MMM yyyy", culture)} \u00B7 All day";
        }

        return start.Date == end.Date
            ? $"{start.ToString("dddd, d MMMM yyyy", culture)} \u00B7 {start.ToString("t", culture)} \u2013 {end.ToString("t", culture)}"
            : $"{start.ToString("ddd d MMM yyyy, t", culture)} \u2013 {end.ToString("ddd d MMM yyyy, t", culture)}";
    }
}
