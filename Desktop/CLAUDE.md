# Desktop

Avalonia (C#/.NET) desktop music library + player, targeting Windows and macOS from one codebase. Phase 0 (scaffold), Phase 1 (library + scanner), Phase 2 (library browsing), and Phase 3 (playback engine) are done. Update this file if the actual structure ever diverges from it.

Current phase: see [Docs/2 Desktop Roadmap.md](../Docs/2%20Desktop%20Roadmap.md). Don't build anything from a later phase, or anything on the "not in 0.1" list there, without checking with the user first.

## Solution layout

```
SongBird.slnx             SDK-generated solution file (XML format, replaces .sln)
SongBird.Core             domain model, no Avalonia/UI refs, no third-party deps
SongBird.Infrastructure   SQLite, filesystem, tag parsing, hashing, artwork extraction, library manifests
SongBird.Playback         IPlaybackService implementation(s), wraps the chosen audio backend
SongBird.Desktop          Avalonia: Views, ViewModels, Navigation, Commands, desktop-specific services. Composition root (DI + logging) lives in App.axaml.cs.
SongBird.Tests            xUnit
```

Project references: `Infrastructure` → `Core`; `Playback` → `Core`; `Desktop` → all three; `Tests` → `Core`/`Infrastructure`/`Playback` (not `Desktop` — UI isn't the test priority, see below).

New code goes in the project matching that table — don't search for precedent, this is the precedent.

### What's in Core (as of Phase 1)

- `Models/`: `Library` (Id, Name, RootPath), `Track` (Id, LibraryId, RelativePath, FileSize, FileLastWriteTimeUtc, FileHash, Duration, Format, tag fields, DateAddedUtc)
- `Metadata/AudioFileMetadata`: what an `IMetadataReader` returns for one file — tag fields + `Artwork` bytes
- `Scanning/`: `ScanProgress`, `ScanResult`/`ScanError`, `SupportedAudioExtensions` (.mp3/.flac/.m4a/.wav/.ogg)
- `Abstractions/`: `ILibraryRepository`, `IMetadataReader`, `IMediaScanner`, `IPlaybackService`
- `Playback/PlaybackState`: `Stopped`/`Playing`/`Paused` enum used by `IPlaybackService`

### What's in Infrastructure (as of Phase 1)

- `Persistence/SqliteLibraryRepository` — implements `ILibraryRepository`, creates its own schema on first use, one connection per operation (pooled by the driver)
- `Metadata/TagLibMetadataReader` — implements `IMetadataReader` via TagLibSharp
- `Scanning/FileSystemMediaScanner` — implements `IMediaScanner`; runs the whole scan via `Task.Run` internally so callers never need to remember to background it. Handles the identity/move-detection logic described below.

Look at these four files before writing anything scanner- or persistence-adjacent — the incremental-scan and move-detection algorithm is non-obvious and already solved there.

### What's in Playback (as of Phase 3)

- `LibVlcPlaybackService` — the only `IPlaybackService` implementation, wraps LibVLCSharp (`LibVLC` + `MediaPlayer`). `Load(absoluteFilePath)` takes a resolved absolute path, not a `Track` — path resolution (`Library.RootPath + Track.RelativePath`) happens in the Desktop layer, keeping this service ignorant of the domain model entirely.
- Requires `LibVLCSharp.Shared.Core.Initialize()` to have been called once before any `LibVlcPlaybackService` is constructed — done in `SongBird.Desktop/Program.cs`'s `Main`, first thing. Any new entry point (tests, a future CLI, etc.) that constructs one needs the same call first, or it throws.
- The native VLC runtime (`VideoLAN.LibVLC.Windows`) is a package on the **executable** project (`SongBird.Desktop`, or any other exe that ends up hosting playback), not on `SongBird.Playback` itself — native asset deployment needs to happen at the final output directory. A macOS build will need `VideoLAN.LibVLC.Mac` added the same way when that's actually being packaged/tested.
- Not unit-tested against real audio — deliberately: "what to prioritize in tests" (below) doesn't include playback, since it's hardware/native-runtime-dependent and not practically unit-testable. Verified manually instead: run the app (or a scratch console app referencing this project) against a real file and confirm `Play`/`Pause`/`Seek`/`Volume` and the `StateChanged`/`PositionChanged`/`PlaybackEnded` events all behave.

### What's in Desktop (as of Phase 3)

- `Services/IFolderPickerService` + `FolderPickerService` — wraps Avalonia's `Window.StorageProvider` behind an interface so ViewModels don't depend on a `Window`. Registered in DI with the real `MainWindow` instance (see `App.axaml.cs` — the window is constructed *before* `BuildServices` for this reason).
- `Converters/DurationConverter` — `TimeSpan` → `m:ss` / `h:mm:ss` for display. `Converters/PlaybackStateConverter` — `PlaybackState` → the transport button's label ("Play"/"Pause").
- `ViewModels/MainViewModel` — app shell; on startup picks `CreateLibraryViewModel` (no library yet) or `LibraryViewModel` (library exists) and sets it as `CurrentPage`. Also exposes `Player` (the `PlayerViewModel` singleton) for `PlayerBarView` to bind to. `MainWindow.axaml` is a `DockPanel`: `PlayerBarView` docked `Top`, `ContentControl` (bound to `CurrentPage`) filling the rest — the player bar sits outside the page-swapping area so it survives navigation. `ViewLocator` resolves each page's View by naming convention (`FooViewModel` → `FooView`); `PlayerBarView` isn't a "page" so it's referenced directly, not through the locator.
- `ViewModels/CreateLibraryViewModel` — first-run folder picker → saves `Library` → runs the Phase 1 scanner with progress → raises `LibraryReady`.
- `ViewModels/LibraryViewModel` — owns `Songs`/`Albums`/`Artists` (grouped from the loaded tracks), `SearchText` filtering, and drilldown (`SelectedAlbum`/`SelectedArtist` → `DrilldownTracks`). Single-library scope for now (always loads `GetLibrariesAsync()[0]`). `PlayTrackCommand` resolves the absolute path and calls `PlayerViewModel.PlayTrack`.
- `ViewModels/PlayerViewModel` — DI singleton wrapping `IPlaybackService`; holds `NowPlaying`, `State`, `Position`, `Duration`, `Volume`. Marshals every `IPlaybackService` event to the UI thread via `Dispatcher.UIThread.Post` (LibVLC fires them on its own thread). `IsUserSeeking` suppresses `Position` updates while the transport bar's seek slider is being dragged, set/cleared by `PlayerBarView`'s code-behind — otherwise playback ticks (~4/sec) fight the drag. `PlaybackEnded` currently just goes idle; Phase 4 hooks queue-advance there instead.
- `Views/PlayerBarView` — the persistent transport bar (now-playing title/artist, play/pause, seek slider + position/duration, volume). Lives outside page navigation (see above). The play/pause button is `IsEnabled` only when `NowPlaying` is set — **known scope gap, not a bug**: there's no wiring from "a track is selected in a list" to the global transport bar, only from "a track was double-clicked" (`PlayerViewModel.PlayTrack`). Selecting a row does not load it. This was left alone deliberately rather than auto-loading on selection change, because that would interrupt whatever's currently playing just from browsing/clicking around the list. If click-then-press-Play is wanted, it needs its own explicit design (e.g. a `SelectedTrack` that only feeds the player when the button is actually pressed and nothing is currently loaded), not a quick binding.
- Track rows use `DoubleTapped` in code-behind (`LibraryView.axaml.cs`) rather than a binding, since Avalonia has no built-in double-click-to-command binding — this is the one place View code-behind is expected, not a pattern to generalize.

### Known Avalonia layout & input gotchas (this Avalonia version, empirically confirmed — not documented upstream, budget time to re-verify at runtime if you hit unexplained blank/missing UI or dead input)

- **`Grid` with star-sized (`*`) rows/columns**: anything listed *after* a `*` definition in `RowDefinitions`/`ColumnDefinitions` gets silently swallowed to zero size — no error, nothing renders, regardless of whether that later definition is `Auto` or a fixed pixel size, and regardless of nesting depth. The `*` **must be the last token in the list.** Confirmed both for columns (inside a `ListBox.ItemTemplate`, see `TrackItemTemplate` in `LibraryView.axaml` — fixed with a `StackPanel` of fixed-`Width` `TextBlock`s instead of a star-column `Grid`) and for rows (`MainWindow`'s top-level layout).
- **`DockPanel.Dock="Bottom"` doesn't work** — the bottom-docked child gets zero space and the fill child takes 100%, no error. `Dock="Top"`, `"Left"`, and `"Right"` all work correctly (confirmed). This is why `PlayerBarView` is docked `Top` instead of the more conventional `Bottom` — it's not a design choice, it's this bug. If a future screen genuinely needs bottom-docked content, don't reach for `DockPanel.Dock="Bottom"`; re-verify at runtime first or use an alternative (e.g. restructure so the fixed element is `Top`-docked instead).
- **Explicit `Height` + non-default `VerticalAlignment` (e.g. `"Bottom"`) on a `Grid` child** also silently fails to render, even outside any `RowDefinitions` context (plain overlapping siblings in a single-cell `Grid`). `Height` alone (default `Stretch` alignment) works fine — it just centers rather than bottom-aligns. Root cause not fully isolated; treat any `VerticalAlignment="Bottom"`/`"Top"` combined with explicit sizing as suspect and verify at runtime.
- **A gesture handler (`DoubleTapped`, likely `Tapped` too) attached to content *inside* a `ListBox`/`SelectingItemsControl` item template never fires.** The `ListBoxItem` container consumes the pointer input for its own selection handling first, so a recognizer nested inside the template (e.g. on the root of a `DataTemplate`) never sees it — no error, the handler is just never called. Confirmed by diagnostic logging showing zero invocations across many real double-clicks, while the same clicks reliably changed the `ListBox`'s selection. **Fix**: attach the gesture handler to the `ListBox` itself, not the item template, and read `((ListBox)sender).SelectedItem` in the handler (see `OnTrackListDoubleTapped` in `LibraryView.axaml.cs`). This is the pattern to use for any future double-click-on-a-row interaction (e.g. Phase 4's queue).

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
- LibVLCSharp (wraps VLC) for audio playback — chosen over ManagedBass (non-commercial-only license) and NAudio (effectively Windows-only) specifically because it's free/LGPL and genuinely cross-platform, matching the Windows+macOS goal
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
