using E2E.Calendar.Core.Models;

using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Sync;

public interface ISyncProvider
{
    E2E.Calendar.Core.Models.Provider Provider { get; }

    Task<Account> SignInAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<E2E.Calendar.Core.Models.Calendar>> ListCalendarsAsync(Account account, CancellationToken cancellationToken = default);

    Task<PullResult> PullAsync(E2E.Calendar.Core.Models.Calendar calendar, CancellationToken cancellationToken = default);

    Task PushAsync(E2E.Calendar.Core.Models.Calendar calendar, OutboxItem item, CancellationToken cancellationToken = default);
}
