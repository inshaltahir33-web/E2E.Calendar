using System.Globalization;
using System.Text;

namespace E2E.Calendar.Core.Recurrence;

public static class RecurrenceRuleBuilder
{
    private static readonly string[] DayCodes =
    [
        "SU", "MO", "TU", "WE", "TH", "FR", "SA",
    ];

    public static string? Build(RecurrenceRuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Frequency == RecurrenceFrequency.None)
        {
            return null;
        }

        var builder = new StringBuilder();

        builder.Append("FREQ=").Append(options.Frequency switch
        {
            RecurrenceFrequency.Daily => "DAILY",
            RecurrenceFrequency.Weekly => "WEEKLY",
            RecurrenceFrequency.Monthly => "MONTHLY",
            RecurrenceFrequency.Yearly => "YEARLY",
            _ => throw new ArgumentOutOfRangeException(nameof(options)),
        });

        if (options.Interval > 1)
        {
            builder.Append(";INTERVAL=").Append(options.Interval.ToString(CultureInfo.InvariantCulture));
        }

        switch (options.Frequency)
        {
            case RecurrenceFrequency.Weekly when options.WeeklyDays.Count > 0:
                builder.Append(";BYDAY=").Append(JoinDays(options.WeeklyDays));
                break;

            case RecurrenceFrequency.Monthly when options.MonthlyPattern == MonthlyPattern.ByWeekday:
                builder
                    .Append(";BYDAY=")
                    .Append(options.MonthlyWeekdayOrdinal.ToString(CultureInfo.InvariantCulture))
                    .Append(DayCodes[(int)options.MonthlyWeekday]);
                break;

            case RecurrenceFrequency.Monthly:
                builder.Append(";BYMONTHDAY=").Append(options.MonthlyDay.ToString(CultureInfo.InvariantCulture));
                break;

            case RecurrenceFrequency.Yearly:
                builder.Append(";BYMONTH=").Append(options.YearlyMonth.ToString(CultureInfo.InvariantCulture));
                builder.Append(";BYMONTHDAY=").Append(options.MonthlyDay.ToString(CultureInfo.InvariantCulture));
                break;
        }

        switch (options.End)
        {
            case RecurrenceEnd.OnDate when options.EndDate is { } endDate:
                builder.Append(";UNTIL=").Append(endDate.ToString("yyyyMMdd'T'235959", CultureInfo.InvariantCulture));
                break;

            case RecurrenceEnd.AfterCount when options.Count is > 0:
                builder.Append(";COUNT=").Append(options.Count.Value.ToString(CultureInfo.InvariantCulture));
                break;
        }

