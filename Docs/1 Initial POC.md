Yes. Since you're explicitly happy with **separate purpose-built mobile clients**, I would optimize the desktop app entirely for being excellent desktop software. You don't need to drag mobile-framework considerations into this decision.

For you specifically, I'd start with **C#/.NET + Avalonia + SQLite**. Avalonia currently supports Windows and macOS from the same desktop codebase, including Intel and Apple Silicon Macs; it uses Win32 directly on Windows and its own native macOS backend. It can also cross-compile macOS binaries from Windows, although actual macOS packaging/signing/testing will still eventually warrant access to a Mac. ([Avalonia Docs][1])

That's a particularly good fit because C# is already your strongest stack. I don't see enough benefit here to make you learn Rust for Tauri or accept Electron's bundled Chromium/Node runtime just to build a music library UI. Electron absolutely would work and officially targets Windows/macOS/Linux, but you're not gaining much from the web stack for this application. ([Electron][2])

## Desktop 0.1 — the first Claude assignment

I'd define the milestone as:

> **Given a folder containing ordinary music files, build a persistent indexed library and a reliable desktop music player around it.**

Nothing networked yet.

### 1. Library creation

On first launch:

**Create Library** → choose a directory.

Example:

```text
E:\Music
```

The application creates a persistent library identity and remembers the directory.

Support initially:

```text
.mp3
.flac
.m4a
.wav
.ogg
```

I'd be perfectly happy reducing that to MP3 + FLAC initially if codec/library support makes the others distracting.

The important abstraction is already:

```text
Library
    Id
    Name
    RootPath

Track
    Id
    LibraryId
    MediaIdentity
    RelativePath
    FileHash
    FileSize
    Duration
    Format

    Title
    Artist
    Album
    AlbumArtist
    TrackNumber
    DiscNumber
    Year
    Genre

    DateAdded
```

I'd make paths **relative to the library root** whenever possible.

That's what eventually makes:

```text
E:\Music\Artist\Album\song.flac
```

becoming:

```text
/mnt/music/Artist/Album/song.flac
```

fairly painless.

### 2. Scanner/indexer

On library creation, recursively scan the root.

For every supported file:

1. Read file metadata/tags.
2. Determine duration/format.
3. Extract embedded artwork if present.
4. Generate persistent media identity.
5. Store index in local SQLite.
6. Display progress without blocking the UI.

Then subsequent scans should be incremental.

Don't re-hash a 500 GB collection every time the application starts. You can use path + size + modification time to identify files that warrant deeper inspection, while hashes provide stronger identity when necessary.

I'd also explicitly require:

**A corrupt or unsupported file cannot abort a library scan.**

Record the error and keep going.

### 3. Library browsing

First-pass navigation can be extremely conventional:

```text
Songs
Albums
Artists
```

Plus search.

That's enough.

Selecting an artist shows their albums/tracks. Selecting an album shows its tracks. Double-clicking something starts playback.

I wouldn't build genres, composers, release types, smart playlists, etc. yet.

### 4. Player

This is where I would be stricter with Claude because this is the entire reason you're building the damn thing.

Must support:

```text
Play
Pause
Seek
Next
Previous
Volume
Shuffle
Repeat Off
Repeat All
Repeat One
```

And playback continues when navigating around the application.

The player state should be independent of the current screen.

### 5. Queue is an actual domain object

I'd explicitly put this in the requirements because otherwise an agent may implement some convenient ad-hoc `GetNextTrack()` behavior.

Something approximately:

```text
PlaybackQueue
    Tracks[]
    CurrentIndex
    ShuffleEnabled
    RepeatMode
```

Shuffle creates a permutation of the eligible queue.

If there are 827 tracks:

```text
[413, 72, 601, 4, 219, ...]
```

You advance through it.

**Do not randomly select the next song independently.**

That immediately gives you the behavior you're apparently unable to extract from a multibillion-dollar company: *shuffle my fucking music.* 😆

The current queue and position should survive application restart.

