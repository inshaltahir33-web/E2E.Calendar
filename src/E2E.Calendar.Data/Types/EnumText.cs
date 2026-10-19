namespace E2E.Calendar.Data.Types;

public static class EnumText
{
    public static string ToText<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();
}
