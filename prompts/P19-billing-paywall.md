Implement premium per spec section 8 and docs/02 §9.
1. IEntitlementService final implementation using Windows.Services.Store (StoreContext.GetAppLicenseAsync, GetAssociatedStoreProductsAsync): Monthly + Yearly subscriptions (7-day trial on Monthly), Lifetime durable add-on; cache result with expiry; restore purchases; offline grace (7 days). DEBUG-only fake entitlement switch in Settings > Developer.
2. Central Feature enum + limits table from spec 8.1 (accounts 1, alarms 3, task lists 1, templates 2, year/multi-day, calendar sets, strip, habit, weather, digest, themes, free slot, Hello). Enforce in Services (not only UI), returning a PremiumRequired result.
3. Paywall dialog: 3 cards (Monthly / Yearly highlighted "Save 44%" / Lifetime), feature list, Restore, Skip; shown when tapping a locked item and once after onboarding. Crown badge on locked items (never hidden).
4. Calendar sets/profiles: saved visibility presets; premium accent presets (Appearance) via swapping SystemAccent tokens only (do NOT change docs/11 values).
Tests for gating matrix. Commit "feat: premium billing and paywall".
