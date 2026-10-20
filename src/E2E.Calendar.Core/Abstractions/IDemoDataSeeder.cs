namespace E2E.Calendar.Core.Abstractions;

public interface IDemoDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
