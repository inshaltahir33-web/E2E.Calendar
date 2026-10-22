namespace E2E.Calendar.Core.Abstractions;

/// <summary>
/// Key/value access to the <c>settings</c> table.
/// </summary>
public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken cancellationToken = default);
}
