using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Core.Sync;
using E2E.Calendar.Sync;
using FluentAssertions;
using System.Net;
using System.Net.Http;
using Xunit;

namespace E2E.Calendar.Sync.Tests;

public class GraphCalendarProviderTests
{
    private static (E2E.Calendar.Sync.Microsoft.GraphCalendarProvider provider, FakeHttpMessageHandler handler, InMemorySecretStore secrets, string accountId) Create()
    {
        var handler = new FakeHttpMessageHandler();
        var http = new HttpClient(handler);
        var secrets = new InMemorySecretStore();
        var entitlements = new FreeEntitlementService();
        var logger = global::Microsoft.Extensions.Logging.Abstractions.NullLogger<E2E.Calendar.Sync.Microsoft.GraphCalendarProvider>.Instance;
        var provider = new E2E.Calendar.Sync.Microsoft.GraphCalendarProvider(http, secrets, entitlements, logger);

        var accountId = Guid.NewGuid().ToString("N");
        secrets.SetAsync($"ms:access:{accountId}", "test-token").GetAwaiter().GetResult();
        return (provider, handler, secrets, accountId);
    }

    [Fact]
    public async Task PullAsync_ReturnsEventsAndDeltaLink()
    {
        var (provider, handler, _, accountId) = Create();
        var calendar = new E2E.Calendar.Core.Models.Calendar
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            RemoteId = "cal1",
            Name = "Calendar",
        };

        var json = """
        {
          "value": [
            {
              "id": "evt1",
              "subject": "Standup",
              "bodyPreview": "Daily",
              "start": { "dateTime": "2026-10-23T09:30:00", "timeZone": "UTC" },
              "end": { "dateTime": "2026-10-23T09:45:00", "timeZone": "UTC" },
              "onlineMeeting": { "joinUrl": "https://teams.example/join" }
            }
          ],
          "@odata.deltaLink": "https://graph.microsoft.com/v1.0/me/calendars/cal1/calendarView/delta?token=abc"
        }
        """;

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

        var result = await provider.PullAsync(calendar);

        result.ResetRequired.Should().BeFalse();
        result.NextSyncToken.Should().Contain("token=abc");
        result.Events.Should().HaveCount(1);
        result.Events[0].Title.Should().Be("Standup");
        result.Events[0].MeetingUrl.Should().Be("https://teams.example/join");
    }

    [Fact]
    public async Task PullAsync_OnSyncStateNotFound_ReturnsResetRequired()
    {
        var (provider, handler, _, accountId) = Create();
        var calendar = new E2E.Calendar.Core.Models.Calendar
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            RemoteId = "cal1",
            Name = "Calendar",
            SyncToken = "https://graph.microsoft.com/v1.0/me/calendars/cal1/calendarView/delta?token=old",
        };

        var json = """{ "error": { "code": "syncStateNotFound", "message": "Sync state not found" } }""";
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

        var result = await provider.PullAsync(calendar);

        result.ResetRequired.Should().BeTrue();
    }

    [Fact]
    public async Task PushAsync_Upsert_PostsEvent()
    {
        var (provider, handler, _, accountId) = Create();
        var calendar = new E2E.Calendar.Core.Models.Calendar
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            RemoteId = "cal1",
            Name = "Calendar",
        };

        var ev = new CalendarEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            CalendarId = calendar.Id,
            Title = "Lunch",
            StartUtc = new DateTimeOffset(2026, 10, 23, 13, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 10, 23, 14, 0, 0, TimeSpan.Zero),
            TimeZoneId = "UTC",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        var payload = System.Text.Json.JsonSerializer.Serialize(ev);
        var outbox = new OutboxItem
        {
            Entity = "event",
            EntityId = ev.Id,
            Op = OutboxOp.Upsert,
            Payload = payload,
        };

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        });

        await provider.PushAsync(calendar, outbox);

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.RequestBodies[0].Should().Contain("Lunch");
    }
}
