using SongBird.Core.Abstractions;
using SongBird.Core.Metadata;

namespace SongBird.Infrastructure.Metadata;

public sealed class TagLibMetadataReader : IMetadataReader
{
    public AudioFileMetadata Read(string absoluteFilePath)
    {
        using var file = TagLib.File.Create(absoluteFilePath);
        var tag = file.Tag;

        return new AudioFileMetadata
        {
            Title = NullIfEmpty(tag.Title),
            Artist = NullIfEmpty(tag.FirstPerformer),
            Album = NullIfEmpty(tag.Album),
            AlbumArtist = NullIfEmpty(tag.FirstAlbumArtist),
            TrackNumber = tag.Track == 0 ? null : (int)tag.Track,
            DiscNumber = tag.Disc == 0 ? null : (int)tag.Disc,
            Year = tag.Year == 0 ? null : (int)tag.Year,
            Genre = NullIfEmpty(tag.FirstGenre),
            Duration = file.Properties.Duration,
            Format = Path.GetExtension(absoluteFilePath).TrimStart('.').ToLowerInvariant(),
            Artwork = tag.Pictures.Length > 0 ? tag.Pictures[0].Data.Data : null,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
