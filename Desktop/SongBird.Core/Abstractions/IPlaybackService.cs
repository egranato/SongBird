using SongBird.Core.Playback;

namespace SongBird.Core.Abstractions;

/// <summary>
/// Plays a single media file. Has no queue awareness and never decides what plays next -
/// Next/Previous/Repeat/Shuffle are queue/orchestration concerns (Phase 4), not this service's.
/// </summary>
public interface IPlaybackService
{
    void Load(string absoluteFilePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);

    /// <summary>0.0-1.0</summary>
    double Volume { get; set; }

    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    PlaybackState State { get; }

    event EventHandler? PlaybackEnded;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<PlaybackState>? StateChanged;
}
