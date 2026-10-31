namespace E2E.Calendar.App.ViewModels;

/// <summary>A single row in the tray flyout's "Today" list.</summary>
public sealed record TrayAgendaItem(string TimeText, string Title, bool IsDone)
{
    public double TitleOpacity => IsDone ? 0.55 : 1.0;
}
