using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(Account account, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
