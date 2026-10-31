using System;
using System.Collections.Generic;
using System.ComponentModel;
using E2E.Calendar.App.Services;
using E2E.Calendar.App.Utilities;
using E2E.Calendar.Core.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace E2E.Calendar.App.Views;

/// <summary>
/// Reminders and tasks page: Today, Scheduled, Overdue, Completed, Flagged and the task lists (Inbox, Work).
/// Shows real data, lets the user add, complete, flag and delete items, and keeps sidebar counts in sync.
/// </summary>
public sealed partial class TodoView : Page
{
    private readonly TodoService _todo;
    private readonly IShellState _shell;
    private int _loadVersion;
    private string? _configuredFilter;

    public TodoView(TodoService todo, IShellState shell)
    {
        ArgumentNullException.ThrowIfNull(todo);
        ArgumentNullException.ThrowIfNull(shell);

        _todo = todo;
        _shell = shell;

        InitializeComponent();

        _shell.PropertyChanged += OnShellPropertyChanged;
        _todo.Changed += OnTodoChanged;
    }

    private string Filter => string.IsNullOrWhiteSpace(_shell.TodoFilter) ? TodoService.Today : _shell.TodoFilter!;

    private void OnLoaded(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    /// <summary>Keeps the content column at 880px max and centered; a centered Grid alone would shrink to its text.</summary>
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (RootGrid is null)
        {
            return;
        }

        var target = Math.Min(e.NewSize.Width, 880d);

        if (!double.IsNaN(RootGrid.Width) && Math.Abs(RootGrid.Width - target) < 0.5)
        {
            return;
        }

        RootGrid.Width = target;
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IShellState.TodoFilter) && _shell.Mode == ShellMode.Todo)
        {
            _ = ReloadAsync();
        }
    }

    private void OnTodoChanged(object? sender, EventArgs e) => _ = ReloadAsync();

    private async System.Threading.Tasks.Task ReloadAsync()
    {
        var version = ++_loadVersion;
        var filter = Filter;

        if (!string.Equals(_configuredFilter, filter, StringComparison.Ordinal))
        {
            ConfigureAddArea(filter);
            _configuredFilter = filter;
        }

        var canAdd = TodoService.CanAdd(filter);

        HeadingText.Text = filter;
        HintText.Visibility = canAdd ? Visibility.Visible : Visibility.Collapsed;
        HintText.Text = TodoService.IsTaskList(filter)
            ? "Tasks in this list. New items are added here."
            : "Reminders and tasks that match. New items are added as reminders.";
        EmptyText.Text = filter switch
        {
            TodoService.Today => "Nothing left for today.",
            TodoService.Scheduled => "Nothing scheduled. Add one above.",
            TodoService.Overdue => "Nothing overdue.",
            TodoService.Completed => "Nothing completed yet.",
            TodoService.Flagged => "Nothing flagged.",
            TodoService.Inbox => "Inbox is empty. Add a task above.",
            _ => "This list is empty.",
        };

        try
        {
            var groups = await _todo.LoadGroupedAsync(filter);

            if (version != _loadVersion)
            {
                return;
            }

            RowsHost.Children.Clear();
            var count = 0;

            foreach (var group in groups)
            {
                if (group.Header is not null)
                {
                    RowsHost.Children.Add(BuildGroupHeader(group.Header));
                }

                foreach (var row in group.Rows)
                {
                    RowsHost.Children.Add(BuildRow(row));
                    count++;
                }
            }

            EmptyText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            HintText.Text = "Could not load this list: " + exception.Message;
        }
    }

    /// <summary>The add row follows the page: reminder chips, a task due date, or no box at all.</summary>
    private void ConfigureAddArea(string filter)
    {
        var canAdd = TodoService.CanAdd(filter);

        AddArea.Visibility = canAdd ? Visibility.Visible : Visibility.Collapsed;
        NoAddText.Visibility = canAdd ? Visibility.Collapsed : Visibility.Visible;

        if (!canAdd)
        {
            NoAddText.Text = filter switch
            {
                TodoService.Overdue => "Overdue items are read-only. Complete or reschedule them from Today or Scheduled.",
                TodoService.Completed => "Completed items are read-only.",
                _ => "Flagged items are read-only. Flag or unflag them from another list.",
            };

            AddBox.Text = string.Empty;
            return;
        }

        var isTaskList = TodoService.IsTaskList(filter);

        AddBox.PlaceholderText = isTaskList ? "Add a task…" : "Add a reminder…";
        AutomationProperties.SetName(AddBox, isTaskList ? "New task title" : "New reminder title");

        DueDateChip.Visibility = Visibility.Visible;
        DueTimeChip.Visibility = isTaskList ? Visibility.Collapsed : Visibility.Visible;

        ResetChips();
    }

    private void ResetChips()
    {
        if (TodoService.IsTaskList(Filter))
        {
            SetChips(null, null);
            return;
        }

        var (date, time) = _todo.GetAddDefault(Filter);
        SetChips(date, time);
    }

    private void SetChips(DateOnly? date, TimeOnly? time)
    {
        DueDateChip.Date = date is { } day
            ? new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue))
            : null;

        if (time is { } clock)
        {
            DueTimeChip.Time = clock.ToTimeSpan();
        }
    }

    /// <summary>
    /// Typed date/time words only fill the chips; the words are removed from the title
    /// when the item is added. Unrecognised text leaves the chips untouched.
    /// </summary>
    private void OnAddTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!TodoService.CanAdd(Filter))
        {
            return;
        }

        var preview = _todo.Preview(AddBox.Text);

        if (preview.Date is not { } date)
        {
            return;
        }

        DueDateChip.Date = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));

        if (!TodoService.IsTaskList(Filter) && preview.Time is { } time)
        {
            DueTimeChip.Time = time.ToTimeSpan();
        }
    }

    private FrameworkElement BuildRow(TodoRow row)
    {
        // Rows are flat with a subtle 1px divider instead of a card per item.
        var outer = new Border
        {
            BorderBrush = ThemeBrushes.Get(this, "GridLine"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
        };

        var grid = new Grid
        {
            ColumnSpacing = 8,
            Height = Tokens.Double("TodoRowHeight", 48d),
            Padding = new Thickness(4, 0, 4, 0),
            Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
        };

        outer.Child = grid;

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox
        {
            IsChecked = row.IsDone,
            MinWidth = 0,
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(check, "Done: " + row.Title);
        check.Click += async (_, _) => await _todo.SetDoneAsync(row, check.IsChecked == true);
        grid.Children.Add(check);

        var title = new TextBlock
        {
            Text = row.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = ThemeBrushes.Get(this, row.IsDone ? "App.TextTertiaryBrush" : "App.TextPrimaryBrush"),
            Opacity = row.IsDone ? 0.6 : 1,
        };

        if (row.IsDone)
        {
            title.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
        }
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var list = new TextBlock
        {
            Text = row.ListName,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = ThemeBrushes.Get(this, "App.TextTertiaryBrush"),
            Visibility = string.Equals(row.ListName, Filter, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible,
        };
        Grid.SetColumn(list, 2);
        grid.Children.Add(list);

        // The right edge shows the due chip; hovering the row swaps it for the actions.
        var hasDue = !string.IsNullOrEmpty(row.DueText);

        var right = new Grid
        {
            MinWidth = 132,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(right, 3);
        grid.Children.Add(right);

        var dueChip = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(6),
            Background = ThemeBrushes.Get(this, "App.CardBackgroundBrush"),
            BorderBrush = ThemeBrushes.Get(this, row.IsOverdue ? "App.DangerBrush" : "App.DividerBrush"),
            BorderThickness = new Thickness(1),
            Visibility = hasDue ? Visibility.Visible : Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = row.DueText,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = ThemeBrushes.Get(this, row.IsOverdue ? "App.DangerBrush" : "App.TextSecondaryBrush"),
            },
        };
        right.Children.Add(dueChip);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        right.Children.Add(actions);

        actions.Children.Add(CreateRowButton(
            "\uE734",
            row.Kind == TodoKind.Reminder ? "Flag" : "High priority",
            "Flag " + row.Title,
            row.IsFlagged ? "App.WarningBrush" : "App.TextTertiaryBrush",
            () => _todo.ToggleFlagAsync(row)));

        actions.Children.Add(CreateRowButton(
            "\uE74D",
            "Delete",
            "Delete " + row.Title,
            "App.TextTertiaryBrush",
            () => _todo.DeleteAsync(row)));

        if (row.Kind == TodoKind.Task)
        {
            actions.Children.Add(CreateRowButton(
                "\uE710",
                "Add sub-task",
                "Add sub-task to " + row.Title,
                "App.TextTertiaryBrush",
                () => AddSubtaskAsync(row)));
        }

        outer.PointerEntered += (_, _) =>
        {
            grid.Background = ThemeBrushes.Get(this, "App.HoverBrush");
            dueChip.Visibility = Visibility.Collapsed;
            actions.Visibility = Visibility.Visible;
        };

        outer.PointerExited += (_, _) =>
        {
            grid.Background = ThemeBrushes.Get(this, "App.TransparentBrush");
            actions.Visibility = Visibility.Collapsed;
            dueChip.Visibility = hasDue ? Visibility.Visible : Visibility.Collapsed;
        };

        return outer;
    }

    private FrameworkElement BuildGroupHeader(string header)
    {
        var text = new TextBlock
        {
            Text = header,
            Margin = new Thickness(4, 12, 4, 4),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.Get(this, "App.TextSecondaryBrush"),
        };
        return text;
    }

    private Button CreateRowButton(string glyph, string tooltip, string automationName, string brushKey, Func<Task> action)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = Tokens.Double("IconSizeMedium", 14d) },
            Width = 28,
            Height = 28,
            MinWidth = 0,
            Padding = new Thickness(0),
            Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
            BorderThickness = new Thickness(0),
            Foreground = ThemeBrushes.Get(this, brushKey),
        };
        AutomationProperties.SetName(button, automationName);
        ToolTipService.SetToolTip(button, tooltip);
        button.Click += async (_, _) => await action();
        return button;
    }

    private async Task AddSubtaskAsync(TodoRow parent)
    {
        var box = new TextBox { PlaceholderText = "Sub-task title" };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Title = "Add sub-task",
            Content = box,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary ||
            string.IsNullOrWhiteSpace(box.Text))
        {
            return;
        }

        await _todo.AddSubtaskAsync(parent, box.Text);
    }

    private void OnAddKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = AddAsync();
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => _ = AddAsync();

    private async System.Threading.Tasks.Task AddAsync()
    {
        if (!TodoService.CanAdd(Filter))
        {
            return;
        }

        var text = AddBox.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var preview = _todo.Preview(text);
        var isTaskList = TodoService.IsTaskList(Filter);
        DateOnly? dueDate = DueDateChip.Date is { } picked
            ? DateOnly.FromDateTime(picked.Date)
            : null;
        TimeOnly? dueTime = isTaskList ? null : TimeOnly.FromTimeSpan(DueTimeChip.Time);

        try
        {
            var added = await _todo.AddAsync(text, Filter, preview.Title, dueDate, dueTime);

            if (added)
            {
                AddBox.Text = string.Empty;
                ResetChips();
            }
            else
            {
                HintText.Text = $"The list '{Filter}' does not exist yet, so nothing was added.";
            }
        }
        catch (Exception exception)
        {
            HintText.Text = "Could not add: " + exception.Message;
        }
    }
}
