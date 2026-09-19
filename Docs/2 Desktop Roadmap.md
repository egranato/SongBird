# Desktop Roadmap

Derived from [1 Initial POC.md](1%20Initial%20POC.md). Scope: desktop client only (Windows/macOS via Avalonia). Server and mobile are out of scope until noted otherwise below.

Each phase should leave the app in a buildable, runnable state. Don't start a phase until the previous one is done.

## Phase 0 — Solution scaffolding ✅ done
`SongBird.slnx` with `Core` / `Infrastructure` / `Playback` / `Desktop` / `Tests` projects (see [Desktop/CLAUDE.md](../Desktop/CLAUDE.md) for the split). DI via `Microsoft.Extensions.DependencyInjection`, logging wired up, empty Avalonia shell that launches.

## Phase 1 — Library + Scanner
`Library`/`Track` domain model. SQLite schema. Recursive scan with TagLibSharp metadata reads. Incremental rescan using path+size+mtime, hashing only when needed. A corrupt/unsupported file is logged and skipped — it must never abort a scan. Scan progress must not block the UI.

Library and Track IDs must be generated independently of filesystem paths. Renaming or moving a file within a library must not conceptually create a new track when its identity can still be established (e.g. via hash/tag-based matching against the existing index) — it should be recognized as the same track at a new path, not scanned in as a new one.

`Track` stores `RelativePath` (relative to the library root), not an absolute path. `Library` stores the host-specific `RootPath` separately. Never persist an absolute track path — resolve it at runtime from `Library.RootPath + Track.RelativePath`.

The metadata reader's output (`AudioFileMetadata`: Title, Artist, Album, etc.) must include the embedded artwork, even though Phase 6 is where caching/dedup/display actually happen. Reading artwork is part of Phase 1's scan pass, not a scanning-contract change bolted on later.

## Phase 2 — Library browsing
Songs / Albums / Artists views, plus search. Selecting an artist/album drills into its tracks. Double-click starts playback.

## Phase 3 — Playback engine
`IPlaybackService` abstraction in `Playback`. It plays media — it does not decide what plays next. Surface: `Load(track)`, `Play`, `Pause`, `Stop`, `Seek`, `Volume`, plus events `PlaybackEnded`, `PositionChanged`, `StateChanged`. Playback state is independent of whatever screen is currently showing.

Next/Previous/Repeat/Shuffle are queue/orchestration concerns, not playback-service concerns — see Phase 4. The application-level controller reacts to `PlaybackEnded` by asking the queue what happens next; `IPlaybackService` itself has no queue awareness.

## Phase 4 — Queue
`PlaybackQueue` domain object: explicit track-index permutation for shuffle (never pick-next-at-random). Repeat off/all/one. Play Next and Add to Queue actions. Queue + position survive app restart. Queue UI shows current track and up-next list.

On restart, persist and restore: queue, queue cursor, shuffle permutation, repeat mode, active track, and playback position. Restoring state must not automatically begin playback — launching the app should never surprise-resume whatever was playing last.

## Phase 5 — Metadata editing
`TrackField<T>`-style overlay model (original value vs. effective value) for Title, Artist, Album, Album Artist, Track #, Disc #, Year, Genre. Each edited field stores at minimum `value`, `modifiedAtUtc`, and `modifiedByDeviceId` (yes, even with one device today — it's what lets a future sync algorithm reason about edits without a schema migration). Edits are stored in the library DB only — **never written back into the source audio file**.

## Phase 6 — Artwork
Extract embedded artwork on scan. Dedupe identical art across tracks in the same album in the cache (don't store N copies). Generic placeholder when none is present.

## Phase 7 — 0.1 exit criteria / hardening
Point the app at a real music folder and use it as your daily player instead of whatever you use today. Fix whatever that surfaces. This is what "Desktop 0.1 done" means.

## Explicitly not in 0.1
Server, networking, phone sync, accounts, cloud, streaming, transcoding, automatic metadata lookup, lyrics, recommendations, visualization, equalizer, crossfade, ReplayGain, Android/iOS, NAS hosting, filesystem watching (periodic/manual rescan is enough for now), writing metadata back into source files.

## After 0.1 (not detailed yet)
- **Desktop 0.2** — portable-library + synchronization data model: portable durable library state under `/.songbird/` on the external drive (exact format — JSON, SQLite snapshot, JSONL, multiple manifests — is a 0.2 design decision, not fixed now), with the local SQLite DB treated as a derived index per host. This establishes the protocol mobile/server will later consume.
- **Android** — starts only after 0.2's sync protocol exists, so it consumes a real protocol instead of two apps evolving in parallel.
- **Streaming server** — deferred until desktop + protocol are solid.
