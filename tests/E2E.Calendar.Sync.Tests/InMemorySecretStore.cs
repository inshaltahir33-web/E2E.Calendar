using E2E.Calendar.Core.Abstractions;

// Kept in the test project only. The production project (E2E.Calendar.Sync) no longer
// contains this type. Import via E2E.Calendar.Sync.Tests namespace for test use only.
namespace E2E.Calendar.Sync.Tests;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
