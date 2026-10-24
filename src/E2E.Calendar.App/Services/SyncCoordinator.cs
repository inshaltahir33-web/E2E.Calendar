using System.Collections.ObjectModel;
using System.Text.Json;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Sync;

namespace E2E.Calendar.App.Services;

public sealed class SyncCoordinator
{
    private readonly ICalendarRepository _calendars;
    private readonly IEventRepository _events;
    private readonly IAccountRepository _accounts;
    private readonly IAlertRepository _alerts;
    private readonly IReadOnlyDictionary<Provider, ISyncProvider> _providers;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SyncCoordinator(
        ICalendarRepository calendars,
        IEventRepository events,
        IAccountRepository accounts,
        IAlertRepository alerts,
        IEnumerable<ISyncProvider> providers)
    {
        _calendars = calendars;
        _events = events;
        _accounts = accounts;
        _alerts = alerts;
        _providers = providers.ToDictionary(provider => provider.Provider);
    }

    public ObservableCollection<Account> Accounts { get; } = new();

    public bool IsSyncing { get; private set; }

    public async Task LoadAccountsAsync(CancellationToken cancellationToken = default)
    {
        Accounts.Clear();
        foreach (var account in await _accounts.GetAllAsync(cancellationToken))
        {
            Accounts.Add(account);
        }
    }

    public async Task<Account> AddAccountAsync(Provider provider, CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(provider, out var syncProvider))
        {
            throw new InvalidOperationException($"The {provider} sync provider is not configured.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            IsSyncing = true;
            var account = await syncProvider.SignInAsync(cancellationToken);
            if (Accounts.Any(existing => existing.Id == account.Id))
            {
                return account;
            }

            Accounts.Add(account);
            await _accounts.UpsertAsync(account, cancellationToken);
            await ImportCalendarsAsync(account, syncProvider, cancellationToken);
            return account;
        }
        finally
        {
            IsSyncing = false;
            _gate.Release();
        }
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            IsSyncing = true;
            var calendars = await _calendars.GetAllAsync(cancellationToken);
            foreach (var calendar in calendars.Where(calendar => !calendar.IsLocal && calendar.AccountId is not null))
            {
                var account = Accounts.FirstOrDefault(item => item.Id == calendar.AccountId);
                if (account is null || !_providers.TryGetValue(account.Provider, out var provider))
                {
                    continue;
                }

                var result = await provider.PullAsync(calendar, cancellationToken);
                if (result.ResetRequired)
                {
                    result = await provider.PullAsync(calendar with { SyncToken = null }, cancellationToken);
                }

                foreach (var calendarEvent in result.Events)
                {
                    await _events.UpsertAsync(calendarEvent, cancellationToken);
                }

                await _calendars.UpsertAsync(calendar with { SyncToken = result.NextSyncToken }, cancellationToken);
            }
        }
        finally
        {
            IsSyncing = false;
            _gate.Release();
        }
    }

    public bool HasPendingInvitation(CalendarEvent calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Attendees))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(calendarEvent.Attendees);
            return document.RootElement.ValueKind == JsonValueKind.Array &&
                document.RootElement.EnumerateArray().Any(attendee =>
                {
                    var isSelf = attendee.TryGetProperty("self", out var self) && self.GetBoolean();
                    var response = attendee.TryGetProperty("responseStatus", out var googleStatus)
                        ? googleStatus.GetString()
                        : attendee.TryGetProperty("status", out var graphStatus) &&
                          graphStatus.TryGetProperty("response", out var graphResponse)
                            ? graphResponse.GetString()
                            : null;
                    return isSelf && string.Equals(response, "needsAction", StringComparison.OrdinalIgnoreCase);
                });
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task RespondToInvitationAsync(
        CalendarEvent calendarEvent,
        RsvpStatus status,
        CancellationToken cancellationToken = default)
    {
        var calendar = await _calendars.GetByIdAsync(calendarEvent.CalendarId, cancellationToken)
            ?? throw new InvalidOperationException("The invitation calendar is no longer available.");
        var account = Accounts.FirstOrDefault(item => item.Id == calendar.AccountId)
            ?? throw new InvalidOperationException("The connected account is no longer available.");
        if (!_providers.TryGetValue(account.Provider, out var provider))
        {
            throw new InvalidOperationException($"The {account.Provider} sync provider is not configured.");
        }

        await provider.RespondToInvitationAsync(calendar, calendarEvent, status, cancellationToken);
        var response = status switch
        {
            RsvpStatus.Accepted => "accepted",
            RsvpStatus.Tentative => "tentative",
            RsvpStatus.Declined => "declined",
            _ => "needsAction",
        };
        await _events.UpsertAsync(
            calendarEvent with { Attendees = UpdateSelfResponse(calendarEvent.Attendees, response), Dirty = false },
            cancellationToken);
    }

    public async Task<int> ImportBirthdaysAsync(CancellationToken cancellationToken = default)
    {
        var imported = 0;
        foreach (var account in Accounts)
        {
            if (!_providers.TryGetValue(account.Provider, out var provider))
            {
                continue;
            }

            var calendar = new E2E.Calendar.Core.Models.Calendar
            {
                Id = $"birthdays-{account.Id}",
                AccountId = account.Id,
                Name = "Birthdays",
                Color = CalendarPalette.Personal,
                Visible = true,
                ReadOnly = true,
                IsLocal = false,
            };
            await _calendars.UpsertAsync(calendar, cancellationToken);

            foreach (var birthday in await provider.ImportBirthdaysAsync(account, cancellationToken))
            {
                var eventWithCalendar = birthday with { CalendarId = calendar.Id };
                await _events.UpsertAsync(eventWithCalendar, cancellationToken);
                await _alerts.UpsertAlertAsync(new Alert
                {
                    Id = $"birthday-day-before-{eventWithCalendar.Id}",
                    OwnerType = OwnerType.Event,
                    OwnerId = eventWithCalendar.Id,
                    TriggerOffsetMinutes = 15 * 60,
                }, cancellationToken);
                await _alerts.UpsertAlertAsync(new Alert
                {
                    Id = $"birthday-day-of-{eventWithCalendar.Id}",
                    OwnerType = OwnerType.Event,
                    OwnerId = eventWithCalendar.Id,
                    TriggerAtUtc = eventWithCalendar.StartUtc,
                }, cancellationToken);
                imported++;
            }
        }

        return imported;
    }

    private static string UpdateSelfResponse(string? attendeesJson, string response)
    {
        if (string.IsNullOrWhiteSpace(attendeesJson))
        {
            return attendeesJson ?? "[]";
        }

        using var document = JsonDocument.Parse(attendeesJson);
        var attendees = document.RootElement.EnumerateArray().Select(attendee =>
        {
            var values = attendee.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone());
            if (values.ContainsKey("self"))
            {
                values["responseStatus"] = JsonSerializer.SerializeToElement(response);
            }

            return values;
        });
        return JsonSerializer.Serialize(attendees);
    }

    private async Task ImportCalendarsAsync(
        Account account,
        ISyncProvider provider,
        CancellationToken cancellationToken)
    {
        var calendars = await provider.ListCalendarsAsync(account, cancellationToken);
        foreach (var calendar in calendars)
        {
            await _calendars.UpsertAsync(calendar, cancellationToken);
        }
    }
}
