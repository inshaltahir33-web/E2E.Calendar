using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Entitlements;

namespace E2E.Calendar.Core.Services;

public sealed class FreeEntitlementService : IEntitlementService
{
    public bool Can(Feature feature) => true;

    public int LimitFor(Feature feature) => -1;
}
