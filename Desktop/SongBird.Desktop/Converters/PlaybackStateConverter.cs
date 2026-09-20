using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SongBird.Core.Playback;

namespace SongBird.Desktop.Converters;

/// <summary>State -> the label for the transport button (what pressing it will do next).</summary>
public sealed class PlaybackStateConverter : IValueConverter
{
    public static readonly PlaybackStateConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PlaybackState.Playing ? "Pause" : "Play";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