        return builder.ToString();
    }

    public static bool TryParse(string? rRule, out RecurrenceRuleOptions options)
    {
        options = new RecurrenceRuleOptions();

        if (string.IsNullOrWhiteSpace(rRule))
        {
            return false;
        }

        rRule = rRule.Trim();
        if (rRule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
        {
            rRule = rRule["RRULE:".Length..];
        }

        var frequency = RecurrenceFrequency.None;
        var interval = 1;
        var weeklyDays = new List<DayOfWeek>();
        var monthlyDay = 0;
        var monthlyOrdinal = 0;
        var monthlyWeekday = DayOfWeek.Monday;
        var monthlyPattern = MonthlyPattern.ByDate;
        var yearlyMonth = 0;
        var end = RecurrenceEnd.Never;
        DateOnly? endDate = null;
        int? count = null;

        foreach (var part in rRule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            var name = part[..separator].ToUpperInvariant();
            var value = part[(separator + 1)..].Trim();

            switch (name)
            {
                case "FREQ":
                    frequency = value.ToUpperInvariant() switch
                    {
                        "DAILY" => RecurrenceFrequency.Daily,
                        "WEEKLY" => RecurrenceFrequency.Weekly,
                        "MONTHLY" => RecurrenceFrequency.Monthly,
                        "YEARLY" => RecurrenceFrequency.Yearly,
                        _ => RecurrenceFrequency.None,
                    };
                    break;

                case "INTERVAL":
                    interval = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInterval)
                        ? Math.Max(1, parsedInterval)
                        : 1;
                    break;

                case "BYDAY":
                    foreach (var code in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (TryParseWeekday(code, out var ordinal, out var weekday))
                        {
                            if (ordinal != 0)
                            {
                                monthlyOrdinal = ordinal;
                                monthlyWeekday = weekday;
                                monthlyPattern = MonthlyPattern.ByWeekday;
                            }
                            else
                            {
                                weeklyDays.Add(weekday);
                            }
                        }
                    }

                    break;

                case "BYMONTHDAY":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day))
                    {
                        monthlyDay = day;
                        monthlyPattern = MonthlyPattern.ByDate;
                    }

                    break;

                case "BYMONTH":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month))
                    {
                        yearlyMonth = month;
                    }

                    break;

                case "COUNT":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCount))
                    {
                        count = parsedCount;
                        end = RecurrenceEnd.AfterCount;
                    }

                    break;

                case "UNTIL":
                    if (TryParseUntil(value, out var until))
                    {
                        endDate = until;
                        end = RecurrenceEnd.OnDate;
                    }

                    break;
            }
        }

        if (frequency == RecurrenceFrequency.None)
        {
            return false;
        }

        options = new RecurrenceRuleOptions
        {
            Frequency = frequency,
            Interval = interval,
            WeeklyDays = weeklyDays.Distinct().OrderBy(day => day).ToList(),
            MonthlyPattern = monthlyPattern,
            MonthlyDay = monthlyDay == 0 ? 1 : monthlyDay,
            MonthlyWeekdayOrdinal = monthlyOrdinal == 0 ? 1 : monthlyOrdinal,
            MonthlyWeekday = monthlyWeekday,
            YearlyMonth = yearlyMonth == 0 ? 1 : yearlyMonth,
            End = end,
            EndDate = endDate,
            Count = count,
        };

        return true;
    }

    public static string Describe(RecurrenceRuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Frequency switch
        {
            RecurrenceFrequency.None => "Does not repeat",
            RecurrenceFrequency.Daily => options.Interval > 1
                ? $"Every {options.Interval} days"
                : "Every day",
            RecurrenceFrequency.Weekly => options.Interval > 1
                ? $"Every {options.Interval} weeks on {JoinDayNames(options.WeeklyDays)}"
                : $"Every week on {JoinDayNames(options.WeeklyDays)}",
            RecurrenceFrequency.Monthly when options.MonthlyPattern == MonthlyPattern.ByWeekday =>
                $"Monthly on the {OrdinalName(options.MonthlyWeekdayOrdinal)} {options.MonthlyWeekday}",
            RecurrenceFrequency.Monthly => $"Monthly on day {options.MonthlyDay}",
            RecurrenceFrequency.Yearly =>
                $"Every year on {new DateTime(options.YearlyMonth, 1, 1).ToString("MMMM", CultureInfo.CurrentCulture)} {options.MonthlyDay}",
            _ => "Does not repeat",
        };
    }

    private static string JoinDays(IEnumerable<DayOfWeek> days)
    {
        var codes = days
            .Distinct()
            .OrderBy(day => day)
            .Select(day => DayCodes[(int)day]);

        return string.Join(',', codes);
    }

    private static string JoinDayNames(IEnumerable<DayOfWeek> days)
    {
        var names = days
            .Distinct()
            .OrderBy(day => day)
            .Select(day => day.ToString());

        return string.Join(", ", names);
    }

    private static string OrdinalName(int ordinal) => ordinal switch
    {
        -1 => "last",
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        _ => ordinal.ToString(CultureInfo.CurrentCulture),
    };

    private static bool TryParseWeekday(string code, out int ordinal, out DayOfWeek weekday)
    {
        ordinal = 0;
        weekday = DayOfWeek.Sunday;

        var text = code.ToUpperInvariant();

        if (text.Length < 2)
        {
            return false;
        }

        var dayCode = text[^2..];
        var index = Array.IndexOf(DayCodes, dayCode);

        if (index < 0)
        {
            return false;
        }

        weekday = (DayOfWeek)index;

        var prefix = text[..^2];

        if (prefix.Length > 0 &&
            int.TryParse(prefix, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            ordinal = parsed;
        }

        return true;
    }

    private static bool TryParseUntil(string value, out DateOnly date)
    {
        date = default;

        var text = value.Trim().TrimEnd('Z');

        if (text.Length < 8)
        {
            return false;
        }

        var datePart = text[..8];

        return DateOnly.TryParseExact(
            datePart,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }
}
