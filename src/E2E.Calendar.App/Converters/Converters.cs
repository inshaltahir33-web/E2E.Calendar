using E2E.Calendar.App.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace E2E.Calendar.App.Converters;

public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var color = value as string;
        var role = parameter as string ?? "Bar";
        return CalendarColors.Resolve(null, color, role);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is bool boolean && boolean;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
