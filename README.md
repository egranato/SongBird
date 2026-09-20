# SongBird

A from-scratch iTunes replacement: local music library management first, with eventual self-hosted streaming to mobile clients. Desktop-first — the streaming server and mobile apps come later.

## Status

Desktop client (Windows/macOS, built with Avalonia): Phases 0–3 of the [desktop roadmap](Docs/2%20Desktop%20Roadmap.md) are done.

- Point the app at a folder of music and it builds a persistent, incrementally-rescannable library (tag-based, move/rename-safe).
- Browse by Songs / Albums / Artists, with search and drilldown.
- Play, pause, seek, and adjust volume through a persistent transport bar.

Not yet built: queue/shuffle/repeat, metadata editing, artwork, the portable-library sync model, mobile apps, and the streaming server. See the roadmap for what's next.

## Repo layout

| Folder | Status |
| --- | --- |
| `Desktop/` | Active — the Avalonia desktop client |
| `StreamingServer/`, `Android/`, `IOS/` | Not started |
| `Docs/` | Planning docs: architecture rationale and the phase roadmap |

## Running it

```
cd Desktop
dotnet run --project SongBird.Desktop
```

Requires the .NET 10 SDK. First launch asks you to pick a music folder; see [Docs/2 Desktop Roadmap.md](Docs/2%20Desktop%20Roadmap.md) for what happens after that.

## License

MIT — see [LICENSE](LICENSE).
