using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Sync;
using E2E.Calendar.Sync.Ics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CalendarModel = E2E.Calendar.Core.Models.Calendar;

namespace E2E.Calendar.Sync.CalDav;

public sealed class CalDavProvider : ISyncProvider
{
    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;
    private readonly IcsService _ics;
    private readonly ILogger<CalDavProvider> _logger;
    private readonly string _url;
    private readonly string _username;
    private readonly string _password;

    public CalDavProvider(
        HttpClient http,
        ISecretStore secrets,
        IcsService ics,
        ILogger<CalDavProvider> logger,
        IConfiguration configuration)
    {
        _http = http;
        _secrets = secrets;
        _ics = ics;
        _logger = logger;
        _url = configuration["CalDav:Url"]
            ?? Environment.GetEnvironmentVariable("E2E_CALDAV_URL")
            ?? string.Empty;
        _username = configuration["CalDav:Username"]
            ?? Environment.GetEnvironmentVariable("E2E_CALDAV_USERNAME")
            ?? string.Empty;
        _password = configuration["CalDav:Password"]
            ?? Environment.GetEnvironmentVariable("E2E_CALDAV_PASSWORD")
            ?? string.Empty;
    }

    public Provider Provider => Provider.CalDav;

    public async Task<Account> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_url, UriKind.Absolute, out var endpoint) ||
            string.IsNullOrWhiteSpace(_username) ||
            string.IsNullOrWhiteSpace(_password))
        {
            throw new InvalidOperationException("CalDAV URL, username, and password are not configured.");
        }

        var accountId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_url}|{_username}")))[..16];
        await _secrets.SetAsync($"caldav:password:{accountId}", _password, cancellationToken);
        await _secrets.SetAsync($"caldav:url:{accountId}", endpoint.ToString(), cancellationToken);

        return new Account
        {
            Id = accountId,
            Provider = Provider.CalDav,
            DisplayName = _username,
            Email = _username,
        };
    }

    public async Task<IReadOnlyList<CalendarModel>> ListCalendarsAsync(Account account, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Parse("PROPFIND"), await GetUrlAsync(account, cancellationToken));
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(
            """<?xml version="1.0"?><d:propfind xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav"><d:prop><d:displayname><d:resourcetype><c:calendar-description</d:prop></d:propfind>""",
            Encoding.UTF8,
            "application/xml");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        XNamespace dav = "DAV:";
        XNamespace calDav = "urn:ietf:params:xml:ns:caldav";

        return xml.Descendants(dav + "response")
            .Select(item => new
            {
                Href = item.Element(dav + "href")?.Value,
                Name = item.Descendants(dav + "displayname").FirstOrDefault()?.Value,
                IsCalendar = item.Descendants(dav + "resourcetype").Descendants(calDav + "calendar").Any(),
            })
            .Where(item => item.IsCalendar && !string.IsNullOrWhiteSpace(item.Href))
            .Select(item => new CalendarModel
            {
                Id = Guid.NewGuid().ToString("N"),
                AccountId = account.Id,
                RemoteId = new Uri(new Uri(_url), item.Href!).ToString(),
                Name = string.IsNullOrWhiteSpace(item.Name) ? "CalDAV calendar" : item.Name!,
                IsLocal = false,
            })
            .ToArray();
    }

    public async Task<PullResult> PullAsync(CalendarModel calendar, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(calendar.RemoteId))
        {
            return new PullResult();
        }

        var body = """<?xml version="1.0"?><c:calendar-query xmlns:c="urn:ietf:params:xml:ns:caldav" xmlns:d="DAV:"><d:prop><d:getetag/><c:calendar-data/></d:prop><c:filter><c:comp-filter name="VCALENDAR"><c:comp-filter name="VEVENT"/></c:comp-filter></c:filter></c:calendar-query>""";
        using var request = CreateRequest(HttpMethod.Parse("REPORT"), calendar.RemoteId);
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(body, Encoding.UTF8, "application/xml");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        XNamespace dav = "DAV:";
        var now = DateTimeOffset.UtcNow;
        var events = new List<CalendarEvent>();
        foreach (var item in xml.Descendants(dav + "response"))
        {
            var href = item.Element(dav + "href")?.Value;
            var data = item.Descendants().FirstOrDefault(node => node.Name.LocalName == "calendar-data")?.Value;
            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            events.AddRange(_ics.Import(data, calendar.Id, now).Select(ev => ev with
            {
                RemoteId = new Uri(new Uri(_url), href).ToString(),
                ETag = item.Descendants(dav + "getetag").FirstOrDefault()?.Value,
                Dirty = false,
            }));
        }

        return new PullResult { Events = events };
    }

    public async Task PushAsync(CalendarModel calendar, OutboxItem item, CancellationToken cancellationToken = default)
    {
        var remoteId = item.Payload is null ? item.EntityId : item.EntityId;
        if (item.Op == OutboxOp.Delete)
        {
            using var delete = CreateRequest(HttpMethod.Delete, remoteId);
            using var response = await _http.SendAsync(delete, cancellationToken);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
            return;
        }

        var ev = System.Text.Json.JsonSerializer.Deserialize<CalendarEvent>(item.Payload ?? string.Empty)
            ?? throw new InvalidOperationException("The CalDAV event payload is invalid.");
        var target = string.IsNullOrWhiteSpace(ev.RemoteId)
            ? $"{calendar.RemoteId!.TrimEnd('/')}/{Uri.EscapeDataString(ev.Id)}.ics"
            : ev.RemoteId;
        using var request = CreateRequest(new HttpMethod("PUT"), target);
        request.Headers.TryAddWithoutValidation("If-Match", ev.ETag);
        request.Content = new StringContent(_ics.Export([ev]), Encoding.UTF8, "text/calendar");
        using var result = await _http.SendAsync(request, cancellationToken);
        result.EnsureSuccessStatusCode();
    }

    public Task RespondToInvitationAsync(CalendarModel calendar, CalendarEvent ev, RsvpStatus status, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<CalendarEvent>> ImportBirthdaysAsync(Account account, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CalendarEvent>>([]);

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        return request;
    }

    private async Task<string> GetUrlAsync(Account account, CancellationToken cancellationToken)
    {
        var value = await _secrets.GetAsync($"caldav:url:{account.Id}", cancellationToken);
        return value ?? _url;
    }
}
