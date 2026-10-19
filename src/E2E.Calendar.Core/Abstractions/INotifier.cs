using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Abstractions;

public interface INotifier
{
    Task ShowAsync(NotificationRequest notification, CancellationToken cancellationToken = default);

    Task ScheduleAsync(NotificationRequest notification, CancellationToken cancellationToken = default);

    Task CancelAsync(string tag, CancellationToken cancellationToken = default);
}
