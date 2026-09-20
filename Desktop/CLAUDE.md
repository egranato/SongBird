# Desktop

Avalonia (C#/.NET) desktop music library + player, targeting Windows and macOS from one codebase. Phase 0 (scaffold), Phase 1 (library + scanner), and Phase 2 (library browsing) are done. Update this file if the actual structure ever diverges from it.

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

### What's in Desktop (as of Phase 2)

- `Services/IFolderPickerService` + `FolderPickerService` — wraps Avalonia's `Window.StorageProvider` behind an interface so ViewModels don't depend on a `Window`. Registered in DI with the real `MainWindow` instance (see `App.axaml.cs` — the window is constructed *before* `BuildServices` for this reason).
- `Converters/DurationConverter` — `TimeSpan` → `m:ss` / `h:mm:ss` for display. **Known Avalonia gotcha**: a `Grid` with star-sized (`*`) columns inside content that isn't itself width-constrained (e.g. a `ListBox.ItemTemplate` with default `HorizontalContentAlignment`) silently drops the content in columns after the star columns — no error, no warning, items after the star columns just don't render. Fixed by using a `StackPanel` with fixed-`Width` `TextBlock`s instead (see `TrackItemTemplate` in `LibraryView.axaml`). Don't reach for star-sized Grid columns inside list item templates without testing at runtime.
- `ViewModels/MainViewModel` — app shell; on startup picks `CreateLibraryViewModel` (no library yet) or `LibraryViewModel` (library exists) and sets it as `CurrentPage`. `MainWindow.axaml` is just a `ContentControl` bound to `CurrentPage`; `ViewLocator` resolves the matching View by naming convention (`FooViewModel` → `FooView`).
- `ViewModels/CreateLibraryViewModel` — first-run folder picker → saves `Library` → runs the Phase 1 scanner with progress → raises `LibraryReady`.
- `ViewModels/LibraryViewModel` — owns `Songs`/`Albums`/`Artists` (grouped from the loaded tracks), `SearchText` filtering, and drilldown (`SelectedAlbum`/`SelectedArtist` → `DrilldownTracks`). Single-library scope for now (always loads `GetLibrariesAsync()[0]`).
- `ViewModels/PlayerViewModel` — DI singleton holding `NowPlaying`. Double-click in `LibraryView` sets this; no audio yet (Phase 3 wires a real player in behind the same command).
- Track rows use `DoubleTapped` in code-behind (`LibraryView.axaml.cs`) rather than a binding, since Avalonia has no built-in double-click-to-command binding — this is the one place View code-behind is expected, not a pattern to generalize.

### Theme

Dark-first, hawk-headed-parrot-inspired palette (charcoal + crimson + restrained feather blue/green). ~85-90% neutral, accents used only for their named semantic purpose — never assign a saturated color to a control just because it needs *a* color.

- `Theme/Palette.axaml` — the single source of truth: `Color.*` values plus matching `Brush.*` (`Background`, `Surface`, `SurfaceElevated`, `Border`, `TextPrimary`, `TextSecondary`, `AccentPrimary` = crimson/selection/primary actions, `AccentSecondary` = feather blue/secondary emphasis, `Success` = sync/success states, `Warning`/`Error` = conventional amber/red, deliberately *not* bird-themed so they stay unambiguous). Adding a new UI color means adding it here, not inlining a hex value in a View.
- `Theme/ControlStyles.axaml` — base `Styles` (Window/TextBlock/Button/TextBox/ListBox/TabControl/TabItem) that reference the palette via `DynamicResource`. Both files are merged into `App.axaml`.
- `App.axaml` also overrides FluentTheme's documented accent customization points (`SystemAccentColor` + the `Light1-3`/`Dark1-3` ramp) with `AccentPrimary`'s values, so built-in accent-driven visuals (selection highlight, focus rings, `TabItem` selected indicator) pick up the crimson automatically instead of Fluent's default blue.
- `RequestedThemeVariant="Dark"` is explicit, not following the OS theme — this app is dark-first by design, not incidentally dark because the last dev's OS was in dark mode.

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
