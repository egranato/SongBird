using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SongBird.Core.Models;
using SongBird.Infrastructure.Metadata;
using SongBird.Infrastructure.Persistence;
using SongBird.Infrastructure.Scanning;
using SongBird.Tests.TestSupport;

namespace SongBird.Tests.Scanning;

public sealed class FileSystemMediaScannerTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"songbird-scan-{Guid.NewGuid():N}");
    private readonly string _dbPath;
    private readonly SqliteLibraryRepository _repository;
    private readonly FileSystemMediaScanner _scanner;
    private readonly Library _library;

    public FileSystemMediaScannerTests()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "library.db");
        _repository = new SqliteLibraryRepository(_dbPath);
        _scanner = new FileSystemMediaScanner(_repository, new TagLibMetadataReader(), NullLogger<FileSystemMediaScanner>.Instance);
        _library = new Library { Id = Guid.NewGuid(), Name = "Test", RootPath = _root };
    }

    public Task InitializeAsync() => _repository.SaveLibraryAsync(_library);

    [Fact]
    public async Task Initial_scan_indexes_supported_files_with_tags_and_duration()
    {
        WavFixture.Create(Path.Combine(_root, "song.wav"), title: "Song One", artist: "Artist", album: "Album", durationSeconds: 0.5);

        var result = await _scanner.ScanAsync(_library);

        Assert.Equal(1, result.TracksAdded);
        Assert.Empty(result.Errors);

        var track = Assert.Single(await _repository.GetTracksAsync(_library.Id));
        Assert.Equal("Song One", track.Title);
        Assert.Equal("Artist", track.Artist);
        Assert.Equal("Album", track.Album);
        Assert.Equal("wav", track.Format);
        Assert.Equal("song.wav", track.RelativePath);
        Assert.True(track.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task Rescan_with_no_changes_is_a_no_op_and_preserves_identity()
    {
        WavFixture.Create(Path.Combine(_root, "song.wav"), title: "Song");
        await _scanner.ScanAsync(_library);
        var originalId = Assert.Single(await _repository.GetTracksAsync(_library.Id)).Id;

        var result = await _scanner.ScanAsync(_library);

        Assert.Equal(0, result.TracksAdded);
        Assert.Equal(0, result.TracksUpdated);
        Assert.Equal(0, result.TracksMoved);
        Assert.Equal(0, result.TracksRemoved);
        Assert.Equal(originalId, Assert.Single(await _repository.GetTracksAsync(_library.Id)).Id);
    }

    [Fact]
    public async Task Editing_a_file_in_place_updates_the_same_track()
    {
        var path = Path.Combine(_root, "song.wav");
        WavFixture.Create(path, title: "Original Title");
        await _scanner.ScanAsync(_library);
        var originalId = Assert.Single(await _repository.GetTracksAsync(_library.Id)).Id;

        WavFixture.Create(path, title: "New Title");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        var result = await _scanner.ScanAsync(_library);

        Assert.Equal(0, result.TracksAdded);
        Assert.Equal(1, result.TracksUpdated);
        var track = Assert.Single(await _repository.GetTracksAsync(_library.Id));
        Assert.Equal(originalId, track.Id);
        Assert.Equal("New Title", track.Title);
    }

    [Fact]
    public async Task Moving_a_file_to_a_new_relative_path_preserves_its_identity()
    {
        var oldPath = Path.Combine(_root, "old-name.wav");
        WavFixture.Create(oldPath, title: "Moved Song", artist: "Artist", album: "Album");
        await _scanner.ScanAsync(_library);
        var originalId = Assert.Single(await _repository.GetTracksAsync(_library.Id)).Id;

        var newDir = Path.Combine(_root, "Artist", "Album");
        Directory.CreateDirectory(newDir);
        File.Move(oldPath, Path.Combine(newDir, "new-name.wav"));

        var result = await _scanner.ScanAsync(_library);

        Assert.Equal(0, result.TracksAdded);
        Assert.Equal(1, result.TracksMoved);
        Assert.Equal(0, result.TracksRemoved);
        var track = Assert.Single(await _repository.GetTracksAsync(_library.Id));
        Assert.Equal(originalId, track.Id);
        Assert.Equal(Path.Combine("Artist", "Album", "new-name.wav"), track.RelativePath);
    }

    [Fact]
    public async Task Deleting_a_file_removes_its_track()
    {
        var path = Path.Combine(_root, "song.wav");
        WavFixture.Create(path, title: "Gone Soon");
        await _scanner.ScanAsync(_library);

        File.Delete(path);
        var result = await _scanner.ScanAsync(_library);

        Assert.Equal(1, result.TracksRemoved);
        Assert.Empty(await _repository.GetTracksAsync(_library.Id));
    }

    [Fact]
    public async Task Corrupt_file_is_skipped_without_aborting_the_scan()
    {
        WavFixture.Create(Path.Combine(_root, "good.wav"), title: "Good Song");
        await File.WriteAllBytesAsync(Path.Combine(_root, "bad.wav"), [0x00, 0x01, 0x02, 0x03]);

        var result = await _scanner.ScanAsync(_library);

        Assert.Equal(1, result.TracksAdded);
        var error = Assert.Single(result.Errors);
        Assert.Equal("bad.wav", error.RelativePath);

        var track = Assert.Single(await _repository.GetTracksAsync(_library.Id));
        Assert.Equal("Good Song", track.Title);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return Task.CompletedTask;
    }
}
