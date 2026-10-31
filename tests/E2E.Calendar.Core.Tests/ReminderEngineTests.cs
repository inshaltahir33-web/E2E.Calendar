using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ReminderEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly IAlertRepository _alerts = Substitute.For<IAlertRepository>();
    private readonly IReminderRepository _reminders = Substitute.For<IReminderRepository>();
    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly ReminderEngine _engine;

    public ReminderEngineTests()
    {
        _engine = new ReminderEngine(_alerts, _reminders, _events, _tasks);
    }

    [Fact]
    public async Task A_persistent_alert_queues_the_next_repeat_after_the_fire_time()
    {
        var fired = FiredInstance();
        _alerts.GetAlertByIdAsync("alert-1", Arg.Any<CancellationToken>())
            .Returns(PersistentAlert(repeatEveryMinutes: 5));
        _reminders.GetByIdAsync("reminder-1", Arg.Any<CancellationToken>())
            .Returns(ActiveReminder());

        var scheduled = await _engine.TryScheduleRepeatAsync(fired, Now);

        scheduled.Should().BeTrue();
        await _alerts.Received(1).UpsertInstanceAsync(
            Arg.Is<AlertInstance>(instance =>
                instance.Id == ReminderEngine.RepeatInstanceId("alert-1", fired.OccurrenceUtc, 1) &&
                instance.AlertId == "alert-1" &&
                instance.OccurrenceUtc == fired.OccurrenceUtc &&
                instance.FireAtUtc == Now.AddMinutes(5) &&
                instance.RepeatCount == 1 &&
                instance.State == AlertState.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_alert_without_a_repeat_interval_never_repeats()
    {
        _alerts.GetAlertByIdAsync("alert-1", Arg.Any<CancellationToken>())
            .Returns(PersistentAlert(repeatEveryMinutes: null));

        var scheduled = await _engine.TryScheduleRepeatAsync(FiredInstance(), Now);

        scheduled.Should().BeFalse();
        await _alerts.DidNotReceive().UpsertInstanceAsync(Arg.Any<AlertInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_caught_up_repeat_is_queued_from_now_instead_of_the_stale_fire_time()
    {
        var stale = FiredInstance() with { FireAtUtc = Now.AddHours(-2) };
        _alerts.GetAlertByIdAsync("alert-1", Arg.Any<CancellationToken>())
            .Returns(PersistentAlert(repeatEveryMinutes: 10));
        _reminders.GetByIdAsync("reminder-1", Arg.Any<CancellationToken>())
            .Returns(ActiveReminder());

        var scheduled = await _engine.TryScheduleRepeatAsync(stale, Now);

        scheduled.Should().BeTrue();
        await _alerts.Received(1).UpsertInstanceAsync(
            Arg.Is<AlertInstance>(instance => instance.FireAtUtc == Now.AddMinutes(10)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_completed_reminder_stops_the_chain()
    {
        _alerts.GetAlertByIdAsync("alert-1", Arg.Any<CancellationToken>())
            .Returns(PersistentAlert(repeatEveryMinutes: 5));
        _reminders.GetByIdAsync("reminder-1", Arg.Any<CancellationToken>())
            .Returns(ActiveReminder() with { DoneUtc = Now });

        var scheduled = await _engine.TryScheduleRepeatAsync(FiredInstance(), Now);

        scheduled.Should().BeFalse();
        await _alerts.DidNotReceive().UpsertInstanceAsync(Arg.Any<AlertInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_queued_repeat_does_not_fire_once_its_reminder_is_done()
    {
        _reminders.GetByIdAsync("reminder-1", Arg.Any<CancellationToken>())
            .Returns(ActiveReminder() with { DoneUtc = Now });

        (await _engine.ShouldFireAsync(FiredInstance(repeatCount: 1))).Should().BeFalse();

        // The first (non-repeat) instance is always eligible; the planner owns it.
        (await _engine.ShouldFireAsync(FiredInstance())).Should().BeTrue();
    }

    [Fact]
    public async Task Stopping_a_chain_cancels_every_pending_repeat_of_that_occurrence()
    {
        var instance = FiredInstance(repeatCount: 2);

        await _engine.StopRepeatChainAsync(instance);

        await _alerts.Received(1).CancelPendingRepeatsAsync(
            instance.AlertId,
            instance.OccurrenceUtc,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_countdown_creates_a_one_shot_reminder_with_an_at_start_alert()
    {
        _alerts.GetAlertsForOwnerAsync(OwnerType.Reminder, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var reminder = await _engine.CreateCountdownAsync(25, Now, "UTC");

        reminder.Title.Should().Be("Reminder in 25 min");
        reminder.DueUtc.Should().Be(Now.AddMinutes(25));
        reminder.RRule.Should().BeNull();
        reminder.TimeZoneId.Should().Be("UTC");

        await _reminders.Received(1).UpsertAsync(
            Arg.Is<Reminder>(stored => stored.Id == reminder.Id && stored.DueUtc == Now.AddMinutes(25)),
            Arg.Any<CancellationToken>());

        await _alerts.Received(1).UpsertAlertAsync(
            Arg.Is<Alert>(alert =>
                alert.OwnerType == OwnerType.Reminder &&
                alert.OwnerId == reminder.Id &&
                alert.TriggerOffsetMinutes == 0 &&
                !alert.Persistent &&
                alert.RepeatEveryMinutes == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_persistent_standalone_reminder_registers_its_repeat_interval()
    {
        _alerts.GetAlertsForOwnerAsync(OwnerType.Reminder, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var reminder = new Reminder
        {
            Id = "reminder-2",
            Title = "Take medicine",
            DueUtc = Now.AddMinutes(10),
            TimeZoneId = "UTC",
            CreatedUtc = Now,
            UpdatedUtc = Now,
        };

        await _engine.CreateStandaloneAsync(reminder, repeatEveryMinutes: 5);

        await _alerts.Received(1).UpsertAlertAsync(
            Arg.Is<Alert>(alert =>
                alert.OwnerId == "reminder-2" &&
                alert.Persistent &&
                alert.RepeatEveryMinutes == 5),
            Arg.Any<CancellationToken>());
    }

    private static AlertInstance FiredInstance(int repeatCount = 0) => new()
    {
        Id = repeatCount == 0
            ? "alert-1:1000"
            : $"alert-1:1000:repeat:{repeatCount}",
        AlertId = "alert-1",
        OwnerType = OwnerType.Reminder,
        OwnerId = "reminder-1",
        OccurrenceUtc = Now.AddHours(-1),
        FireAtUtc = Now,
        State = AlertState.Fired,
        RepeatCount = repeatCount,
    };

    private static Alert PersistentAlert(int? repeatEveryMinutes) => new()
    {
        Id = "alert-1",
        OwnerType = OwnerType.Reminder,
        OwnerId = "reminder-1",
        TriggerOffsetMinutes = 0,
        Persistent = repeatEveryMinutes is > 0,
        RepeatEveryMinutes = repeatEveryMinutes,
    };

    private static Reminder ActiveReminder() => new()
    {
        Id = "reminder-1",
        Title = "Take medicine",
        DueUtc = Now.AddHours(-1),
        TimeZoneId = "UTC",
        CreatedUtc = Now.AddDays(-1),
        UpdatedUtc = Now.AddDays(-1),
    };
}
