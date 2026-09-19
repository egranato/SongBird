using CommunityToolkit.Mvvm.ComponentModel;
using SongBird.Core.Models;

namespace SongBird.Desktop.ViewModels;

/// <summary>
/// Shared, screen-independent "now playing" state. Registered as a DI singleton so it survives
/// navigation. No audio yet (Phase 3) - double-click just sets this for now.
/// </summary>
public partial class PlayerViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial Track? NowPlaying { get; set; }
}
