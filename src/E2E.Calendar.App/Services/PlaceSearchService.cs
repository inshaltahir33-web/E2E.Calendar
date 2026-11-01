using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App.Services;

public sealed record PlaceSearchResult(string DisplayName, double Latitude, double Longitude);

/// <summary>
/// Searches OpenStreetMap Nominatim. No API key is required; the service identifies
/// itself with the "E2ECalendar" User-Agent and limits itself to one request per
/// second as required by the Nominatim usage policy.
/// </summary>
public sealed class PlaceSearchService
{
    private static readonly HttpClient SharedClient = new();
    private readonly ILogger<PlaceSearchService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;

    public PlaceSearchService(ILogger<PlaceSearchService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PlaceSearchResult>();
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            var elapsed = DateTimeOffset.UtcNow - _lastRequestUtc;
            if (elapsed < TimeSpan.FromSeconds(1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - elapsed, cancellationToken);
            }

            _lastRequestUtc = DateTimeOffset.UtcNow;

            var url = "https://nominatim.openstreetmap.org/search?format=json&limit=8&q=" +
                      Uri.EscapeDataString(query.Trim());

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("E2ECalendar");

            using var response = await SharedClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Nominatim returned {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var results = new List<PlaceSearchResult>();

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("display_name", out var nameElement) ||
                    !element.TryGetProperty("lat", out var latElement) ||
                    !element.TryGetProperty("lon", out var lonElement))
                {
                    continue;
                }

                if (!double.TryParse(latElement.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var latitude) ||
                    !double.TryParse(lonElement.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var longitude))
                {
                    continue;
                }

                results.Add(new PlaceSearchResult(nameElement.GetString() ?? query.Trim(), latitude, longitude));
            }

            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Place search failed (offline or network error).");
            throw new InvalidOperationException("No internet connection", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Place search timed out.");
            throw new InvalidOperationException("No internet connection", ex);
        }
        finally
        {
            _gate.Release();
        }
    }
}
