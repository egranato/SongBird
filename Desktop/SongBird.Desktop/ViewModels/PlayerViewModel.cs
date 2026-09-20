using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SongBird.Core.Abstractions;
using SongBird.Core.Models;
using SongBird.Core.Playback;

namespace SongBird.Desktop.ViewModels;

/// <summary>
/// Shared, screen-independent playback state. Registered as a DI singleton so it survives
/// navigation. Wraps IPlaybackService, which only plays media - it has no queue awareness,
/// so PlaybackEnded currently just goes idle (Phase 4 hooks queue-advance here instead).
/// </summary>
public partial class PlayerViewModel : ViewModelBase, IDisposable
{
    private readonly IPlaybackService _playbackService;

    [ObservableProperty]
    public partial Track? NowPlaying { get; set; }

    [ObservableProperty]
    public partial PlaybackState State { get; set; } = PlaybackState.Stopped;

    [ObservableProperty]
    public partial TimeSpan Position { get; set; }

    [ObservableProperty]
    public partial TimeSpan Duration { get; set; }

    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;

    /// <summary>True while the user is dragging the seek slider - suppresses Position updates so playback ticks don't fight the drag.</summary>
    public bool IsUserSeeking { get; set; }

    public PlayerViewModel(IPlaybackService playbackService)
    {
        _playbackService = playbackService;
        _playbackService.PositionChanged += OnPositionChanged;
        _playbackService.StateChanged += OnStateChanged;
        _playbackService.PlaybackEnded += OnPlaybackEnded;

        Volume = _playbackService.Volume;
    }

    public void PlayTrack(Track track, string absoluteFilePath)
    {
        NowPlaying = track;
        _playbackService.Load(absoluteFilePath);
        _playbackService.Play();
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (State == PlaybackState.Playing)
        {
            _playbackService.Pause();
        }
        else if (NowPlaying is not null)
        {
            _playbackService.Play();
        }
    }

    [RelayCommand]
    private void Seek(double positionSeconds) => _playbackService.Seek(TimeSpan.FromSeconds(positionSeconds));

    partial void OnVolumeChanged(double value) => _playbackService.Volume = value;

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        if (IsUserSeeking)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => Position = position);
    }

    private void OnStateChanged(object? sender, PlaybackState state) =>
        Dispatcher.UIThread.Post(() =>
        {
            State = state;
            if (state == PlaybackState.Playing)
            {
                Duration = _playbackService.Duration;
            }
        });

    private void OnPlaybackEnded(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            State = PlaybackState.Stopped;
            Position = TimeSpan.Zero;
        });

    public void Dispose()
    {
        _playbackService.PositionChanged -= OnPositionChanged;
        _playbackService.StateChanged -= OnStateChanged;
        _playbackService.PlaybackEnded -= OnPlaybackEnded;
    }
}
