using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SongBird.Desktop.Converters;

public sealed class DurationConverter : IValueConverter
{
    public static readonly DurationConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan duration)
        {
            return null;
        }

        return duration.Hours > 0
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
