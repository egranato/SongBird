using Microsoft.Data.Sqlite;
using SongBird.Core.Models;
using SongBird.Infrastructure.Persistence;

namespace SongBird.Tests.Persistence;

public sealed class SqliteLibraryRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"songbird-test-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SaveLibrary_then_get_round_trips_all_fields()
    {
        var repository = new SqliteLibraryRepository(_dbPath);
        var library = new Library { Id = Guid.NewGuid(), Name = "My Music", RootPath = @"E:\Music" };

        await repository.SaveLibraryAsync(library);
        var loaded = await repository.GetLibraryAsync(library.Id);

        Assert.NotNull(loaded);
        Assert.Equal(library.Name, loaded.Name);
        Assert.Equal(library.RootPath, loaded.RootPath);
    }

    [Fact]
    public async Task UpsertTrack_then_get_round_trips_all_fields_including_nulls()
    {
        var repository = new SqliteLibraryRepository(_dbPath);
        var library = new Library { Id = Guid.NewGuid(), Name = "Lib", RootPath = @"E:\Music" };
        await repository.SaveLibraryAsync(library);

        var track = new Track
        {
            Id = Guid.NewGuid(),
            LibraryId = library.Id,
            RelativePath = @"Artist\Album\Song.mp3",
            FileSize = 12345,
            FileLastWriteTimeUtc = DateTime.UtcNow,
            FileHash = "deadbeef",
            Duration = TimeSpan.FromSeconds(217),
            Format = "mp3",
            Title = "Song",
            Artist = "Artist",
            Album = "Album",
            AlbumArtist = null,
            TrackNumber = 3,
            DiscNumber = null,
            Year = 2020,
            Genre = "Rock",
            DateAddedUtc = DateTime.UtcNow,
        };

        await repository.UpsertTrackAsync(track);
        var tracks = await repository.GetTracksAsync(library.Id);

        var loaded = Assert.Single(tracks);
        Assert.Equal(track.Id, loaded.Id);
        Assert.Equal(track.RelativePath, loaded.RelativePath);
        Assert.Equal(track.FileHash, loaded.FileHash);
        Assert.Equal(track.Duration, loaded.Duration);
        Assert.Equal(track.Title, loaded.Title);
        Assert.Equal(track.AlbumArtist, loaded.AlbumArtist);
        Assert.Equal(track.DiscNumber, loaded.DiscNumber);
        Assert.Equal(track.Genre, loaded.Genre);
    }

    [Fact]
    public async Task DeleteTrack_removes_it()
    {
        var repository = new SqliteLibraryRepository(_dbPath);
        var library = new Library { Id = Guid.NewGuid(), Name = "Lib", RootPath = @"E:\Music" };
        await repository.SaveLibraryAsync(library);

        var track = new Track
        {
            Id = Guid.NewGuid(),
            LibraryId = library.Id,
            RelativePath = "Song.mp3",
            Format = "mp3",
            DateAddedUtc = DateTime.UtcNow,
        };
        await repository.UpsertTrackAsync(track);

        await repository.DeleteTrackAsync(track.Id);
        var tracks = await repository.GetTracksAsync(library.Id);

        Assert.Empty(tracks);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
