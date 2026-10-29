using E2E.Calendar.App.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace E2E.Calendar.App.Services;

/// <summary>
/// The single place where the app shows <see cref="ContentDialog"/>s. It assigns the
/// <see cref="XamlRoot"/>, prevents a second dialog from stacking on top of an open one,
/// and keeps dialog lifecycle consistent. Views must not call <c>ContentDialog.ShowAsync</c>
/// directly.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a dialog. The <see cref="XamlRoot"/> is filled in when the dialog has none.
    /// Returns <see cref="ContentDialogResult.None"/> without showing anything when no
    /// root is available yet or when another dialog is already open.
    /// </summary>
    Task<ContentDialogResult> ShowAsync(ContentDialog dialog);
}

public sealed class DialogService : IDialogService
{
    private readonly Func<XamlRoot?> _xamlRootProvider;
    private readonly ILogger<DialogService> _logger;
    private int _isOpen;

    public DialogService(Func<XamlRoot?> xamlRootProvider, ILogger<DialogService> logger)
    {
        ArgumentNullException.ThrowIfNull(xamlRootProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _xamlRootProvider = xamlRootProvider;
        _logger = logger;
    }

    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        dialog.XamlRoot ??= _xamlRootProvider();

        if (dialog.XamlRoot is null)
        {
            _logger.LogWarning("Dialog '{Title}' was not shown: no XamlRoot is available yet.", dialog.Title);
            return ContentDialogResult.None;
        }

        if (Interlocked.CompareExchange(ref _isOpen, 1, 0) != 0)
        {
            _logger.LogWarning("Dialog '{Title}' was not shown: another dialog is already open.", dialog.Title);
            return ContentDialogResult.None;
        }

        // Remember who opened the dialog so focus goes back there once it closes.
        var opener = OverlayFocus.Capture(dialog.XamlRoot);

        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _isOpen, 0);
            OverlayFocus.Restore(opener);
        }
    }
}
