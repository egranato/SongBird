namespace SongBird.Core.Scanning;

public static class SupportedAudioExtensions
{
    public static readonly IReadOnlySet<string> Values = new HashSet<string>(
        [".mp3", ".flac", ".m4a", ".wav", ".ogg"],
        StringComparer.OrdinalIgnoreCase);
}
