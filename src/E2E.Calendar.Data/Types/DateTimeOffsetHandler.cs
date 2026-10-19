using System.Data;
using Dapper;

namespace E2E.Calendar.Data.Types;

public sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) => value switch
    {
        string text => SqliteTime.Parse(text),
        long unixMilliseconds => DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds),
        DateTimeOffset offset => offset,
        DateTime dateTime => new DateTimeOffset(dateTime, TimeSpan.Zero),
        _ => throw new InvalidOperationException($"Cannot convert '{value.GetType()}' to DateTimeOffset."),
    };

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = SqliteTime.ToUtcText(value);
    }
}
