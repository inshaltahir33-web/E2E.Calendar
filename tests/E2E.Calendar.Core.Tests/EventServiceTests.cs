using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using E2E.Calendar.Core.Sync;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class EventServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public EventServiceTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    private EventService CreateService(TimeSpan? undoWindow = null) =>
        new(_events, _outbox, _clock, undoWindow);

    private static EventDraft CreateDraft(
        string title = "Standup",
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null) =>
        new()
        {
            CalendarId = "cal-local",
            Title = title,
            StartUtc = startUtc ?? new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            EndUtc = endUtc ?? new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero),
            TimeZoneId = "Asia/Kolkata",
        };

    private static CalendarEvent StoredEvent(
        string id = "evt-1",
        bool deleted = false,
        DateTimeOffset? updatedUtc = null) =>
        new()
        {
            Id = id,
            CalendarId = "cal-local",
            RemoteId = "remote-1",
            ETag = "etag-1",
            Title = "Standup",
            StartUtc = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero),
            TimeZoneId = "Asia/Kolkata",
            CreatedUtc = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            UpdatedUtc = updatedUtc ?? new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero),
            Deleted = deleted,
            Dirty = false,
        };

    [Fact]
    public async Task CreateAsync_persists_the_event_as_dirty_with_creation_timestamps()
    {
        CalendarEvent? persisted = null;
        await _events.UpsertAsync(Arg.Do<CalendarEvent>(e => persisted = e), Arg.Any<CancellationToken>());

        var created = await CreateService().CreateAsync(CreateDraft());

        persisted.Should().NotBeNull();
        persisted!.Id.Should().NotBeNullOrWhiteSpace();
        persisted.Title.Should().Be("Standup");
        persisted.CalendarId.Should().Be("cal-local");
        persisted.TimeZoneId.Should().Be("Asia/Kolkata");
        persisted.CreatedUtc.Should().Be(Now);
        persisted.UpdatedUtc.Should().Be(Now);
        persisted.Dirty.Should().BeTrue();
        persisted.Deleted.Should().BeFalse();
        created.Should().BeSameAs(persisted);
    }

    [Fact]
    public async Task CreateAsync_enqueues_an_outbox_upsert_for_the_new_event()
    {
        var created = await CreateService().CreateAsync(CreateDraft());

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<OutboxItem>(i =>
                i.Entity == OutboxEntities.Event &&
                i.EntityId == created.Id &&
                i.Op == OutboxOp.Upsert),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_normalises_the_supplied_times_to_utc()
    {
        var istOffset = TimeSpan.FromHours(5.5);
        var draft = CreateDraft(
            startUtc: new DateTimeOffset(2026, 10, 21, 9, 0, 0, istOffset),
            endUtc: new DateTimeOffset(2026, 10, 21, 9, 30, 0, istOffset));

        var created = await CreateService().CreateAsync(draft);

        created.StartUtc.Offset.Should().Be(TimeSpan.Zero);
        created.EndUtc.Offset.Should().Be(TimeSpan.Zero);
        created.StartUtc.Should().Be(new DateTimeOffset(2026, 10, 21, 3, 30, 0, TimeSpan.Zero));
        created.EndUtc.Should().Be(new DateTimeOffset(2026, 10, 21, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateAsync_rejects_an_event_that_ends_before_it_starts()
    {
        var draft = CreateDraft(
            startUtc: new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
            endUtc: new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

        var act = () => CreateService().CreateAsync(draft);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_blank_title()
    {
        var act = () => CreateService().CreateAsync(CreateDraft(title: "   "));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_writes_nothing_when_validation_fails()
    {
        var draft = CreateDraft(
            startUtc: new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
            endUtc: new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

        var act = () => CreateService().CreateAsync(draft);

        await act.Should().ThrowAsync<ArgumentException>();
        await _events.DidNotReceive().UpsertAsync(Arg.Any<CalendarEvent>(), Arg.Any<CancellationToken>());
        await _outbox.DidNotReceive().EnqueueAsync(Arg.Any<OutboxItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_stamps_the_update_time_and_marks_the_event_dirty()
    {
        _events.GetByIdAsync("evt-1", Arg.Any<CancellationToken>()).Returns(StoredEvent());

        var edited = StoredEvent() with { Title = "Daily standup" };

        var updated = await CreateService().UpdateAsync(edited);

        updated.Title.Should().Be("Daily standup");
        updated.UpdatedUtc.Should().Be(Now);
        updated.CreatedUtc.Should().Be(new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero));
        updated.Dirty.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_preserves_the_server_side_identity_of_the_stored_event()
    {
        _events.GetByIdAsync("evt-1", Arg.Any<CancellationToken>()).Returns(StoredEvent());

        var edited = StoredEvent() with { Title = "Daily standup", RemoteId = "other", ETag = "other" };

        var updated = await CreateService().UpdateAsync(edited);

        updated.RemoteId.Should().Be("remote-1");
        updated.ETag.Should().Be("etag-1");
        updated.Id.Should().Be("evt-1");
    }

    [Fact]
    public async Task UpdateAsync_enqueues_an_outbox_upsert()
    {
        _events.GetByIdAsync("evt-1", Arg.Any<CancellationToken>()).Returns(StoredEvent());

        var updated = await CreateService().UpdateAsync(StoredEvent() with { Title = "Daily standup" });

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<OutboxItem>(i => i.EntityId == updated.Id && i.Op == OutboxOp.Upsert),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_throws_when_the_event_does_not_exist()
    {
        _events.GetByIdAsync("missing", Arg.Any<CancellationToken>()).Returns((CalendarEvent?)null);

        var act = () => CreateService().UpdateAsync(StoredEvent(id: "missing"));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SoftDeleteAsync_marks_the_event_deleted_and_enqueues_a_delete()
    {
        _events.GetByIdAsync("evt-1", Arg.Any<CancellationToken>()).Returns(StoredEvent());

        var deleted = await CreateService().SoftDeleteAsync("evt-1");

        deleted.Deleted.Should().BeTrue();
        deleted.Dirty.Should().BeTrue();
        deleted.UpdatedUtc.Should().Be(Now);

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<OutboxItem>(i => i.EntityId == "evt-1" && i.Op == OutboxOp.Delete),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UndoDeleteAsync_restores_a_soft_deleted_event()
    {
        _events.GetByIdAsync("evt-1", Arg.Any<CancellationToken>())
            .Returns(StoredEvent(deleted: true, updatedUtc: Now.AddSeconds(-5)));

        var restored = await CreateService().UndoDeleteAsync("evt-1");

        restored.Deleted.Should().BeFalse();
        restored.Dirty.Should().BeTrue();
        restored.UpdatedUtc.Should().Be(Now);

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<OutboxItem>(i => i.EntityId == "evt-1" && i.Op == OutboxOp.Upsert),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UndoDeleteAsync_throws_once_the_undo_window_has_expired()
    {
        _events.GetByIdAsync("evt-1", Arg.Any<CancellationToken>())
            .Returns(StoredEvent(deleted: true, updatedUtc: Now.AddSeconds(-30)));

        var act = () => CreateService().UndoDeleteAsync("evt-1");

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _outbox.DidNotReceive().EnqueueAsync(Arg.Any<OutboxItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UndoDeleteAsync_throws_when_the_event_was_not_deleted()
    {
        _events.GetByIdAsync("evt-1", Arg.Any<CancellationToken>()).Returns(StoredEvent());

        var act = () => CreateService().UndoDeleteAsync("evt-1");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
