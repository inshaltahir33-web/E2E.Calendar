namespace E2E.Calendar.Core.Abstractions;

using E2E.Calendar.Core.Models;

public interface ICalendarRepository
{
    Task<IReadOnlyList<Calendar>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Calendar?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Calendar calendar, CancellationToken cancellationToken = default);
}
