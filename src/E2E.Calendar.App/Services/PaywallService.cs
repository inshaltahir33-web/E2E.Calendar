using E2E.Calendar.App.Views;
using E2E.Calendar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Calendar.App.Services;

public interface IPaywallService
{
    /// <summary>Shows the premium plans dialog. Returns true when premium is active afterwards.</summary>
    Task<bool> ShowAsync();
}

public sealed class PaywallService : IPaywallService
{
    private readonly IServiceProvider _services;
    private readonly IDialogService _dialogs;
    private readonly IEntitlementService _entitlements;

    public PaywallService(IServiceProvider services, IDialogService dialogs, IEntitlementService entitlements)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(entitlements);

        _services = services;
        _dialogs = dialogs;
        _entitlements = entitlements;
    }

    public async Task<bool> ShowAsync()
    {
        await _dialogs.ShowAsync(_services.GetRequiredService<PaywallDialog>());

        return _entitlements.Current.IsPremium;
    }
}