### 6. Explicit Queue UI

Have a Queue screen/panel from the beginning.

User should be able to see:

```text
Currently Playing

→ Track A

Up Next

  Track H
  Track C
  Track Q
  Track B
  ...
```

For 0.1, I'd support:

**Play Next** and **Add to Queue**.

Drag-to-reorder can wait if necessary, although it's not unreasonable.

### 7. Metadata editing

I actually would include **basic metadata editing in the first pass**, even though normally I'd defer something like that.

Your eventual synchronization architecture depends upon distinguishing:

> metadata supplied by the file

from

> metadata the user has intentionally changed.

So establish that model immediately.

I'd have something like:

```text
TrackField<T>
    OriginalValue
    EffectiveValue
    ModifiedAtUtc?
    ModifiedByDeviceId?
```

You don't literally have to implement it as a generic C# object—that's the semantic model.

For 0.1, allow editing:

```text
Title
Artist
Album
Album Artist
Track Number
Disc Number
Year
Genre
```

And **do not write those changes back into the audio file yet**.

Your application's library metadata overlays the source-file tags.

That decision makes synchronization much easier later and prevents your app from unexpectedly modifying someone's music collection.

### 8. Artwork

Read embedded album artwork.

If multiple tracks on an album contain the same artwork, don't create 14 giant copies in your application cache.

Fallback can simply be a generic album placeholder.

Manual artwork editing/download/search can wait.

### 9. Persistence

I'd use SQLite locally.

Something like:

```text
Library
Track
Album*
Artist*
TrackMetadataOverride
Artwork
Queue
QueueEntry
AppSetting
Device
```

Whether Album/Artist deserve actual tables immediately depends on how you want to model identity. I'd lean yes because eventually **artist identity is more interesting than an arbitrary string attached to a track**.

And keep the active SQLite DB on the host machine rather than assuming you'll run it directly from NAS storage. SQLite specifically warns that WAL does not work over network filesystems because its coordination mechanisms assume processes are on the same host. ([SQLite][3])

That's relevant to the portable-library idea we just discussed.

## I would slightly revise the portable-library architecture

I'd now use:

```text
External Drive

/Music/
    Artist/
       Album/
          songs.flac

/.yourapp/
    library.json
    metadata/
    playlists/
```

and then:

```text
PC Local App Data
    library.db
    artwork-cache/
    waveform-cache/
    etc.
```

The stuff on the drive is **portable durable state**.

SQLite is a **local derived index**.

So:

```text
USB SSD
   ↓
Windows PC
   ↓
index → local SQLite

USB SSD
   ↓
NAS
   ↓
server indexes → its own local SQLite
```

Same library ID, same track IDs, same metadata, different host index.

That is considerably more robust.

---

# Architecture I'd give Claude

I'd split the solution immediately:

```text
MusicApp.sln

MusicApp.Core
MusicApp.Infrastructure
MusicApp.Playback
MusicApp.Desktop
MusicApp.Tests
```

### `Core`

No Avalonia dependencies.

```text
Library
Track
Artist
Album
Playlist
Queue
Metadata
MediaLocation

ILibraryRepository
IMediaScanner
IMetadataReader
IPlaybackService
```

Pure domain/business logic.

### `Infrastructure`

```text
SQLite
Filesystem
Tag parsing
Hashing
Artwork extraction
Library manifests
```

### `Playback`

Wrap whichever audio engine you select behind:

```text
IPlaybackService
```

This abstraction is worth having because **audio backends are exactly the kind of platform-sensitive dependency you may replace later.**

### `Desktop`

Avalonia UI:

```text
Views
ViewModels
Navigation
Commands
Desktop-specific services
```

Avalonia's normal architecture is well suited to this split: views/view-models/business logic can be shared across desktop platforms, while platform-specific integration can be isolated where necessary. ([Avalonia Docs][4])

### `Tests`

I'd put disproportionate testing effort into:

```text
Library scanning
File identity
Metadata merge
Queue construction
Shuffle
Repeat
Persistence
```

