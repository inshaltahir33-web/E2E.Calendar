using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class ProductivityAnalyticsService
{
    private readonly IEventRepository _events;

    public ProductivityAnalyticsService(IEventRepository events)
    {
        _events = events;
    }

    public async Task<ProductivityMetrics> GetMetricsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        DateTimeOffset? completedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc), "The end of the range must be after its start.");
        }

        var now = completedAtUtc ?? DateTimeOffset.UtcNow;
        var events = await _events.GetInRangeAsync(null, fromUtc, toUtc, cancellationToken);
        var completed = events
            .Where(item => !item.Deleted && item.EndUtc <= now)
            .ToArray();

        var durations = completed
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Category) ? "Uncategorised" : item.Category.Trim())
            .ToDictionary(
                group => group.Key,
                group => TimeSpan.FromTicks(group.Sum(item => Math.Max(0, (item.EndUtc - item.StartUtc).Ticks))),
                StringComparer.OrdinalIgnoreCase);

        return new ProductivityMetrics
        {
            CompletedEventCount = completed.Length,
            CompletedDuration = TimeSpan.FromTicks(completed.Sum(item => Math.Max(0, (item.EndUtc - item.StartUtc).Ticks))),
            DurationByCategory = durations,
        };
    }
}
