using E2E.Calendar.App.Controls;
using E2E.Calendar.App.ViewModels;

namespace E2E.Calendar.App.Views;

internal static class TimeGridInteractions
{
    public static void Wire(TimeGrid grid, EventEditorViewModel editor, Func<Task> refresh)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(refresh);

        grid.ItemActivated += async (_, args) => await editor.OpenAsync(args.Item);
        grid.SlotCreated += async (_, args) => await editor.OpenNewAsync(args.StartUtc, args.EndUtc);

        grid.ItemMoved += async (_, args) =>
        {
            await editor.UpdateTimesAsync(args.Item, args.Item.StartUtc, args.Item.EndUtc);
            await refresh();
        };

        grid.ItemResized += async (_, args) =>
        {
            await editor.UpdateTimesAsync(args.Item, args.Item.StartUtc, args.Item.EndUtc);
            await refresh();
        };

        grid.ItemCopied += async (_, args) =>
        {
            await editor.CopyAsync(args.Item, args.Item.StartUtc, args.Item.EndUtc);
            await refresh();
        };
    }
}
