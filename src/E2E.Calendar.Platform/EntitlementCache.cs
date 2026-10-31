using System.Text.Json;
using E2E.Calendar.Core.Entitlements;

namespace E2E.Calendar.Platform;

/// <summary>
/// Stores the last entitlement read from the Store so an offline launch keeps
/// premium features instead of dropping to the free tier. Best effort only:
/// a missing or corrupt cache just means "unknown".
/// </summary>
internal static class EntitlementCache
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "E2E.Calendar",
        "entitlement.json");

    public static async Task<EntitlementSnapshot?> LoadAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(CachePath);
            var cached = await JsonSerializer.DeserializeAsync<CachedEntitlement>(stream, Options, cancellationToken);

            if (cached is null)
            {
                return null;
            }

            if (cached.IsPremium && cached.ExpiresUtc is { } expires && expires <= nowUtc)
            {
                return EntitlementSnapshot.Free(nowUtc) with { Source = EntitlementSource.Cache };
            }

            return new EntitlementSnapshot(
                cached.IsPremium,
                cached.Tier,
                EntitlementSource.Cache,
                cached.ExpiresUtc,
                cached.IsTrial,
                cached.CheckedUtc);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task SaveAsync(EntitlementSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);

            await using var stream = File.Create(CachePath);
            await JsonSerializer.SerializeAsync(
                stream,
                new CachedEntitlement(snapshot.IsPremium, snapshot.Tier, snapshot.ExpiresUtc, snapshot.IsTrial, snapshot.CheckedUtc),
                Options,
                cancellationToken);
        }
        catch (Exception)
        {
            // The cache must never break entitlement checks.
        }
    }

    private sealed record CachedEntitlement(
        bool IsPremium,
        EntitlementTier Tier,
        DateTimeOffset? ExpiresUtc,
        bool IsTrial,
        DateTimeOffset CheckedUtc);
}
