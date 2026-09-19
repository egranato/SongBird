using Dapper;
using Microsoft.Data.Sqlite;
using SongBird.Core.Abstractions;
using SongBird.Core.Models;

namespace SongBird.Infrastructure.Persistence;

public sealed class SqliteLibraryRepository : ILibraryRepository
{
    private readonly string _connectionString;

    public SqliteLibraryRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = Open();
        connection.Execute(
            """
            CREATE TABLE IF NOT EXISTS Library (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                RootPath TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Track (
                Id TEXT PRIMARY KEY,
                LibraryId TEXT NOT NULL REFERENCES Library(Id),
                RelativePath TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                FileLastWriteTimeUtc TEXT NOT NULL,
                FileHash TEXT NULL,
                DurationTicks INTEGER NOT NULL,
                Format TEXT NOT NULL,
                Title TEXT NULL,
                Artist TEXT NULL,
                Album TEXT NULL,
                AlbumArtist TEXT NULL,
                TrackNumber INTEGER NULL,
                DiscNumber INTEGER NULL,
                Year INTEGER NULL,
                Genre TEXT NULL,
                DateAddedUtc TEXT NOT NULL,
                UNIQUE (LibraryId, RelativePath)
            );
            """);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<Library?> GetLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        var row = await connection.QuerySingleOrDefaultAsync<LibraryRow>(
            new CommandDefinition(
                "SELECT Id, Name, RootPath FROM Library WHERE Id = @Id",
                new { Id = libraryId.ToString() },
                cancellationToken: cancellationToken));

        return row?.ToLibrary();
    }

    public async Task<IReadOnlyList<Library>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        var rows = await connection.QueryAsync<LibraryRow>(
            new CommandDefinition("SELECT Id, Name, RootPath FROM Library", cancellationToken: cancellationToken));

        return rows.Select(r => r.ToLibrary()).ToList();
    }

    public async Task SaveLibraryAsync(Library library, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Library (Id, Name, RootPath) VALUES (@Id, @Name, @RootPath)
                ON CONFLICT(Id) DO UPDATE SET Name = @Name, RootPath = @RootPath
                """,
                new { Id = library.Id.ToString(), library.Name, library.RootPath },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Track>> GetTracksAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        var rows = await connection.QueryAsync<TrackRow>(
            new CommandDefinition(
                "SELECT * FROM Track WHERE LibraryId = @LibraryId",
                new { LibraryId = libraryId.ToString() },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToTrack()).ToList();
    }

    public async Task UpsertTrackAsync(Track track, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        var row = TrackRow.FromTrack(track);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Track (
                    Id, LibraryId, RelativePath, FileSize, FileLastWriteTimeUtc, FileHash,
                    DurationTicks, Format, Title, Artist, Album, AlbumArtist,
                    TrackNumber, DiscNumber, Year, Genre, DateAddedUtc
                ) VALUES (
                    @Id, @LibraryId, @RelativePath, @FileSize, @FileLastWriteTimeUtc, @FileHash,
                    @DurationTicks, @Format, @Title, @Artist, @Album, @AlbumArtist,
                    @TrackNumber, @DiscNumber, @Year, @Genre, @DateAddedUtc
                )
                ON CONFLICT(Id) DO UPDATE SET
                    RelativePath = @RelativePath,
                    FileSize = @FileSize,
                    FileLastWriteTimeUtc = @FileLastWriteTimeUtc,
                    FileHash = @FileHash,
                    DurationTicks = @DurationTicks,
                    Format = @Format,
                    Title = @Title,
                    Artist = @Artist,
                    Album = @Album,
                    AlbumArtist = @AlbumArtist,
                    TrackNumber = @TrackNumber,
                    DiscNumber = @DiscNumber,
                    Year = @Year,
                    Genre = @Genre,
                    DateAddedUtc = @DateAddedUtc
                """,
                row,
                cancellationToken: cancellationToken));
    }

    public async Task DeleteTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        using var connection = Open();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Track WHERE Id = @Id",
                new { Id = trackId.ToString() },
                cancellationToken: cancellationToken));
    }

    private sealed record LibraryRow(string Id, string Name, string RootPath)
    {
        public Library ToLibrary() => new()
        {
            Id = Guid.Parse(Id),
            Name = Name,
            RootPath = RootPath,
        };
    }

    private sealed record TrackRow
    {
        public required string Id { get; init; }
        public required string LibraryId { get; init; }
        public required string RelativePath { get; init; }
        public long FileSize { get; init; }
        public required string FileLastWriteTimeUtc { get; init; }
        public string? FileHash { get; init; }
        public long DurationTicks { get; init; }
        public required string Format { get; init; }
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Album { get; init; }
        public string? AlbumArtist { get; init; }
        public int? TrackNumber { get; init; }
        public int? DiscNumber { get; init; }
        public int? Year { get; init; }
        public string? Genre { get; init; }
        public required string DateAddedUtc { get; init; }

        public static TrackRow FromTrack(Track track) => new()
        {
            Id = track.Id.ToString(),
            LibraryId = track.LibraryId.ToString(),
            RelativePath = track.RelativePath,
            FileSize = track.FileSize,
            FileLastWriteTimeUtc = track.FileLastWriteTimeUtc.ToString("O"),
            FileHash = track.FileHash,
            DurationTicks = track.Duration.Ticks,
            Format = track.Format,
            Title = track.Title,
            Artist = track.Artist,
            Album = track.Album,
            AlbumArtist = track.AlbumArtist,
            TrackNumber = track.TrackNumber,
            DiscNumber = track.DiscNumber,
            Year = track.Year,
            Genre = track.Genre,
            DateAddedUtc = track.DateAddedUtc.ToString("O"),
        };

        public Track ToTrack() => new()
        {
            Id = Guid.Parse(Id),
            LibraryId = Guid.Parse(LibraryId),
            RelativePath = RelativePath,
            FileSize = FileSize,
            FileLastWriteTimeUtc = DateTime.Parse(FileLastWriteTimeUtc).ToUniversalTime(),
            FileHash = FileHash,
            Duration = TimeSpan.FromTicks(DurationTicks),
            Format = Format,
            Title = Title,
            Artist = Artist,
            Album = Album,
            AlbumArtist = AlbumArtist,
            TrackNumber = TrackNumber,
            DiscNumber = DiscNumber,
            Year = Year,
            Genre = Genre,
            DateAddedUtc = DateTime.Parse(DateAddedUtc).ToUniversalTime(),
        };
    }
}
