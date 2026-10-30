using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ReminderPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Plans_event_alert_at_offset_and_uses_stable_instance_id()
    {
        var alerts = Substitute.For<IAlertRepository>();
        var events = Substitute.For<IEventRepository>();
        var reminders = Substitute.For<IReminderRepository>();
        var tasks = Substitute.For<ITaskRepository>();
        var recurrence = Substitute.For<IRecurrenceExpander>();

        var calendarEvent = new CalendarEvent
        {
            Id = "event-1",
            CalendarId = "calendar-1",
            Title = "Planning",
            StartUtc = Now.AddHours(2),
            EndUtc = Now.AddHours(3),
            TimeZoneId = "UTC",
            CreatedUtc = Now,
            UpdatedUtc = Now,
        };
        var alert = new Alert
        {
            Id = "alert-1",
            OwnerType = OwnerType.Event,
            OwnerId = calendarEvent.Id,
            TriggerOffsetMinutes = 15,
        };

        alerts.GetAllAlertsAsync(Arg.Any<CancellationToken>()).Returns([alert]);
        events.GetByIdAsync(calendarEvent.Id, Arg.Any<CancellationToken>()).Returns(calendarEvent);

        var planner = new ReminderPlanner(alerts, events, reminders, tasks, recurrence);
        var count = await planner.PlanAsync(Now, TimeSpan.FromDays(14));

        count.Should().Be(1);
        await alerts.Received(1).UpsertInstanceAsync(
            Arg.Is<AlertInstance>(instance =>
                instance.Id == ReminderPlanner.InstanceId(alert.Id, calendarEvent.StartUtc) &&
                instance.FireAtUtc == calendarEvent.StartUtc.AddMinutes(-15) &&
                instance.State == AlertState.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plans_recurring_event_occurrences_from_the_expander()
    {
        var alerts = Substitute.For<IAlertRepository>();
        var events = Substitute.For<IEventRepository>();
        var reminders = Substitute.For<IReminderRepository>();
        var tasks = Substitute.For<ITaskRepository>();
        var recurrence = Substitute.For<IRecurrenceExpander>();
        var master = new CalendarEvent
        {
            Id = "master-1",
            CalendarId = "calendar-1",
            Title = "Daily",
            StartUtc = Now.AddHours(1),
            EndUtc = Now.AddHours(2),
            TimeZoneId = "UTC",
            RRule = "FREQ=DAILY;COUNT=2",
            CreatedUtc = Now,
            UpdatedUtc = Now,
        };
        var alert = new Alert
        {
            Id = "alert-2",
            OwnerType = OwnerType.Event,
            OwnerId = master.Id,
            TriggerOffsetMinutes = 0,
        };
        var occurrences = new[]
        {
            new Occurrence { EventId = master.Id, StartUtc = Now.AddHours(1), EndUtc = Now.AddHours(2) },
            new Occurrence { EventId = master.Id, StartUtc = Now.AddDays(1).AddHours(1), EndUtc = Now.AddDays(1).AddHours(2) },
        };

        alerts.GetAllAlertsAsync(Arg.Any<CancellationToken>()).Returns([alert]);
        events.GetByIdAsync(master.Id, Arg.Any<CancellationToken>()).Returns(master);
        events.GetExceptionsAsync(master.Id, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        recurrence.Expand(master, Arg.Any<IEnumerable<CalendarEvent>>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>())
            .Returns(occurrences);

        var planner = new ReminderPlanner(alerts, events, reminders, tasks, recurrence);
        var count = await planner.PlanAsync(Now, TimeSpan.FromDays(14));

        count.Should().Be(2);
        await alerts.Received(2).UpsertInstanceAsync(Arg.Any<AlertInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plans_recurring_reminder_occurrences_from_the_rule()
    {
        var alerts = Substitute.For<IAlertRepository>();
        var events = Substitute.For<IEventRepository>();
        var reminders = Substitute.For<IReminderRepository>();
        var tasks = Substitute.For<ITaskRepository>();
        var recurrence = Substitute.For<IRecurrenceExpander>();

        var reminder = new Reminder
        {
            Id = "reminder-1",
            Title = "Stretch",
            DueUtc = Now.AddHours(1),
            TimeZoneId = "UTC",
            RRule = "FREQ=DAILY;COUNT=2",
            CreatedUtc = Now,
            UpdatedUtc = Now,
        };
        var alert = new Alert
        {
            Id = "alert-3",
            OwnerType = OwnerType.Reminder,
            OwnerId = reminder.Id,
            TriggerOffsetMinutes = 0,
        };
        var occurrences = new[]
        {
            new Occurrence { EventId = reminder.Id, StartUtc = Now.AddHours(1), EndUtc = Now.AddHours(1) },
            new Occurrence { EventId = reminder.Id, StartUtc = Now.AddDays(1).AddHours(1), EndUtc = Now.AddDays(1).AddHours(1) },
        };

        alerts.GetAllAlertsAsync(Arg.Any<CancellationToken>()).Returns([alert]);
        reminders.GetByIdAsync(reminder.Id, Arg.Any<CancellationToken>()).Returns(reminder);
        recurrence.Expand(
            Arg.Is<CalendarEvent>(master => master.Id == reminder.Id && master.RRule == reminder.RRule),
            Arg.Any<IEnumerable<CalendarEvent>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>()).Returns(occurrences);

        var planner = new ReminderPlanner(alerts, events, reminders, tasks, recurrence);
        var count = await planner.PlanAsync(Now, TimeSpan.FromDays(30));

        count.Should().Be(2);
        await alerts.Received(2).UpsertInstanceAsync(Arg.Any<AlertInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_plan_alerts_whose_trigger_time_is_in_the_past()
    {
        var alerts = Substitute.For<IAlertRepository>();
        var events = Substitute.For<IEventRepository>();
        var reminders = Substitute.For<IReminderRepository>();
        var tasks = Substitute.For<ITaskRepository>();
        var recurrence = Substitute.For<IRecurrenceExpander>();

        var calendarEvent = new CalendarEvent
        {
            Id = "event-past",
            CalendarId = "calendar-1",
            Title = "Already gone",
            StartUtc = Now.AddHours(-3),
            EndUtc = Now.AddHours(-2),
            TimeZoneId = "UTC",
            CreatedUtc = Now,
            UpdatedUtc = Now,
        };
        var alert = new Alert
        {
            Id = "alert-past",
            OwnerType = OwnerType.Event,
            OwnerId = calendarEvent.Id,
            TriggerOffsetMinutes = 0,
        };

        alerts.GetAllAlertsAsync(Arg.Any<CancellationToken>()).Returns([alert]);
        events.GetByIdAsync(calendarEvent.Id, Arg.Any<CancellationToken>()).Returns(calendarEvent);

        var planner = new ReminderPlanner(alerts, events, reminders, tasks, recurrence);
        var count = await planner.PlanAsync(Now, TimeSpan.FromDays(14));

        count.Should().Be(0);
        await alerts.DidNotReceive().UpsertInstanceAsync(Arg.Any<AlertInstance>(), Arg.Any<CancellationToken>());
    }
}
