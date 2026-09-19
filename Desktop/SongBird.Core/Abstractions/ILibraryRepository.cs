using SongBird.Core.Models;

namespace SongBird.Core.Abstractions;

public interface ILibraryRepository
{
    Task<Library?> GetLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Library>> GetLibrariesAsync(CancellationToken cancellationToken = default);
    Task SaveLibraryAsync(Library library, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Track>> GetTracksAsync(Guid libraryId, CancellationToken cancellationToken = default);
    Task UpsertTrackAsync(Track track, CancellationToken cancellationToken = default);
    Task DeleteTrackAsync(Guid trackId, CancellationToken cancellationToken = default);
}
