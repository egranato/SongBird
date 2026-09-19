using SongBird.Core.Metadata;

namespace SongBird.Core.Abstractions;

public interface IMetadataReader
{
    AudioFileMetadata Read(string absoluteFilePath);
}
