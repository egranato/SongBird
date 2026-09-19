using SongBird.Core.Models;
using SongBird.Core.Scanning;

namespace SongBird.Core.Abstractions;

public interface IMediaScanner
{
    Task<ScanResult> ScanAsync(
        Library library,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
