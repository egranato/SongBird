using System.Collections.Generic;
using SongBird.Core.Models;

namespace SongBird.Desktop.ViewModels;

public sealed record AlbumGroup(string Title, string Artist, IReadOnlyList<Track> Tracks)
{
    public override string ToString() => $"{Title} — {Artist}";
}
