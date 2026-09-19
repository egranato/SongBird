namespace SongBird.Core.Metadata;

/// <summary>Tags read from a single audio file, as reported by an IMetadataReader.</summary>
public sealed class AudioFileMetadata
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? AlbumArtist { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public int? Year { get; init; }
    public string? Genre { get; init; }
    public TimeSpan Duration { get; init; }
    public required string Format { get; init; }

    /// <summary>Embedded artwork bytes, if any. Caching/dedup is a later phase; readers must still report it.</summary>
    public byte[]? Artwork { get; init; }
}
