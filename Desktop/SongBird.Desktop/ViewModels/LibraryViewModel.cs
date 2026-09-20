using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongBird.Core.Abstractions;
using SongBird.Core.Models;

namespace SongBird.Desktop.ViewModels;

public partial class LibraryViewModel(
    ILibraryRepository repository,
    IMediaScanner scanner,
    PlayerViewModel player) : ViewModelBase
{
    private Library? _library;
    private List<Track> _allTracks = [];

    public ObservableCollection<Track> Songs { get; } = [];
    public ObservableCollection<AlbumGroup> Albums { get; } = [];
    public ObservableCollection<ArtistGroup> Artists { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial AlbumGroup? SelectedAlbum { get; set; }

    [ObservableProperty]
    public partial ArtistGroup? SelectedArtist { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<Track>? DrilldownTracks { get; set; }

    [ObservableProperty]
    public partial string? DrilldownTitle { get; set; }

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    public async Task LoadAsync()
    {
        var libraries = await repository.GetLibrariesAsync();
        _library = libraries[0]; // Phase 2 scope: a single library, always the first one.
        await ReloadTracksAsync();
    }

    [RelayCommand]
    private async Task RescanAsync()
    {
        if (_library is null)
        {
            return;
        }

        IsScanning = true;
        await scanner.ScanAsync(_library);
        IsScanning = false;
        await ReloadTracksAsync();
    }

    [RelayCommand]
    private void PlayTrack(Track track)
    {
        if (_library is null)
        {
            return;
        }

        var absolutePath = Path.Combine(_library.RootPath, track.RelativePath);
        player.PlayTrack(track, absolutePath);
    }

    [RelayCommand]
    private void CloseDrilldown()
    {
        DrilldownTracks = null;
        DrilldownTitle = null;
        SelectedAlbum = null;
        SelectedArtist = null;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedAlbumChanged(AlbumGroup? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedArtist = null;
        DrilldownTitle = value.ToString();
        DrilldownTracks = new ObservableCollection<Track>(
            value.Tracks.OrderBy(t => t.DiscNumber).ThenBy(t => t.TrackNumber));
    }

    partial void OnSelectedArtistChanged(ArtistGroup? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedAlbum = null;
        DrilldownTitle = value.Name;
        DrilldownTracks = new ObservableCollection<Track>(
            value.Tracks.OrderBy(t => t.Album).ThenBy(t => t.TrackNumber));
    }

    private async Task ReloadTracksAsync()
    {
        _allTracks = (await repository.GetTracksAsync(_library!.Id))
            .OrderBy(t => t.Artist)
            .ThenBy(t => t.Album)
            .ThenBy(t => t.TrackNumber)
            .ToList();

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allTracks
            : _allTracks.Where(Matches).ToList();

        Songs.Clear();
        foreach (var track in filtered)
        {
            Songs.Add(track);
        }

        Albums.Clear();
        foreach (var group in filtered
            .Where(t => t.Album is not null)
            .GroupBy(t => (Title: t.Album!, Artist: t.AlbumArtist ?? t.Artist ?? "Unknown Artist"))
            .OrderBy(g => g.Key.Title))
        {
            Albums.Add(new AlbumGroup(group.Key.Title, group.Key.Artist, group.ToList()));
        }

        Artists.Clear();
        foreach (var group in filtered
            .Where(t => t.Artist is not null)
            .GroupBy(t => t.Artist!)
            .OrderBy(g => g.Key))
        {
            Artists.Add(new ArtistGroup(group.Key, group.ToList()));
        }
    }

    private bool Matches(Track track) =>
        (track.Title?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
        || (track.Artist?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
        || (track.Album?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
}
