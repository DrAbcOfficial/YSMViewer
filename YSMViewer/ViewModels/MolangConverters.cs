using Avalonia.Data.Converters;
using Avalonia.Media;
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

public sealed class DomainToBadgeBrushConverter : IValueConverter
{
    private static readonly IBrush QueryBrush = new SolidColorBrush(Color.FromRgb(0x58, 0xa6, 0xff));
    private static readonly IBrush VariableBrush = new SolidColorBrush(Color.FromRgb(0x3f, 0xb9, 0x50));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(0x8b, 0x94, 0x9e));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string domain)
        {
            return domain.ToLowerInvariant() switch
            {
                "query" => QueryBrush,
                "variable" => VariableBrush,
                _ => DefaultBrush,
            };
        }
        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class FloatToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is float f)
        {
            if (Math.Abs(f) < 0.001f) return "0";
            if (Math.Abs(f - Math.Round(f)) < 0.001f) return ((int)Math.Round(f)).ToString();
            return f.ToString("F2");
        }
        return "0";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            return f;
        return 0f;
    }
}