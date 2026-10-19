using E2E.Calendar.Core.Entitlements;

namespace E2E.Calendar.Core.Abstractions;

public interface IEntitlementService
{
    bool Can(Feature feature);

    int LimitFor(Feature feature);
}
