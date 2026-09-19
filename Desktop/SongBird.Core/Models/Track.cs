namespace SongBird.Core.Models;

public sealed class Track
{
    public required Guid Id { get; init; }
    public required Guid LibraryId { get; init; }

    /// <summary>Relative to the owning Library's RootPath. Never store an absolute path here.</summary>
    public required string RelativePath { get; set; }

    public long FileSize { get; set; }
    public DateTime FileLastWriteTimeUtc { get; set; }

    /// <summary>SHA-256 of the file contents; only computed when identity can't be established from RelativePath alone.</summary>
    public string? FileHash { get; set; }

    public TimeSpan Duration { get; set; }
    public required string Format { get; set; }

    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? AlbumArtist { get; set; }
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public int? Year { get; set; }
    public string? Genre { get; set; }

    public required DateTime DateAddedUtc { get; set; }
}
