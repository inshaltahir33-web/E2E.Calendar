using E2E.Calendar.Core.Entitlements;
using Microsoft.UI.Xaml;

namespace E2E.Calendar.App.ViewModels;

public sealed class PaywallPlan
{
    public PaywallPlan(StoreProductInfo product)
    {
        ProductId = product.ProductId;
        Title = product.Title;
        PriceText = product.PriceText;
        Badge = product.ProductId == StoreProductIds.Yearly ? "Save 44%" : null;
        Detail = product.TrialDays > 0
            ? $"{product.TrialDays}-day free trial"
            : product.IsSubscription ? "Billed automatically" : "One-time purchase";
        IsOwned = product.IsOwned;
        BuyLabel = IsOwned ? "Current plan" : "Buy";
        BuyAutomationName = IsOwned ? $"{Title} is your current plan" : $"Buy {Title}";
    }

    public string ProductId { get; }

    public string Title { get; }

    public string PriceText { get; }

    public string? Badge { get; }

    public string Detail { get; }

    public bool IsOwned { get; }

    public string BuyLabel { get; }

    public string BuyAutomationName { get; }

    public Visibility BadgeVisibility => string.IsNullOrEmpty(Badge) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility OwnedVisibility => IsOwned ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BuyVisibility => IsOwned ? Visibility.Collapsed : Visibility.Visible;
}
