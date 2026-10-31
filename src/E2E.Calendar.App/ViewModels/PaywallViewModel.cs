using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using E2E.Calendar.App.Services;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;

namespace E2E.Calendar.App.ViewModels;

public sealed partial class PaywallViewModel : ObservableObject
{
    private readonly IEntitlementService _entitlements;

    public PaywallViewModel(IEntitlementService entitlements)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        _entitlements = entitlements;
    }

    public ObservableCollection<PaywallPlan> Plans { get; } = new();

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsStoreAvailable => _entitlements.IsStoreAvailable;

    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            var snapshot = await _entitlements.RefreshAsync();
            Status = EntitlementText.Describe(snapshot, _entitlements.IsStoreAvailable);
            await ReloadPlansAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> PurchaseAsync(PaywallPlan plan)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;

        try
        {
            var result = await _entitlements.PurchaseAsync(plan.ProductId);
            Message = result.Message ?? PurchaseMessage(result.Status);

            if (result.Entitlement.IsPremium)
            {
                Status = EntitlementText.Describe(result.Entitlement, _entitlements.IsStoreAvailable);
                await ReloadPlansAsync();
            }

            return result.Entitlement.IsPremium;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RestoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _entitlements.RestoreAsync();
            Message = result.Message ?? "Purchases restored.";
            Status = EntitlementText.Describe(result.Entitlement, _entitlements.IsStoreAvailable);
            await ReloadPlansAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadPlansAsync()
    {
        var products = await _entitlements.GetProductsAsync();

        Plans.Clear();

        foreach (var product in products)
        {
            Plans.Add(new PaywallPlan(product));
        }

        if (products.Count == 0)
        {
            Message ??= "Premium plans could not be loaded from the Store.";
        }
    }

    private static string PurchaseMessage(PurchaseStatus status) => status switch
    {
        PurchaseStatus.Succeeded => "Premium is active.",
        PurchaseStatus.AlreadyOwned => "You already own this plan.",
        PurchaseStatus.Canceled => "Purchase canceled.",
        _ => "The purchase could not be completed.",
    };
}