The UI is replaceable.

Those behaviors aren't.

---

# Framework choices

My starting stack would be:

| Concern      | Choice                                                             |
| ------------ | ------------------------------------------------------------------ |
| Language     | C#                                                                 |
| Runtime      | .NET                                                               |
| Desktop UI   | Avalonia                                                           |
| Architecture | MVVM-ish                                                           |
| DB           | SQLite                                                             |
| ORM          | EF Core or Dapper                                                  |
| Audio        | abstraction around a proven native/cross-platform playback library |
| Metadata     | TagLibSharp or equivalent                                          |
| DI           | Microsoft.Extensions.DependencyInjection                           |
| Logging      | Microsoft.Extensions.Logging / Serilog                             |
| Tests        | xUnit                                                              |

I'd personally use **Dapper or even direct SQLite access** here rather than EF if you want maximum transparency. The schema isn't going to be enormous and you already know SQL very well. EF Core isn't wrong; it's just not buying you much.

Avalonia is also intentionally XAML/.NET-oriented and will probably feel reasonably familiar given your C#/desktop background. ([Avalonia Docs][5])

## Why not the alternatives?

**Electron + React** would probably be my second choice. You already know React, and Electron officially provides one JS/HTML/CSS codebase across Windows/macOS/Linux. ([Electron][2]) But for a filesystem-heavy, always-running desktop media application, I'd rather have your C# process than ship Chromium because React is convenient.

**Tauri + React** is attractive architecturally and supports Windows/macOS, but now you've introduced Rust and platform WebView behavior into a project where neither solves a problem you currently have. ([Tauri][6])

**WPF/WinUI** would be fine if this were definitively Windows-only. Since you already want macOS eventually, I wouldn't knowingly create that port.

**MAUI** wouldn't be my choice here either. Since you've explicitly rejected the requirement that desktop and mobile share a codebase, its biggest architectural argument isn't relevant.

So **Avalonia is the one I'd try first rather than assuming cross-platform desktop is impractical**. Its current desktop support is explicitly aimed at Windows/macOS/Linux, with Windows 11 and current macOS versions in its highest support tier. ([Avalonia Docs][1])

---

## What is explicitly NOT in Desktop 0.1

This is important if you're unleashing Claude on it.

No:

* server
* networking
* phone sync
* accounts
* cloud
* streaming
* transcoding
* automatic metadata lookup
* lyrics
* recommendations
* visualization
* equalizer
* crossfade
* ReplayGain
* Android/iOS
* NAS hosting
* filesystem watching if periodic/manual rescan suffices initially
* writing metadata back into source files

**0.1 ends when you can point the application at your actual music folder, close whatever music player you're currently using on PC, and happily listen to your library through this instead.**

Then I'd make **Desktop 0.2 the portable-library + synchronization data model**, and only after that start Android. That forces the desktop implementation to establish the protocol Android will consume instead of inventing synchronization around two independently evolved applications.

And because you've got Claude time you're deliberately trying to burn productively, this is an unusually good candidate: the 0.1 requirements are concrete enough that you can hand most of the implementation off while you act as architect/reviewer rather than spending your Bird Kart energy writing CRUD and audio plumbing.

[1]: https://docs.avaloniaui.net/docs/supported-platforms?utm_source=chatgpt.com "Supported platforms | Avalonia Docs"
[2]: https://www.electronjs.org/docs/latest?utm_source=chatgpt.com "Introduction | Electron"
[3]: https://www.sqlite.org/wal.html?utm_source=chatgpt.com "Write-Ahead Logging"
[4]: https://docs.avaloniaui.net/docs/fundamentals/cross-platform-architecture?utm_source=chatgpt.com "Cross-platform architecture | Avalonia Docs"
[5]: https://docs.avaloniaui.net/?utm_source=chatgpt.com "Avalonia documentation"
[6]: https://v2.tauri.app/start/prerequisites/?utm_source=chatgpt.com "Prerequisites | Tauri"
