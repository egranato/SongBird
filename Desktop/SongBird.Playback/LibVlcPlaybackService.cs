using LibVLCSharp.Shared;
using SongBird.Core.Abstractions;
using SongBird.Core.Playback;

namespace SongBird.Playback;

public sealed class LibVlcPlaybackService : IPlaybackService, IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private PlaybackState _state = PlaybackState.Stopped;

    public LibVlcPlaybackService()
    {
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);

        _mediaPlayer.TimeChanged += (_, e) => PositionChanged?.Invoke(this, TimeSpan.FromMilliseconds(e.Time));
        _mediaPlayer.Playing += (_, _) => SetState(PlaybackState.Playing);
        _mediaPlayer.Paused += (_, _) => SetState(PlaybackState.Paused);
        _mediaPlayer.Stopped += (_, _) => SetState(PlaybackState.Stopped);
        _mediaPlayer.EndReached += (_, _) =>
        {
            SetState(PlaybackState.Stopped);
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? PlaybackEnded;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;

    public void Load(string absoluteFilePath)
    {
        using var media = new Media(_libVlc, new Uri(absoluteFilePath));
        _mediaPlayer.Media = media;
    }

    public void Play() => _mediaPlayer.Play();

    public void Pause() => _mediaPlayer.Pause();

    public void Stop() => _mediaPlayer.Stop();

    public void Seek(TimeSpan position) => _mediaPlayer.Time = (long)position.TotalMilliseconds;

    public double Volume
    {
        get => _mediaPlayer.Volume / 100.0;
        set => _mediaPlayer.Volume = (int)(Math.Clamp(value, 0, 1) * 100);
    }

    public TimeSpan Position => TimeSpan.FromMilliseconds(Math.Max(0, _mediaPlayer.Time));

    public TimeSpan Duration => TimeSpan.FromMilliseconds(Math.Max(0, _mediaPlayer.Length));

    public PlaybackState State => _state;

    private void SetState(PlaybackState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
    }
}
