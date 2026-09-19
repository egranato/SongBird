using System.Collections.Generic;
using SongBird.Core.Models;

namespace SongBird.Desktop.ViewModels;

public sealed record ArtistGroup(string Name, IReadOnlyList<Track> Tracks)
{
    public override string ToString() => Name;
}
