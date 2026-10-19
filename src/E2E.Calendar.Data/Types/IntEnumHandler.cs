using System.Data;
using System.Globalization;
using Dapper;

namespace E2E.Calendar.Data.Types;

public sealed class IntEnumHandler<TEnum> : SqlMapper.TypeHandler<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Parse(object value) => value switch
    {
        long number => (TEnum)Enum.ToObject(typeof(TEnum), (int)number),
        int number => (TEnum)Enum.ToObject(typeof(TEnum), number),
        string text => (TEnum)Enum.ToObject(typeof(TEnum), int.Parse(text, CultureInfo.InvariantCulture)),
        _ => throw new InvalidOperationException($"Cannot convert '{value.GetType()}' to {typeof(TEnum).Name}."),
    };

    public override void SetValue(IDbDataParameter parameter, TEnum value)
    {
        parameter.DbType = DbType.Int32;
        parameter.Value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}
