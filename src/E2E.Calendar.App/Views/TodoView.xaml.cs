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

        HeadingText.Text = filter;
        HintText.Text = TodoService.IsTaskList(filter)
            ? "Tasks in this list. New items are added here."
            : "Reminders and tasks that match. New items are added as reminders.";

        try
        {
            var rows = await _todo.LoadAsync(filter);

            if (version != _loadVersion)
            {
                return;
            }

            RowsHost.Children.Clear();

            foreach (var row in rows)
            {
                RowsHost.Children.Add(BuildRow(row));
            }

            EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            HintText.Text = "Could not load this list: " + exception.Message;
        }
    }

    private FrameworkElement BuildRow(TodoRow row)
    {
        // Rows are flat with a subtle 1px divider instead of a card per item.
        var outer = new Border
        {
            BorderBrush = ThemeBrushes.Get(this, "GridLine"),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        var grid = new Grid
        {
            ColumnSpacing = 8,
            MinHeight = Tokens.Double("TodoRowHeight", 44d),
            Padding = new Thickness(4, 0, 4, 0),
            Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
        };

        outer.Child = grid;

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

        var due = new TextBlock
        {
            Text = row.DueText,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = ThemeBrushes.Get(this, row.IsOverdue ? "App.DangerBrush" : "App.TextSecondaryBrush"),
        };
        Grid.SetColumn(due, 2);
        grid.Children.Add(due);

        var list = new TextBlock
        {
            Text = row.ListName,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = ThemeBrushes.Get(this, "App.TextTertiaryBrush"),
        };
        Grid.SetColumn(list, 3);
        grid.Children.Add(list);

        var flag = new Button
        {
            Content = new FontIcon { Glyph = "\uE734", FontSize = Tokens.Double("IconSizeMedium", 14d) },
            Padding = new Thickness(6),
            Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
            BorderThickness = new Thickness(0),
            Foreground = ThemeBrushes.Get(this, row.IsFlagged ? "App.WarningBrush" : "App.TextTertiaryBrush"),
        };
        AutomationProperties.SetName(flag, "Flag " + row.Title);
        ToolTipService.SetToolTip(flag, row.Kind == TodoKind.Reminder ? "Flag" : "High priority");
        flag.Click += async (_, _) => await _todo.ToggleFlagAsync(row);
        Grid.SetColumn(flag, 4);
        grid.Children.Add(flag);

        var delete = new Button
        {
            Content = new FontIcon { Glyph = "\uE74D", FontSize = Tokens.Double("IconSizeMedium", 14d) },
            Padding = new Thickness(6),
            Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
            BorderThickness = new Thickness(0),
            Foreground = ThemeBrushes.Get(this, "App.TextTertiaryBrush"),
        };
        AutomationProperties.SetName(delete, "Delete " + row.Title);
        ToolTipService.SetToolTip(delete, "Delete");
        delete.Click += async (_, _) => await _todo.DeleteAsync(row);
        Grid.SetColumn(delete, 5);
        grid.Children.Add(delete);

        if (row.Kind == TodoKind.Task)
        {
            var subtask = new Button
            {
                Content = new FontIcon { Glyph = "\uE710", FontSize = Tokens.Double("IconSizeMedium", 14d) },
                Padding = new Thickness(6),
                Background = ThemeBrushes.Get(this, "App.TransparentBrush"),
                BorderThickness = new Thickness(0),
                Foreground = ThemeBrushes.Get(this, "App.TextTertiaryBrush"),
            };
            AutomationProperties.SetName(subtask, "Add sub-task to " + row.Title);
            ToolTipService.SetToolTip(subtask, "Add sub-task");
            subtask.Click += async (_, _) => await AddSubtaskAsync(row);
            Grid.SetColumn(subtask, 5);
            Grid.SetColumn(delete, 6);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(subtask);
        }

        return outer;
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
        var text = AddBox.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            var added = await _todo.AddAsync(text, Filter);

            if (added)
            {
                AddBox.Text = string.Empty;
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
