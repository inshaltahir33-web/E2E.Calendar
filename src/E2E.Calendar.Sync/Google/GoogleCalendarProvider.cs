using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using E2E.Calendar.Core;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.Sync.Google;

public sealed class GoogleCalendarProvider : ISyncProvider
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
    private const string CalendarListEndpoint = "https://www.googleapis.com/calendar/v3/users/me/calendarList";
    private const string CalendarApiBase = "https://www.googleapis.com/calendar/v3/calendars";

    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;
    private readonly IEntitlementService _entitlements;
    private readonly ILogger<GoogleCalendarProvider> _logger;

    public GoogleCalendarProvider(
        HttpClient http,
        ISecretStore secrets,
        IEntitlementService entitlements,
        ILogger<GoogleCalendarProvider> logger,
        IConfiguration? configuration = null)
    {
        _http = http;
        _secrets = secrets;
        _entitlements = entitlements;
        _logger = logger;
        ClientId = configuration?["Google:ClientId"]
            ?? Environment.GetEnvironmentVariable("E2E_GOOGLE_CLIENT_ID")
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? string.Empty;
        ClientSecret = configuration?["Google:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("E2E_GOOGLE_CLIENT_SECRET")
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
    }

    public string ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public E2E.Calendar.Core.Models.Provider Provider => E2E.Calendar.Core.Models.Provider.Google;

    public async Task<Account> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException("Google client id is not configured.");
        }

        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Guid.NewGuid().ToString("N");

        var port = GetFreePort();
        var redirectUri = $"http://127.0.0.1:{port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        var authUrl =
            $"{AuthEndpoint}?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            "&scope=" + Uri.EscapeDataString("https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/contacts.readonly https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/userinfo.profile") +
            "&code_challenge=" + Uri.EscapeDataString(challenge) +
            "&code_challenge_method=S256" +
            "&state=" + Uri.EscapeDataString(state) +
            "&access_type=offline&prompt=consent";

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(authUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open the system browser automatically.");
        }

        var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
        var code = context.Request.QueryString["code"];
        var returnedState = context.Request.QueryString["state"];

        var responseText = "<html><body>Sign-in complete. You can close this window.</body></html>";
        var buffer = Encoding.UTF8.GetBytes(responseText);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        context.Response.Close();
        listener.Stop();

        if (string.IsNullOrWhiteSpace(code) || !string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Google sign-in was cancelled or state mismatch.");
        }

        var tokenJson = await ExchangeCodeAsync(code!, redirectUri, verifier, cancellationToken);
        using var tokenDoc = JsonDocument.Parse(tokenJson);

        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        var refreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        string? email = null;
        string? name = null;

        try
        {
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var userResponse = await _http.SendAsync(userRequest, cancellationToken);
            if (userResponse.IsSuccessStatusCode)
            {
                using var userDoc = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(cancellationToken));
                email = userDoc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
                name = userDoc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch Google user info.");
        }

        var accountId = Guid.NewGuid().ToString("N");

        await _secrets.SetAsync($"google:access:{accountId}", accessToken, cancellationToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _secrets.SetAsync($"google:refresh:{accountId}", refreshToken!, cancellationToken);
        }

        return new Account
        {
            Id = accountId,
            Provider = E2E.Calendar.Core.Models.Provider.Google,
            DisplayName = name ?? email ?? "Google account",
            Email = email,
        };
    }

    private async Task<string> ExchangeCodeAsync(string code, string redirectUri, string verifier, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = verifier,
        });
        if (!string.IsNullOrWhiteSpace(ClientSecret))
        {
            content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier,
            });
        }

        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<E2E.Calendar.Core.Models.Calendar>> ListCalendarsAsync(Account account, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account.Id, cancellationToken);
        var result = new List<E2E.Calendar.Core.Models.Calendar>();

        using var request = new HttpRequestMessage(HttpMethod.Get, CalendarListEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!doc.RootElement.TryGetProperty("items", out var items))
        {
            return result;
        }

        foreach (var item in items.EnumerateArray())
        {
            var remoteId = item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
            var name = item.TryGetProperty("summary", out var summary) ? summary.GetString() ?? remoteId : remoteId;
            var color = item.TryGetProperty("backgroundColor", out var bg) ? bg.GetString() : null;

            result.Add(new E2E.Calendar.Core.Models.Calendar
            {
                Id = Guid.NewGuid().ToString("N"),
                AccountId = account.Id,
                RemoteId = remoteId,
                Name = name,
                Color = CalendarPalette.Nearest(color),
                Visible = true,
                IsLocal = false,
            });
        }

        return result;
    }

    public async Task<PullResult> PullAsync(E2E.Calendar.Core.Models.Calendar calendar, CancellationToken cancellationToken = default)
    {
        var accountId = calendar.AccountId ?? string.Empty;
        var accessToken = await GetAccessTokenAsync(accountId, cancellationToken);
        var calendarId = string.IsNullOrWhiteSpace(calendar.RemoteId) ? "primary" : calendar.RemoteId!;
        var events = new List<CalendarEvent>();
        string? syncToken = calendar.SyncToken;
        string? pageToken = null;

        do
        {
            var url = $"{CalendarApiBase}/{Uri.EscapeDataString(calendarId)}/events?singleEvents=false&maxResults=2500";
            if (!string.IsNullOrWhiteSpace(syncToken))
            {
                url += $"&syncToken={Uri.EscapeDataString(syncToken!)}";
            }
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                url += $"&pageToken={Uri.EscapeDataString(pageToken!)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Gone)
            {
                return new PullResult { ResetRequired = true };
            }

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var mapped = MapEvent(item, calendar.Id);
                    if (mapped is not null)
                    {
                        events.Add(mapped);
                    }
                }
            }

            pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var npt) ? npt.GetString() : null;
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                continue;
            }

            syncToken = doc.RootElement.TryGetProperty("nextSyncToken", out var nst) ? nst.GetString() : syncToken;
            break;
        }
        while (true);

        return new PullResult
        {
            Events = events,
            NextSyncToken = syncToken,
            ResetRequired = false,
        };
    }

    public async Task PushAsync(E2E.Calendar.Core.Models.Calendar calendar, OutboxItem item, CancellationToken cancellationToken = default)
    {
        var accountId = calendar.AccountId ?? string.Empty;
        var accessToken = await GetAccessTokenAsync(accountId, cancellationToken);
        var calendarId = string.IsNullOrWhiteSpace(calendar.RemoteId) ? "primary" : calendar.RemoteId!;

        CalendarEvent? localEvent = null;
        if (!string.IsNullOrWhiteSpace(item.Payload))
        {
            localEvent = JsonSerializer.Deserialize<CalendarEvent>(item.Payload);
        }

        if (item.Op == OutboxOp.Delete)
        {
            var remoteId = localEvent?.RemoteId ?? item.EntityId;
            if (string.IsNullOrWhiteSpace(remoteId))
            {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{CalendarApiBase}/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(remoteId!)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
            return;
        }

        if (localEvent is null)
        {
            return;
        }

        var json = BuildEventJson(localEvent);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        if (string.IsNullOrWhiteSpace(localEvent.RemoteId))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{CalendarApiBase}/{Uri.EscapeDataString(calendarId)}/events")
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        else
        {
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{CalendarApiBase}/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(localEvent.RemoteId!)}")
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (!string.IsNullOrWhiteSpace(localEvent.ETag))
            {
                request.Headers.TryAddWithoutValidation("If-Match", localEvent.ETag);
            }
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    private async Task<string> GetAccessTokenAsync(string accountId, CancellationToken cancellationToken)
    {
        var token = await _secrets.GetAsync($"google:access:{accountId}", cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("No Google access token stored for this account.");
        }
        return token!;
    }

    private static CalendarEvent? MapEvent(JsonElement element, string calendarId)
    {
        if (element.TryGetProperty("status", out var status) && status.GetString() == "cancelled")
        {
            // Still surface as a deleted event so callers can tombstone locally.
        }

        var remoteId = element.TryGetProperty("id", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(remoteId))
        {
            return null;
        }

        var title = element.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "(no title)" : "(no title)";
        var notes = element.TryGetProperty("description", out var description) ? description.GetString() : null;
        var location = element.TryGetProperty("location", out var loc) ? loc.GetString() : null;
        var url = element.TryGetProperty("htmlLink", out var html) ? html.GetString() : null;
        var etag = element.TryGetProperty("etag", out var et) ? et.GetString() : null;

        var start = element.TryGetProperty("start", out var startEl) ? startEl : default;
        var end = element.TryGetProperty("end", out var endEl) ? endEl : default;

        var allDay = start.ValueKind == JsonValueKind.Object && start.TryGetProperty("date", out _);
        DateTimeOffset startUtc;
        DateTimeOffset endUtc;
        string timeZoneId;

        if (allDay)
        {
            var startDate = start.GetProperty("date").GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
            var endDate = end.TryGetProperty("date", out var ed) ? ed.GetString() ?? startDate : startDate;
            startUtc = DateTimeOffset.Parse(startDate + "T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            endUtc = DateTimeOffset.Parse(endDate + "T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            timeZoneId = "UTC";
        }
        else
        {
            var startText = start.GetProperty("dateTime").GetString() ?? DateTime.UtcNow.ToString("O");
            var endText = end.GetProperty("dateTime").GetString() ?? DateTime.UtcNow.ToString("O");
            startUtc = DateTimeOffset.Parse(startText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            endUtc = DateTimeOffset.Parse(endText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            timeZoneId = start.TryGetProperty("timeZone", out var tz) ? tz.GetString() ?? "UTC" : "UTC";
        }

        var deleted = element.TryGetProperty("status", out var st) && st.GetString() == "cancelled";

        string? rrule = null;
        string? exDates = null;
        if (element.TryGetProperty("recurrence", out var recurrence) && recurrence.ValueKind == JsonValueKind.Array)
        {
            var rules = new List<string>();
            var exs = new List<DateTimeOffset>();
            foreach (var r in recurrence.EnumerateArray())
            {
                var text = r.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
                {
                    rules.Add(text.Substring("RRULE:".Length));
                }
                else if (text.StartsWith("EXDATE", StringComparison.OrdinalIgnoreCase))
                {
                    // Best-effort: store raw EXDATE line as-is in ExDates by parsing the date after the colon.
                    var colon = text.IndexOf(':');
                    if (colon >= 0)
                    {
                        var datePart = text.Substring(colon + 1);
                        if (DateTimeOffset.TryParse(datePart, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                        {
                            exs.Add(parsed);
                        }
                    }
                }
            }

            rrule = rules.Count > 0 ? string.Join("\n", rules) : null;
            exDates = exs.Count > 0 ? ExDateList.Format(exs) : null;
        }

        string? meetingUrl = null;
        if (element.TryGetProperty("hangoutLink", out var hangout))
        {
            meetingUrl = hangout.GetString();
        }
        else if (element.TryGetProperty("conferenceData", out var conference) &&
                 conference.TryGetProperty("entryPoints", out var entryPoints) &&
                 entryPoints.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entryPoints.EnumerateArray())
            {
                if (entry.TryGetProperty("entryPointType", out var type) && type.GetString() == "video" &&
                    entry.TryGetProperty("uri", out var uri))
                {
                    meetingUrl = uri.GetString();
                    break;
                }
            }
        }

        var created = element.TryGetProperty("created", out var createdEl) && DateTimeOffset.TryParse(createdEl.GetString(), out var createdUtc)
            ? createdUtc
            : DateTimeOffset.UtcNow;
        var updated = element.TryGetProperty("updated", out var updatedEl) && DateTimeOffset.TryParse(updatedEl.GetString(), out var updatedUtc)
            ? updatedUtc
            : DateTimeOffset.UtcNow;
        string? attendees = element.TryGetProperty("attendees", out var attendeeElement)
            ? attendeeElement.GetRawText()
            : null;

        return new CalendarEvent
        {
            Id = remoteId!,
            CalendarId = calendarId,
            RemoteId = remoteId,
            ETag = etag,
            Title = title,
            Notes = notes,
            Location = location,
            Url = url,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AllDay = allDay,
            TimeZoneId = timeZoneId,
            RRule = rrule,
            ExDates = exDates,
            Color = null,
            MeetingUrl = meetingUrl,
            Attendees = attendees,
            CreatedUtc = created,
            UpdatedUtc = updated,
            Deleted = deleted,
            Dirty = false,
        };
    }

    private static string BuildEventJson(CalendarEvent ev)
    {
        object start = ev.AllDay
            ? new { date = ev.StartUtc.ToString("yyyy-MM-dd") }
            : new { dateTime = ev.StartUtc.ToString("O"), timeZone = ev.TimeZoneId };
        object end = ev.AllDay
            ? new { date = ev.EndUtc.ToString("yyyy-MM-dd") }
            : new { dateTime = ev.EndUtc.ToString("O"), timeZone = ev.TimeZoneId };

        var recurrence = new List<string>();
        if (!string.IsNullOrWhiteSpace(ev.RRule))
        {
            foreach (var line in ev.RRule!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                recurrence.Add("RRULE:" + line);
            }
        }
        if (!string.IsNullOrWhiteSpace(ev.ExDates))
        {
            var exDates = ExDateList.Parse(ev.ExDates);
            foreach (var ex in exDates)
            {
                recurrence.Add("EXDATE:" + ex.ToUniversalTime().ToString("yyyyMMddTHHmmssZ"));
            }
        }

        var payload = new Dictionary<string, object?>
        {
            ["summary"] = ev.Title,
            ["description"] = NotesText.ToPlainText(ev.Notes),
            ["location"] = ev.Location,
            ["start"] = start,
            ["end"] = end,
        };

        if (recurrence.Count > 0)
        {
            payload["recurrence"] = recurrence;
        }

        return JsonSerializer.Serialize(payload);
    }

    private static string CreateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task RespondToInvitationAsync(E2E.Calendar.Core.Models.Calendar calendar, CalendarEvent ev, RsvpStatus status, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(calendar.AccountId ?? string.Empty, cancellationToken);
        var calendarId = string.IsNullOrWhiteSpace(calendar.RemoteId) ? "primary" : calendar.RemoteId!;
        var remoteId = ev.RemoteId ?? ev.Id;

        var responseStatus = status switch
        {
            RsvpStatus.Accepted => "accepted",
            RsvpStatus.Tentative => "tentative",
            RsvpStatus.Declined => "declined",
            _ => "needsAction",
        };

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"{CalendarApiBase}/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(remoteId)}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var getResponse = await _http.SendAsync(getRequest, cancellationToken);
        getResponse.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(cancellationToken));
        var attendees = new List<Dictionary<string, object?>>();
        if (doc.RootElement.TryGetProperty("attendees", out var atts) && atts.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in atts.EnumerateArray())
            {
                var email = a.TryGetProperty("email", out var e) ? e.GetString() : null;
                var self = a.TryGetProperty("self", out var s) && s.GetBoolean();
                var entry = new Dictionary<string, object?>
                {
                    ["email"] = email,
                    ["self"] = self,
                    ["responseStatus"] = self ? responseStatus : (a.TryGetProperty("responseStatus", out var rs) ? rs.GetString() : "needsAction"),
                };
                attendees.Add(entry);
            }
        }

        if (!attendees.Any(a => a.TryGetValue("self", out _) ))
        {
            // Best effort: mark first attendee? Google identifies self server-side.
        }

        var payload = JsonSerializer.Serialize(new { attendees });
        using var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"), $"{CalendarApiBase}/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(remoteId)}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        patchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var patchResponse = await _http.SendAsync(patchRequest, cancellationToken);
        patchResponse.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CalendarEvent>> ImportBirthdaysAsync(Account account, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account.Id, cancellationToken);
        var result = new List<CalendarEvent>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://people.googleapis.com/v1/people/me/connections?personFields=names,birthdays&pageSize=1000");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return result;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!doc.RootElement.TryGetProperty("connections", out var connections))
        {
            return result;
        }

        foreach (var person in connections.EnumerateArray())
        {
            var name = person.TryGetProperty("names", out var names) && names.GetArrayLength() > 0
                ? names[0].TryGetProperty("displayName", out var dn) ? dn.GetString() : null
                : null;

            if (!person.TryGetProperty("birthdays", out var birthdays) || birthdays.GetArrayLength() == 0)
            {
                continue;
            }

            var bday = birthdays[0].TryGetProperty("date", out var dateEl) ? dateEl : default;
            if (!bday.TryGetProperty("month", out var monthEl) || !bday.TryGetProperty("day", out var dayEl))
            {
                continue;
            }

            var month = monthEl.GetInt32();
            var day = dayEl.GetInt32();
            var year = DateTime.UtcNow.Year;

            if (bday.TryGetProperty("year", out var yearEl))
            {
                year = yearEl.GetInt32();
            }

            var start = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
            result.Add(new CalendarEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                CalendarId = string.Empty,
                Title = $"{name ?? "Contact"}'s birthday",
                StartUtc = start,
                EndUtc = start.AddDays(1),
                AllDay = true,
                TimeZoneId = "UTC",
                RRule = $"FREQ=YEARLY;BYMONTH={month};BYMONTHDAY={day}",
                Color = "Red",
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow,
            });
        }

        return result;
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
