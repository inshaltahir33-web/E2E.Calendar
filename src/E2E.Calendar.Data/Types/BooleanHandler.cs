using System.Data;
using Dapper;

namespace E2E.Calendar.Data.Types;

public sealed class BooleanHandler : SqlMapper.TypeHandler<bool>
{
    public override bool Parse(object value) => value switch
    {
        bool flag => flag,
        long number => number != 0,
        int number => number != 0,
        string text => text is "1" or "true" or "True",
        _ => throw new InvalidOperationException($"Cannot convert '{value.GetType()}' to Boolean."),
    };

    public override void SetValue(IDbDataParameter parameter, bool value)
    {
        parameter.DbType = DbType.Int64;
        parameter.Value = value ? 1L : 0L;
    }
}
