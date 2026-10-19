using System.Data;
using Dapper;

namespace E2E.Calendar.Data.Types;

public sealed class TextEnumHandler<TEnum> : SqlMapper.TypeHandler<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Parse(object value) => value switch
    {
        string text => Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) ? parsed : default,
        long number => (TEnum)Enum.ToObject(typeof(TEnum), (int)number),
        int number => (TEnum)Enum.ToObject(typeof(TEnum), number),
        _ => throw new InvalidOperationException($"Cannot convert '{value.GetType()}' to {typeof(TEnum).Name}."),
    };

    public override void SetValue(IDbDataParameter parameter, TEnum value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString().ToLowerInvariant();
    }
}
