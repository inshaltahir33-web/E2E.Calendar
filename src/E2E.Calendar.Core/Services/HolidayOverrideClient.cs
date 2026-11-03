using System.Net.Http;

namespace E2E.Calendar.Core.Services;

/// <summary>
/// Downloads remote holiday overrides. A blank or placeholder URL is treated as
/// "not configured": no request is made and callers fall back to the bundled ICS data.
/// </summary>
public sealed class HolidayOverrideClient
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;

    public HolidayOverrideClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public static bool IsConfigured(string? url) => TryGetConfiguredUri(url, out _);

    public static bool TryGetConfiguredUri(string? url, out Uri uri)
    {
        uri = null!;

        if (string.IsNullOrWhiteSpace(url)
            || url.Contains("OWNER/REPO", StringComparison.OrdinalIgnoreCase)
            || url.Contains("example.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("https" or "http"))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public async Task<string?> TryDownloadAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (!TryGetConfiguredUri(url, out var uri))
        {
            return null;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DefaultTimeout);
            using var response = await _httpClient.GetAsync(uri, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
