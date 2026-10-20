using E2E.Calendar.App.Controls;
using E2E.Calendar.App.ViewModels;
using E2E.Calendar.Core.Abstractions;

namespace E2E.Calendar.App.Views;

internal static class TimeGridInteractions
{
    public static void Wire(
        TimeGrid grid,
        EventEditorViewModel editor,
        IEventRepository events,
        IEventService eventService,
        Func<Task> refresh)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventService);
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

        grid.ItemActionRequested += async (_, args) =>
        {
            switch (args.Action)
            {
                case TimeGridItemAction.Edit:
                    await editor.OpenAsync(args.Item);
                    break;

                case TimeGridItemAction.Duplicate:
                    await editor.CopyAsync(args.Item, args.Item.StartUtc, args.Item.EndUtc);
                    await refresh();
                    break;

                case TimeGridItemAction.Delete:
                    await eventService.SoftDeleteAsync(args.Item.Id);
                    await refresh();
                    break;

                case TimeGridItemAction.ChangeColor:
                    var source = await events.GetByIdAsync(args.Item.Id);

                    if (source is not null)
                    {
                        await eventService.UpdateAsync(source with { Color = args.Item.Color });
                        await refresh();
                    }

                    break;
            }
        };
    }
}
