using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;
using E2E.Calendar.Core.Sync;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.Sync.Microsoft;

public sealed class GraphCalendarProvider : ISyncProvider
{
    private const string AuthEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    private const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    private const string GraphBase = "https://graph.microsoft.com/v1.0";

    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;
    private readonly IEntitlementService _entitlements;
    private readonly ILogger<GraphCalendarProvider> _logger;

    public GraphCalendarProvider(
        HttpClient http,
        ISecretStore secrets,
        IEntitlementService entitlements,
        ILogger<GraphCalendarProvider> logger)
    {
        _http = http;
        _secrets = secrets;
        _entitlements = entitlements;
        _logger = logger;
    }

    public string ClientId { get; set; } = Environment.GetEnvironmentVariable("E2E_MS_CLIENT_ID") ?? string.Empty;

    public E2E.Calendar.Core.Models.Provider Provider => E2E.Calendar.Core.Models.Provider.Microsoft;

    public async Task<Account> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException("Microsoft client id is not configured.");
        }

        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Guid.NewGuid().ToString("N");

        var port = GetFreePort();
        var redirectUri = $"http://localhost:{port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        var authUrl =
            $"{AuthEndpoint}?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            "&scope=" + Uri.EscapeDataString("Calendars.ReadWrite Contacts.Read offline_access User.Read") +
            "&code_challenge=" + Uri.EscapeDataString(challenge) +
            "&code_challenge_method=S256" +
            "&state=" + Uri.EscapeDataString(state);

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

        var buffer = Encoding.UTF8.GetBytes("<html><body>Sign-in complete. You can close this window.</body></html>");
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        context.Response.Close();
        listener.Stop();

        if (string.IsNullOrWhiteSpace(code) || !string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Microsoft sign-in was cancelled or state mismatch.");
        }

        var tokenJson = await ExchangeCodeAsync(code!, redirectUri, verifier, cancellationToken);
        using var tokenDoc = JsonDocument.Parse(tokenJson);

        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        var refreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        string? email = null;
        string? name = null;

        try
        {
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/me");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var userResponse = await _http.SendAsync(userRequest, cancellationToken);
            if (userResponse.IsSuccessStatusCode)
            {
                using var userDoc = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(cancellationToken));
                email = userDoc.RootElement.TryGetProperty("mail", out var m) ? m.GetString() :
                        userDoc.RootElement.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null;
                name = userDoc.RootElement.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch Microsoft user info.");
        }

        var accountId = Guid.NewGuid().ToString("N");

        await _secrets.SetAsync($"ms:access:{accountId}", accessToken, cancellationToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _secrets.SetAsync($"ms:refresh:{accountId}", refreshToken!, cancellationToken);
        }

        return new Account
        {
            Id = accountId,
            Provider = E2E.Calendar.Core.Models.Provider.Microsoft,
            DisplayName = name ?? email ?? "Microsoft account",
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

        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<E2E.Calendar.Core.Models.Calendar>> ListCalendarsAsync(Account account, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account.Id, cancellationToken);
        var result = new List<E2E.Calendar.Core.Models.Calendar>();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/me/calendars");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!doc.RootElement.TryGetProperty("value", out var values))
        {
            return result;
        }

        foreach (var item in values.EnumerateArray())
        {
            var remoteId = item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? remoteId : remoteId;
            var color = item.TryGetProperty("color", out var colorEl) ? colorEl.GetString() : null;

            result.Add(new E2E.Calendar.Core.Models.Calendar
            {
                Id = Guid.NewGuid().ToString("N"),
                AccountId = account.Id,
                RemoteId = remoteId,
                Name = name,
                Color = color is null ? CalendarPalette.Fallback : CalendarPalette.Nearest(color),
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
        var start = DateTimeOffset.UtcNow.AddYears(-1);
        var end = DateTimeOffset.UtcNow.AddYears(2);

        string? nextLink = $"{GraphBase}/me/calendars/{Uri.EscapeDataString(calendarId)}/calendarView/delta?startDateTime={Uri.EscapeDataString(start.ToString("O"))}&endDateTime={Uri.EscapeDataString(end.ToString("O"))}";
        string? deltaLink = null;

        if (!string.IsNullOrWhiteSpace(calendar.SyncToken))
        {
            nextLink = calendar.SyncToken;
        }

        while (!string.IsNullOrWhiteSpace(nextLink))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextLink!);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Gone ||
                response.StatusCode == HttpStatusCode.NotFound ||
                body.Contains("syncStateNotFound", StringComparison.OrdinalIgnoreCase))
            {
                return new PullResult { ResetRequired = true };
            }

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var values))
            {
                foreach (var item in values.EnumerateArray())
                {
                    var mapped = MapEvent(item, calendar.Id);
                    if (mapped is not null)
                    {
                        events.Add(mapped);
                    }
                }
            }

            nextLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) ? nl.GetString() : null;
            if (string.IsNullOrWhiteSpace(nextLink) && doc.RootElement.TryGetProperty("@odata.deltaLink", out var dl))
            {
                deltaLink = dl.GetString();
            }
        }

        return new PullResult
        {
            Events = events,
            NextSyncToken = deltaLink ?? calendar.SyncToken,
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

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{GraphBase}/me/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(remoteId!)}");
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
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{GraphBase}/me/calendars/{Uri.EscapeDataString(calendarId)}/events")
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        else
        {
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{GraphBase}/me/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(localEvent.RemoteId!)}")
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    private async Task<string> GetAccessTokenAsync(string accountId, CancellationToken cancellationToken)
    {
        var token = await _secrets.GetAsync($"ms:access:{accountId}", cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("No Microsoft access token stored for this account.");
        }
        return token!;
    }

    private static CalendarEvent? MapEvent(JsonElement element, string calendarId)
    {
        var remoteId = element.TryGetProperty("id", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(remoteId))
        {
            return null;
        }

        var title = element.TryGetProperty("subject", out var subject) ? subject.GetString() ?? "(no title)" : "(no title)";
        var notes = element.TryGetProperty("bodyPreview", out var bp) ? bp.GetString() : null;
        var location = element.TryGetProperty("location", out var loc) && loc.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

        DateTimeOffset startUtc = DateTimeOffset.UtcNow;
        DateTimeOffset endUtc = DateTimeOffset.UtcNow.AddHours(1);
        string timeZoneId = "UTC";
        bool allDay = false;

        if (element.TryGetProperty("isAllDay", out var allDayEl))
        {
            allDay = allDayEl.GetBoolean();
        }

        if (element.TryGetProperty("start", out var startEl))
        {
            var dateTime = startEl.TryGetProperty("dateTime", out var sdt) ? sdt.GetString() : null;
            var tz = startEl.TryGetProperty("timeZone", out var stz) ? stz.GetString() : null;
            if (!string.IsNullOrWhiteSpace(dateTime))
            {
                startUtc = DateTimeOffset.Parse(dateTime!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
            }
            if (!string.IsNullOrWhiteSpace(tz))
            {
                timeZoneId = tz!;
            }
        }

        if (element.TryGetProperty("end", out var endEl))
        {
            var dateTime = endEl.TryGetProperty("dateTime", out var edt) ? edt.GetString() : null;
            if (!string.IsNullOrWhiteSpace(dateTime))
            {
                endUtc = DateTimeOffset.Parse(dateTime!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
            }
        }

        string? meetingUrl = null;
        if (element.TryGetProperty("onlineMeeting", out var om) && om.TryGetProperty("joinUrl", out var ju))
        {
            meetingUrl = ju.GetString();
        }

        // recurrence mapping (best-effort)
        string? rrule = null;
        if (element.TryGetProperty("recurrence", out var rec) && rec.ValueKind == JsonValueKind.Object)
        {
            rrule = ConvertGraphRecurrenceToRrule(rec);
        }

        string? recurrenceMasterId = null;
        DateTimeOffset? recurrenceId = null;
        if (element.TryGetProperty("seriesMasterId", out var sm) && sm.ValueKind == JsonValueKind.String)
        {
            recurrenceMasterId = sm.GetString();
        }
        if (element.TryGetProperty("originalStart", out var os) && os.ValueKind == JsonValueKind.String)
        {
            if (DateTimeOffset.TryParse(os.GetString(), out var parsed))
            {
                recurrenceId = parsed;
            }
        }

        var created = DateTimeOffset.UtcNow;
        var updated = DateTimeOffset.UtcNow;
        if (element.TryGetProperty("createdDateTime", out var cd) && DateTimeOffset.TryParse(cd.GetString(), out var cdt))
        {
            created = cdt;
        }
        if (element.TryGetProperty("lastModifiedDateTime", out var lmd) && DateTimeOffset.TryParse(lmd.GetString(), out var lmdt))
        {
            updated = lmdt;
        }

        var deleted = false;
        string? attendees = element.TryGetProperty("attendees", out var attendeeElement)
            ? attendeeElement.GetRawText()
            : null;

        return new CalendarEvent
        {
            Id = remoteId!,
            CalendarId = calendarId,
            RemoteId = remoteId,
            ETag = element.TryGetProperty("@odata.etag", out var et) ? et.GetString() : null,
            Title = title,
            Notes = notes,
            Location = location,
            Url = null,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AllDay = allDay,
            TimeZoneId = timeZoneId,
            RRule = rrule,
            ExDates = null,
            Color = null,
            MeetingUrl = meetingUrl,
            Attendees = attendees,
            TravelMinutes = null,
            CreatedUtc = created,
            UpdatedUtc = updated,
            Deleted = deleted,
            Dirty = false,
            RecurrenceMasterId = recurrenceMasterId,
            RecurrenceIdUtc = recurrenceId,
        };
    }

    private static string? ConvertGraphRecurrenceToRrule(JsonElement recurrence)
    {
        if (!recurrence.TryGetProperty("pattern", out var pattern))
        {
            return null;
        }

        var type = pattern.TryGetProperty("type", out var t) ? t.GetString() : null;
        var interval = pattern.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 1;

        string freq = type switch
        {
            "daily" => "DAILY",
            "weekly" => "WEEKLY",
            "absoluteMonthly" or "relativeMonthly" => "MONTHLY",
            "absoluteYearly" or "relativeYearly" => "YEARLY",
            _ => "DAILY",
        };

        var parts = new List<string> { $"FREQ={freq}", $"INTERVAL={interval}" };

        if (pattern.TryGetProperty("daysOfWeek", out var days) && days.ValueKind == JsonValueKind.Array)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sunday"] = "SU",
                ["monday"] = "MO",
                ["tuesday"] = "TU",
                ["wednesday"] = "WE",
                ["thursday"] = "TH",
                ["friday"] = "FR",
                ["saturday"] = "SA",
            };

            var byDay = new List<string>();
            foreach (var d in days.EnumerateArray())
            {
                var name = d.GetString();
                if (name is not null && map.TryGetValue(name, out var code))
                {
                    byDay.Add(code);
                }
            }

            if (byDay.Count > 0)
            {
                parts.Add("BYDAY=" + string.Join(",", byDay));
            }
        }

        if (pattern.TryGetProperty("dayOfMonth", out var dom) && dom.ValueKind == JsonValueKind.Number)
        {
            parts.Add($"BYMONTHDAY={dom.GetInt32()}");
        }

        if (pattern.TryGetProperty("month", out var month) && month.ValueKind == JsonValueKind.Number)
        {
            parts.Add($"BYMONTH={month.GetInt32()}");
        }

        if (recurrence.TryGetProperty("range", out var range))
        {
            var rangeType = range.TryGetProperty("type", out var rt) ? rt.GetString() : null;
            if (rangeType == "endDate" && range.TryGetProperty("endDate", out var ed))
            {
                var date = ed.GetString();
                if (!string.IsNullOrWhiteSpace(date))
                {
                    parts.Add("UNTIL=" + date!.Replace("-", "") + "T235959Z");
                }
            }
            else if (rangeType == "numbered" && range.TryGetProperty("numberOfOccurrences", out var n))
            {
                parts.Add($"COUNT={n.GetInt32()}");
            }
        }

        return string.Join(";", parts);
    }

    private static string BuildEventJson(CalendarEvent ev)
    {
        var payload = new Dictionary<string, object?>
        {
            ["subject"] = ev.Title,
            ["body"] = new { contentType = "text", content = ev.Notes },
            ["location"] = new { displayName = ev.Location },
            ["start"] = new { dateTime = ev.StartUtc.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = ev.TimeZoneId },
            ["end"] = new { dateTime = ev.EndUtc.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = ev.TimeZoneId },
            ["isAllDay"] = ev.AllDay,
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string CreateCodeVerifier() => Base64UrlEncode(RandomBytes(32));

    private static string CreateCodeChallenge(string verifier)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Base64UrlEncode(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task RespondToInvitationAsync(E2E.Calendar.Core.Models.Calendar calendar, CalendarEvent ev, RsvpStatus status, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(calendar.AccountId ?? string.Empty, cancellationToken);
        var calendarId = string.IsNullOrWhiteSpace(calendar.RemoteId) ? "primary" : calendar.RemoteId!;
        var remoteId = ev.RemoteId ?? ev.Id;

        var endpoint = status switch
        {
            RsvpStatus.Accepted => "accept",
            RsvpStatus.Tentative => "tentativelyAccept",
            RsvpStatus.Declined => "decline",
            _ => "accept",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GraphBase}/me/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(remoteId)}/{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CalendarEvent>> ImportBirthdaysAsync(Account account, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account.Id, cancellationToken);
        var result = new List<CalendarEvent>();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/me/contacts?$select=displayName,birthday&$top=1000");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return result;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!doc.RootElement.TryGetProperty("value", out var values))
        {
            return result;
        }

        foreach (var contact in values.EnumerateArray())
        {
            var name = contact.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
            if (!contact.TryGetProperty("birthday", out var birthday) || birthday.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(birthday.GetString(), out var bday))
            {
                continue;
            }

            var month = bday.Month;
            var day = bday.Day;
            var year = DateTime.UtcNow.Year;
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
