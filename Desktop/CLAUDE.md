# Desktop

Avalonia (C#/.NET) desktop music library + player, targeting Windows and macOS from one codebase. Phase 0 (scaffold) and Phase 1 (library + scanner) are done. Update this file if the actual structure ever diverges from it.

Current phase: see [Docs/2 Desktop Roadmap.md](../Docs/2%20Desktop%20Roadmap.md). Don't build anything from a later phase, or anything on the "not in 0.1" list there, without checking with the user first.

## Solution layout

```
SongBird.slnx             SDK-generated solution file (XML format, replaces .sln)
SongBird.Core             domain model, no Avalonia/UI refs, no third-party deps
SongBird.Infrastructure   SQLite, filesystem, tag parsing, hashing, artwork extraction, library manifests
SongBird.Playback         IPlaybackService implementation(s), wraps the chosen audio backend — currently empty (Phase 3)
SongBird.Desktop          Avalonia: Views, ViewModels, Navigation, Commands, desktop-specific services. Composition root (DI + logging) lives in App.axaml.cs.
SongBird.Tests            xUnit
```

Project references: `Infrastructure` → `Core`; `Playback` → `Core`; `Desktop` → all three; `Tests` → `Core`/`Infrastructure`/`Playback` (not `Desktop` — UI isn't the test priority, see below).

New code goes in the project matching that table — don't search for precedent, this is the precedent.

### What's in Core (as of Phase 1)

- `Models/`: `Library` (Id, Name, RootPath), `Track` (Id, LibraryId, RelativePath, FileSize, FileLastWriteTimeUtc, FileHash, Duration, Format, tag fields, DateAddedUtc)
- `Metadata/AudioFileMetadata`: what an `IMetadataReader` returns for one file — tag fields + `Artwork` bytes
- `Scanning/`: `ScanProgress`, `ScanResult`/`ScanError`, `SupportedAudioExtensions` (.mp3/.flac/.m4a/.wav/.ogg)
- `Abstractions/`: `ILibraryRepository`, `IMetadataReader`, `IMediaScanner` (`IPlaybackService` doesn't exist yet — Phase 3)

### What's in Infrastructure (as of Phase 1)

- `Persistence/SqliteLibraryRepository` — implements `ILibraryRepository`, creates its own schema on first use, one connection per operation (pooled by the driver)
- `Metadata/TagLibMetadataReader` — implements `IMetadataReader` via TagLibSharp
- `Scanning/FileSystemMediaScanner` — implements `IMediaScanner`; runs the whole scan via `Task.Run` internally so callers never need to remember to background it. Handles the identity/move-detection logic described below.

Look at these four files before writing anything scanner- or persistence-adjacent — the incremental-scan and move-detection algorithm is non-obvious and already solved there.

## Pinned stack decisions (don't re-litigate)

- C# / .NET 10, Avalonia 12 (CommunityToolkit.Mvvm), MVVM-ish
- SQLite via `Microsoft.Data.Sqlite` + Dapper — not EF Core
- TagLibSharp for tag reading
- `Microsoft.Extensions.DependencyInjection` for DI — wired in `SongBird.Desktop/App.axaml.cs` (`BuildServices()`); register new services there
- `Microsoft.Extensions.Logging` (console provider) for logging — already wired
- xUnit for tests

## Hard domain rules

- `Track` stores `RelativePath` (relative to the library root); `Library` stores the host-specific `RootPath`. Never persist an absolute track path — resolve it at runtime as `Library.RootPath + Track.RelativePath`. This is what makes a library portable across machines/mount points later.
- Library and Track IDs are generated independently of filesystem path. Moving/renaming a file within the library must be recognized as the same track at a new path (via hash/tag-based matching), not scanned in as a new track.
- Metadata edits are an overlay on top of file tags (`TrackField<T>`-style: original value vs. effective value). Each edited field stores `value`, `modifiedAtUtc`, and `modifiedByDeviceId`. **Never write edits back into the source audio file.**
- Shuffle is a precomputed permutation of the queue, advanced through in order. Never pick the next track at random on each call.
- `IPlaybackService` plays media and reports state (`Load`/`Play`/`Pause`/`Stop`/`Seek`/`Volume`, events `PlaybackEnded`/`PositionChanged`/`StateChanged`) — it has no queue awareness and never decides what plays next. Next/Previous/Repeat/Shuffle live in the queue/orchestration layer, which reacts to `PlaybackEnded`.
- Restoring queue/playback state on app restart must never auto-start playback.
- The metadata reader reports embedded artwork as part of its scan output, even though display/caching/dedup land later (Phase 6).
- A corrupt or unsupported file must never abort a library scan — log it and continue.
- The working SQLite DB lives on the local host's app data, not a network share/NAS path (SQLite's WAL mode assumes same-host coordination).

## What to prioritize in tests

Library scanning, file identity, metadata merge, queue construction, shuffle, repeat, persistence. The UI is replaceable and not the priority.
