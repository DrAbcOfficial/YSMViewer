using Avalonia.Data.Converters;
using System.Globalization;

namespace YSMViewer.ViewModels;

public sealed class FloatToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is float f)
            return f > 0.5f;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? 1f : 0f;
        return 0f;
    }
}

public sealed class BoolToFloatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? 1f : 0f;
        return 0f;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is float f)
            return f > 0.5f;
        return false;
    }
}