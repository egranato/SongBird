namespace SongBird.Core.Scanning;

public sealed class ScanProgress
{
    public required int FilesProcessed { get; init; }
    public required int TotalFiles { get; init; }
    public required string CurrentRelativePath { get; init; }
}
