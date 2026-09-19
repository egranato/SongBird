using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SongBird.Core.Abstractions;
using SongBird.Core.Metadata;
using SongBird.Core.Models;
using SongBird.Core.Scanning;

namespace SongBird.Infrastructure.Scanning;

public sealed class FileSystemMediaScanner(
    ILibraryRepository repository,
    IMetadataReader metadataReader,
    ILogger<FileSystemMediaScanner> logger) : IMediaScanner
{
    // Runs the whole scan on a thread-pool thread so a caller can `await` this directly
    // from a UI thread without it blocking - see Desktop/CLAUDE.md's "must not block UI" rule.
    public Task<ScanResult> ScanAsync(
        Library library,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => ScanCoreAsync(library, progress, cancellationToken), cancellationToken);

    private async Task<ScanResult> ScanCoreAsync(
        Library library,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var existingTracks = await repository.GetTracksAsync(library.Id, cancellationToken);
        var byRelativePath = existingTracks.ToDictionary(t => t.RelativePath, StringComparer.OrdinalIgnoreCase);
        var seenRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var files = EnumerateSupportedFiles(library.RootPath).ToList();
        var errors = new List<ScanError>();
        int added = 0, updated = 0, moved = 0;

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var absolutePath = files[i];
            var relativePath = Path.GetRelativePath(library.RootPath, absolutePath);
            seenRelativePaths.Add(relativePath);

            progress?.Report(new ScanProgress
            {
                FilesProcessed = i,
                TotalFiles = files.Count,
                CurrentRelativePath = relativePath,
            });

            FileInfo info;
            try
            {
                info = new FileInfo(absolutePath);
            }
            catch (IOException ex)
            {
                LogAndRecordError(relativePath, ex, errors);
                continue;
            }

            if (byRelativePath.TryGetValue(relativePath, out var trackAtThisPath))
            {
                if (trackAtThisPath.FileSize == info.Length
                    && trackAtThisPath.FileLastWriteTimeUtc == info.LastWriteTimeUtc)
                {
                    continue;
                }

                if (!TryReadMetadata(absolutePath, relativePath, errors, out var changedMetadata))
                {
                    continue;
                }

                ApplyMetadata(trackAtThisPath, changedMetadata, info);
                trackAtThisPath.FileHash = ComputeHash(absolutePath);
                await repository.UpsertTrackAsync(trackAtThisPath, cancellationToken);
                updated++;
                continue;
            }

            if (!TryReadMetadata(absolutePath, relativePath, errors, out var metadata))
            {
                continue;
            }

            var hash = ComputeHash(absolutePath);
            var moveCandidate = FindMoveCandidate(existingTracks, seenRelativePaths, hash, metadata, info);

            if (moveCandidate is not null)
            {
                moveCandidate.RelativePath = relativePath;
                ApplyMetadata(moveCandidate, metadata, info);
                moveCandidate.FileHash = hash;
                await repository.UpsertTrackAsync(moveCandidate, cancellationToken);
                byRelativePath[relativePath] = moveCandidate;
                moved++;
                continue;
            }

            var newTrack = new Track
            {
                Id = Guid.NewGuid(),
                LibraryId = library.Id,
                RelativePath = relativePath,
                FileHash = hash,
                Format = metadata.Format,
                DateAddedUtc = DateTime.UtcNow,
            };
            ApplyMetadata(newTrack, metadata, info);
            await repository.UpsertTrackAsync(newTrack, cancellationToken);
            byRelativePath[relativePath] = newTrack;
            added++;
        }

        var removed = 0;
        foreach (var track in existingTracks)
        {
            if (!seenRelativePaths.Contains(track.RelativePath))
            {
                await repository.DeleteTrackAsync(track.Id, cancellationToken);
                removed++;
            }
        }

        return new ScanResult
        {
            TracksAdded = added,
            TracksUpdated = updated,
            TracksMoved = moved,
            TracksRemoved = removed,
            Errors = errors,
        };
    }

    /// Among tracks not yet matched this scan, prefer an exact content-hash match; fall back to
    /// a tag+size+duration match so a move is still recognized even if the old hash was never computed.
    private static Track? FindMoveCandidate(
        IReadOnlyList<Track> existingTracks,
        HashSet<string> seenRelativePaths,
        string fileHash,
        AudioFileMetadata metadata,
        FileInfo info)
    {
        Track? tagMatch = null;

        foreach (var candidate in existingTracks)
        {
            if (seenRelativePaths.Contains(candidate.RelativePath))
            {
                continue;
            }

            if (candidate.FileHash is not null && candidate.FileHash == fileHash)
            {
                return candidate;
            }

            if (tagMatch is null
                && candidate.FileSize == info.Length
                && candidate.Duration == metadata.Duration
                && string.Equals(candidate.Title, metadata.Title, StringComparison.Ordinal)
                && string.Equals(candidate.Artist, metadata.Artist, StringComparison.Ordinal)
                && string.Equals(candidate.Album, metadata.Album, StringComparison.Ordinal))
            {
                tagMatch = candidate;
            }
        }

        return tagMatch;
    }

    private static void ApplyMetadata(Track track, AudioFileMetadata metadata, FileInfo info)
    {
        track.FileSize = info.Length;
        track.FileLastWriteTimeUtc = info.LastWriteTimeUtc;
        track.Duration = metadata.Duration;
        track.Title = metadata.Title;
        track.Artist = metadata.Artist;
        track.Album = metadata.Album;
        track.AlbumArtist = metadata.AlbumArtist;
        track.TrackNumber = metadata.TrackNumber;
        track.DiscNumber = metadata.DiscNumber;
        track.Year = metadata.Year;
        track.Genre = metadata.Genre;
    }

    // A corrupt or unsupported file must never abort the scan - log it, record it, and move on.
    private bool TryReadMetadata(
        string absolutePath,
        string relativePath,
        List<ScanError> errors,
        out AudioFileMetadata metadata)
    {
        try
        {
            metadata = metadataReader.Read(absolutePath);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAndRecordError(relativePath, ex, errors);
            metadata = null!;
            return false;
        }
    }

    private void LogAndRecordError(string relativePath, Exception ex, List<ScanError> errors)
    {
        logger.LogWarning(ex, "Skipping unreadable file {RelativePath}", relativePath);
        errors.Add(new ScanError { RelativePath = relativePath, Message = ex.Message });
    }

    private static string ComputeHash(string absolutePath)
    {
        using var stream = File.OpenRead(absolutePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    // Walks manually (rather than Directory.EnumerateFiles(..., AllDirectories)) so one
    // unreadable subdirectory can't abort the whole scan.
    private static IEnumerable<string> EnumerateSupportedFiles(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            List<string> subdirectories;
            List<string> files;

            try
            {
                subdirectories = Directory.EnumerateDirectories(directory).ToList();
                files = Directory.EnumerateFiles(directory).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                pending.Push(subdirectory);
            }

            foreach (var file in files)
            {
                if (SupportedAudioExtensions.Values.Contains(Path.GetExtension(file)))
                {
                    yield return file;
                }
            }
        }
    }
}
