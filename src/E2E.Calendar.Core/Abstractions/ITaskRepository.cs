using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskItem>> GetByListAsync(string listId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskList>> GetListsAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(TaskItem task, CancellationToken cancellationToken = default);
}
