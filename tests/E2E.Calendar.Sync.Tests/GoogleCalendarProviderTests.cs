using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Core.Sync;
using E2E.Calendar.Sync;
using E2E.Calendar.Sync.Google;
using FluentAssertions;
using System.Net;
using System.Net.Http;
using Xunit;

namespace E2E.Calendar.Sync.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> RequestBodies { get; } = new();

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
        {
            RequestBodies.Add(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }
        else
        {
            RequestBodies.Add(string.Empty);
        }
        if (_responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(_responses.Dequeue());
    }
}

public class GoogleCalendarProviderTests
{
    private static (GoogleCalendarProvider provider, FakeHttpMessageHandler handler, InMemorySecretStore secrets, string accountId) Create()
    {
        var handler = new FakeHttpMessageHandler();
        var http = new HttpClient(handler);
        var secrets = new InMemorySecretStore();
        var entitlements = new FreeEntitlementService();
        var logger = global::Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleCalendarProvider>.Instance;
        var provider = new GoogleCalendarProvider(http, secrets, entitlements, logger);

        var accountId = Guid.NewGuid().ToString("N");
        secrets.SetAsync($"google:access:{accountId}", "test-token").GetAwaiter().GetResult();
        return (provider, handler, secrets, accountId);
    }

    [Fact]
    public async Task PullAsync_ReturnsEventsAndSyncToken()
    {
        var (provider, handler, _, accountId) = Create();
        var calendar = new E2E.Calendar.Core.Models.Calendar
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            RemoteId = "primary",
            Name = "Primary",
        };

        var json = """
        {
          "items": [
            {
              "id": "evt1",
              "summary": "Standup",
              "start": { "dateTime": "2026-10-23T09:30:00+05:00" },
              "end": { "dateTime": "2026-10-23T09:45:00+05:00" },
              "updated": "2026-10-22T10:00:00Z",
              "created": "2026-10-22T10:00:00Z"
            }
          ],
          "nextSyncToken": "token-123"
        }
        """;

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

        var result = await provider.PullAsync(calendar);

        result.ResetRequired.Should().BeFalse();
        result.NextSyncToken.Should().Be("token-123");
        result.Events.Should().HaveCount(1);
        result.Events[0].Title.Should().Be("Standup");
        result.Events[0].RemoteId.Should().Be("evt1");
    }

    [Fact]
    public async Task PullAsync_On410_ReturnsResetRequired()
    {
        var (provider, handler, _, accountId) = Create();
        var calendar = new E2E.Calendar.Core.Models.Calendar
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            RemoteId = "primary",
            Name = "Primary",
            SyncToken = "old-token",
        };

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Gone));

        var result = await provider.PullAsync(calendar);

        result.ResetRequired.Should().BeTrue();
        result.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task PushAsync_Upsert_PostsEvent()
    {
        var (provider, handler, _, accountId) = Create();
        var calendar = new E2E.Calendar.Core.Models.Calendar
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            RemoteId = "primary",
            Name = "Primary",
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

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        });

        await provider.PushAsync(calendar, outbox);

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.RequestBodies.Should().HaveCount(1);
        handler.RequestBodies[0].Should().Contain("Lunch");
    }
}
