using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.App.Services;

public sealed class UpdateCheckService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdateCheckService> _logger;

    public UpdateCheckService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<UpdateCheckService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckOnceAsync(stoppingToken);
        }
    }

    private async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        var manifestUrl = _configuration["AppUpdates:ManifestUrl"]
            ?? Environment.GetEnvironmentVariable("E2E_UPDATE_MANIFEST_URL");
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http"))
        {
            return;
        }

        try
        {
            await using var stream = await _httpClient.GetStreamAsync(uri, cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, cancellationToken: cancellationToken);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return;
            }

            var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
            if (Version.TryParse(manifest.Version, out var available) && available > current)
            {
                _logger.LogInformation("A newer calendar version is available: {Version}", available);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Calendar update check failed");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Calendar update manifest was invalid");
        }
    }

    private sealed record UpdateManifest(string Version, string? DownloadUrl);
}
