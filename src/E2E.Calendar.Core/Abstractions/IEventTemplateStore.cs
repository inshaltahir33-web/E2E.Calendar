using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

/// <summary>Event templates saved by the user from the event editor.</summary>
public interface IEventTemplateStore
{
    Task<IReadOnlyList<EventTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(EventTemplate template, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
