namespace SongBird.Core.Scanning;

public sealed class ScanResult
{
    public int TracksAdded { get; init; }
    public int TracksUpdated { get; init; }
    public int TracksMoved { get; init; }
    public int TracksRemoved { get; init; }
    public IReadOnlyList<ScanError> Errors { get; init; } = [];
}

/// <summary>A single file that couldn't be read during a scan. The scan itself still completes.</summary>
public sealed class ScanError
{
    public required string RelativePath { get; init; }
    public required string Message { get; init; }
}
