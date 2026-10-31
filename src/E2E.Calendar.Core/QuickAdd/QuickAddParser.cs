using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Recurrence;

namespace E2E.Calendar.Core.QuickAdd;

/// <summary>
/// Parses natural-language phrases such as
/// "Lunch with Ali tomorrow 1pm at Kohinoor" or
/// "remind me 30 min before dentist tomorrow 4pm".
/// Extraction runs from the most specific token to the least specific one so
/// patterns cannot overlap, and every extractor removes its matched span.
/// </summary>
public static partial class QuickAddParser
{
    private static readonly string[] MonthNames =
    [
        "january", "february", "march", "april", "may", "june",
        "july", "august", "september", "october", "november", "december",
    ];

    private static readonly (string Token, DayOfWeek Day)[] Weekdays =
    [
        ("monday", DayOfWeek.Monday),
        ("tuesday", DayOfWeek.Tuesday),
        ("wednesday", DayOfWeek.Wednesday),
        ("thursday", DayOfWeek.Thursday),
        ("friday", DayOfWeek.Friday),
        ("saturday", DayOfWeek.Saturday),
        ("sunday", DayOfWeek.Sunday),
    ];

    public static ParsedItem Parse(string? input, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var timeZone = clock.LocalTimeZone;
        var nowLocal = timeZone.ToLocal(clock.UtcNow);
        var working = $" {(input ?? string.Empty).Trim()} ";

        var kind = ParsedItemKind.Event;
        string? calendarHint = null;
        var priority = Priority.Normal;
        int? leadMinutes = null;
        int? relativeMinutes = null;
        var durationMinutes = (int?)null;
        DateOnly? explicitDate = null;
        var explicitTime = (TimeOnly?)null;
        DayOfWeek? weekday = null;
        var nextWeekday = false;
        RecurrenceFrequency? frequency = null;
        var interval = 1;
        var weeklyDays = new List<DayOfWeek>();

        string Extract(string pattern, Func<Match, bool> onMatch, RegexOptions options = RegexOptions.IgnoreCase)
        {
            var regex = new Regex(pattern, options | RegexOptions.CultureInvariant);

            working = regex.Replace(working, match =>
            {
                onMatch(match);

                return " ";
            });

            return working;
        }

        // Tags, priority and the "remind me" prefix.
        Extract(@"(?:^|\s)#([\p{L}\p{Nd}_]+)", match =>
        {
            calendarHint = match.Groups[1].Value;
            return true;
        });

        Extract(@"(?:^|\s)!(high|normal|low|critical)\b", match =>
        {
            priority = match.Groups[1].Value.ToLowerInvariant() switch
            {
                "high" => Priority.High,
                "critical" => Priority.Critical,
                "low" => Priority.Low,
                _ => Priority.Normal,
            };

            return true;
        });

        if (ReminderPrefixRegex().IsMatch(working))
        {
            kind = ParsedItemKind.Reminder;
            working = ReminderPrefixRegex().Replace(working, " ", 1);
        }

        // Explicit lead time: "remind me 30 min before" / "30 minutes before".
        Extract(@"\b(?:remind(?:\s+me)?\s+)?(\d+)\s*(minutes?|mins?|m|hours?|hrs?|h)\s*before\b", match =>
        {
            leadMinutes = NormaliseMinutes(match.Groups[1].Value, match.Groups[2].Value);

            if (leadMinutes is not null)
            {
                kind = ParsedItemKind.Reminder;
            }

            return true;
        });

        // "remind me in 10 minutes" and the generic "in 10 minutes". A bare relative
        // time is a countdown reminder: it only has to fire once, N minutes from now.
        Extract(@"\bin\s+(\d+)\s*(minutes?|mins?|m|hours?|hrs?|h)\b", match =>
        {
            relativeMinutes = NormaliseMinutes(match.Groups[1].Value, match.Groups[2].Value);

            if (relativeMinutes is not null)
            {
                kind = ParsedItemKind.Reminder;
            }

            return true;
        }, RegexOptions.IgnoreCase);

        // Recurrence: "every day", "every 2 weeks", "every monday",
        // "every week on mon, wed", "every weekday".
        Extract(@"\bevery\s+weekday\b", _ =>
        {
            frequency = RecurrenceFrequency.Weekly;
            weeklyDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

            return true;
        });

        Extract(@"\bevery\s+week\s+on\s+((?:(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*)(?:\s*(?:,|and|&)\s*(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*)*)", match =>
        {
            var days = ParseWeekdays(match.Groups[1].Value);

            if (days.Count > 0)
            {
                frequency = RecurrenceFrequency.Weekly;
                weeklyDays = days;
            }

            return true;
        });

        Extract(@"\bevery\s+(?:(\d+)\s+)?(day|days|week|weeks|month|months|year|years)\b", match =>
        {
            frequency = match.Groups[2].Value.ToLowerInvariant() switch
            {
                "day" or "days" => RecurrenceFrequency.Daily,
                "week" or "weeks" => RecurrenceFrequency.Weekly,
                "month" or "months" => RecurrenceFrequency.Monthly,
                _ => RecurrenceFrequency.Yearly,
            };

            if (int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                interval = parsed;
            }

            return true;
        });

        Extract(@"\bevery\s+((?:(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*)(?:\s*(?:,|and|&)\s*(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*)*)", match =>
        {
            var days = ParseWeekdays(match.Groups[1].Value);

            if (days.Count > 0)
            {
                frequency = RecurrenceFrequency.Weekly;
                weeklyDays = days;
            }

            return true;
        });

        // Duration: "for 30 min", "for 2 hours".
        Extract(@"\bfor\s+(\d+)\s*(minutes?|mins?|m|hours?|hrs?|h)\b", match =>
        {
            durationMinutes = NormaliseMinutes(match.Groups[1].Value, match.Groups[2].Value);
            return true;
        });

        // Dates. Explicit ISO and slash dates come first so their digits are
        // never consumed by a clock pattern.
        Extract(@"\b(\d{4})-(\d{2})-(\d{2})\b", match =>
        {
            if (DateOnly.TryParseExact(match.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                explicitDate = date;
            }

            return true;
        });

        Extract(@"\b(\d{1,2})/(\d{1,2})(?:/(\d{2,4}))?\b", match =>
        {
            var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var year = match.Groups[3].Success
                ? NormaliseYear(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture))
                : nowLocal.Year;

            AddDate(ref explicitDate, year, month, day);

            return true;
        });

        Extract(@"\b(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?\s+(\d{1,2})(?:,?\s*(\d{4}))?\b", match =>
        {
            var month = MonthIndex(match.Groups[1].Value);
            var day = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var year = match.Groups[3].Success
                ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                : nowLocal.Year;

            AddDate(ref explicitDate, year, month, day);

            return true;
        });

        Extract(@"\b(\d{1,2})\s+(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\b(?:,?\s*(\d{4}))?", match =>
        {
            var month = MonthIndex(match.Groups[2].Value);
            var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var year = match.Groups[3].Success
                ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                : nowLocal.Year;

            AddDate(ref explicitDate, year, month, day);

            return true;
        });

        Extract(@"\bday\s+after\s+tomorrow\b", _ =>
        {
            explicitDate = DateOnly.FromDateTime(nowLocal).AddDays(2);
            return true;
        });

        Extract(@"\btomorrow\b", _ =>
        {
            explicitDate = DateOnly.FromDateTime(nowLocal).AddDays(1);
            return true;
        });

        Extract(@"\btonight\b", _ =>
        {
            explicitDate = DateOnly.FromDateTime(nowLocal);
            explicitTime = new TimeOnly(20, 0);

            return true;
        });

        Extract(@"\btoday\b", _ =>
        {
            explicitDate = DateOnly.FromDateTime(nowLocal);
            return true;
        });

        Extract(@"\bnext\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", match =>
        {
            weekday = ParseWeekday(match.Groups[1].Value);
            nextWeekday = true;

            return true;
        });

        Extract(@"\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", match =>
        {
            weekday ??= ParseWeekday(match.Groups[1].Value);
            return true;
        });

        // Times. "1pm" and "at 9" are both supported; clock values without a
        // date are anchored to today (or tomorrow when they already passed).
        Extract(@"\b(noon|midnight)\b", match =>
        {
            explicitTime = match.Groups[1].Value.Equals("noon", StringComparison.OrdinalIgnoreCase)
                ? new TimeOnly(12, 0)
                : new TimeOnly(0, 0);

            return true;
        });

        Extract(@"\b(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)\b", match =>
        {
            explicitTime = BuildTime(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            return true;
        });

        Extract(@"\b(\d{1,2}):(\d{2})\b", match =>
        {
            explicitTime ??= BuildTime(match.Groups[1].Value, match.Groups[2].Value, null);
            return true;
        });

        Extract(@"\bat\s+(\d{1,2})\b", match =>
        {
            explicitTime ??= BuildTime(match.Groups[1].Value, null, null);
            return true;
        });

        // Location: everything after a trailing "at". Runs after the clock so
        // "at 3" is treated as a time, not a place.
        var location = (string?)null;
        var locationMatch = Regex.Match(working, @"\bat\s+([^,]{2,})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (locationMatch.Success)
        {
            location = locationMatch.Groups[1].Value.Trim();
            working = working[..locationMatch.Index];
        }

        var title = Regex.Replace(working, @"\s+", " ").Trim(' ', ',', '.', ';', '-', '–');

        if (string.IsNullOrWhiteSpace(title))
        {
            title = kind == ParsedItemKind.Reminder && relativeMinutes is { } countdown
                ? $"Reminder in {countdown} min"
                : calendarHint is null ? "Untitled" : $"New {calendarHint}";
        }

        if (title.Equals("new event", StringComparison.OrdinalIgnoreCase))
        {
            title = "Untitled";
        }

        // Resolve the date.
        var today = DateOnly.FromDateTime(nowLocal);
        var nowTime = TimeOnly.FromDateTime(nowLocal);

        DateOnly date;
        var confidence = 0.6d;

        if (explicitDate is { } parsedDate)
        {
            date = parsedDate;
            confidence = 0.85d;
        }
        else if (weekday is { } targetDay)
        {
            date = NextWeekday(today, targetDay, nextWeekday);
            confidence = 0.8d;
        }
        else if (relativeMinutes is not null)
        {
            date = today;
            confidence = 0.9d;
        }
        else if (explicitTime is not null && explicitTime <= nowTime)
        {
            date = today.AddDays(1);
        }
        else
        {
            date = today;
        }

        if (relativeMinutes is { } relative)
        {
            explicitTime = TimeOnly.FromDateTime(nowLocal.AddMinutes(relative));
            date = DateOnly.FromDateTime(nowLocal.AddMinutes(relative));
        }

        var alertOffsets = new List<int>();

        if (leadMinutes is { } lead)
        {
            alertOffsets.Add(lead);
        }
        else if (kind == ParsedItemKind.Reminder)
        {
            alertOffsets.Add(0);
        }

        var rule = frequency is { } repeat
            ? RecurrenceRuleBuilder.Build(new RecurrenceRuleOptions
            {
                Frequency = repeat,
                Interval = interval,
                WeeklyDays = weeklyDays.Distinct().OrderBy(day => day).ToList(),
            })
            : null;

        var resolved = ResolveTimes(kind, date, explicitTime, durationMinutes, timeZone);

        return new ParsedItem
        {
            Kind = kind,
            Title = title,
            Date = date,
            StartTime = resolved.StartTime,
            EndTime = resolved.EndTime,
            StartUtc = resolved.StartUtc,
            EndUtc = resolved.EndUtc,
            AllDay = resolved.AllDay,
            Location = location,
            CalendarHint = calendarHint,
            AlertOffsetsMinutes = alertOffsets,
            RRule = rule,
            Priority = priority,
            Confidence = Math.Min(1d, confidence + (location is null ? 0d : 0.05d) + (rule is null ? 0d : 0.05d)),
        };
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc, TimeOnly? StartTime, TimeOnly? EndTime, bool AllDay)
        ResolveTimes(ParsedItemKind kind, DateOnly date, TimeOnly? time, int? durationMinutes, TimeZoneInfo timeZone)
    {
        if (time is null)
        {
            if (kind == ParsedItemKind.Reminder)
            {
                var reminderStart = timeZone.ToUtc(date.ToDateTime(new TimeOnly(9, 0)));

                return (reminderStart, reminderStart.AddMinutes(15), new TimeOnly(9, 0), new TimeOnly(9, 15), false);
            }

            var dayStart = timeZone.ToUtc(date.ToDateTime(TimeOnly.MinValue));

            return (dayStart, dayStart.AddDays(1), null, null, true);
        }

        var startLocal = date.ToDateTime(time.Value);
        var duration = durationMinutes ?? (kind == ParsedItemKind.Reminder ? 15 : 60);
        var endLocal = startLocal.AddMinutes(duration);

        return (
            timeZone.ToUtc(startLocal),
            timeZone.ToUtc(endLocal),
            time,
            TimeOnly.FromDateTime(endLocal),
            false);
    }

    private static int? NormaliseMinutes(string amount, string unit)
    {
        if (!int.TryParse(amount, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return null;
        }

        var normalised = unit.ToLowerInvariant();

        return normalised.StartsWith('h') ? value * 60 : value;
    }

    private static TimeOnly? BuildTime(string hourText, string? minuteText, string? meridiem)
    {
        if (!int.TryParse(hourText, CultureInfo.InvariantCulture, out var hour))
        {
            return null;
        }

        var minute = 0;

        if (!string.IsNullOrEmpty(minuteText) && !int.TryParse(minuteText, CultureInfo.InvariantCulture, out minute))
        {
            return null;
        }

        if (meridiem?.Equals("pm", StringComparison.OrdinalIgnoreCase) == true && hour < 12)
        {
            hour += 12;
        }
        else if (meridiem?.Equals("am", StringComparison.OrdinalIgnoreCase) == true && hour == 12)
        {
            hour = 0;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return null;
        }

        return new TimeOnly(hour, minute);
    }

    private static void AddDate(ref DateOnly? date, int year, int month, int day)
    {
        try
        {
            date = new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    private static int NormaliseYear(int year) => year < 100 ? 2000 + year : year;

    private static int MonthIndex(string token)
    {
        var lower = token.ToLowerInvariant();

        for (var index = 0; index < MonthNames.Length; index++)
        {
            if (MonthNames[index].StartsWith(lower, StringComparison.Ordinal) ||
                lower.StartsWith(MonthNames[index][..3], StringComparison.Ordinal))
            {
                return index + 1;
            }
        }

        return 1;
    }

    private static DayOfWeek ParseWeekday(string token)
    {
        var lower = token.ToLowerInvariant();

        foreach (var (name, day) in Weekdays)
        {
            if (name.Equals(lower, StringComparison.Ordinal) || name.StartsWith(lower, StringComparison.Ordinal))
            {
                return day;
            }
        }

        return DayOfWeek.Monday;
    }

    private static List<DayOfWeek> ParseWeekdays(string text)
    {
        var days = new List<DayOfWeek>();

        foreach (Match match in Regex.Matches(text, @"\b(mon|tue|wed|thu|fri|sat|sun)[a-z]*\b", RegexOptions.IgnoreCase))
        {
            var day = ParseWeekday(match.Groups[1].Value);

            if (!days.Contains(day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    private static DateOnly NextWeekday(DateOnly from, DayOfWeek target, bool strictlyNext)
    {
        var delta = ((int)target - (int)from.DayOfWeek + 7) % 7;

        if (delta == 0)
        {
            return strictlyNext ? from.AddDays(7) : from;
        }

        return from.AddDays(delta);
    }

    [GeneratedRegex(@"^\s*remind(?:\s+me)?(?:\s+to)?\s+", RegexOptions.IgnoreCase)]
    private static partial Regex ReminderPrefixRegex();
}
