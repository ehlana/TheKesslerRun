using System;
using System.Globalization;
using System.Windows.Data;

namespace TheKesslerRun2.ResourceEditor.Converters;

internal sealed class NullToBooleanConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is not null;
        return Invert ? !result : result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
