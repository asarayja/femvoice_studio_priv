using System;
using FemVoiceStudio.Audio.Abstractions;

namespace FemVoice.Avalonia.Audio;

/// <summary>
/// "Hear your own voice" — real-time mic-to-speaker monitor. When the user has enabled
/// <see cref="FemVoice.Avalonia.Preferences.UiPreferences.HearOwnVoice"/>, a session routes each captured microphone
/// frame to the speaker via the platform playback backend (<see cref="AudioPlaybackBackendFactory"/>: ALSA on Linux,
/// winmm on Windows, AudioTrack on Android). It is fully opt-in and fail-safe: when the preference is off, or no
/// output backend exists, <see cref="Start"/> opens nothing and <see cref="Feed"/> is a no-op. Created once per
/// capture-driving view-model; <see cref="Feed"/> is called from the capture thread.
/// </summary>
public sealed class VoiceMonitor : IDisposable
{
    private readonly IAudioPlaybackService _playback;
    private volatile bool _active;

    public VoiceMonitor() : this(AudioPlaybackBackendFactory.CreateForRuntime()) { }

    /// <param name="playback">Injected backend (tests pass a no-op/fake); production uses the runtime one.</param>
    public VoiceMonitor(IAudioPlaybackService playback) => _playback = playback ?? new NoopAudioPlaybackService();

    /// <summary>True when real playback is available on this platform (drives the Settings toggle's usefulness).</summary>
    public bool IsAvailable => _playback.IsAvailable;

    /// <summary>Start monitoring for this session — ONLY if the user enabled it AND playback is available. Idempotent.</summary>
    public void Start(int sampleRate)
    {
        if (_active) return;
        if (!_playback.IsAvailable) return;
        if (!FemVoice.Avalonia.Preferences.CapturePreferences.HearOwnVoice()) return;
        _playback.Start(sampleRate, 1);
        _active = true;
    }

    /// <summary>Play back one captured mono frame (no-op when not actively monitoring). Called on the capture thread.</summary>
    public void Feed(float[] samples)
    {
        if (_active) _playback.Write(samples);
    }

    public void Stop()
    {
        if (!_active) return;
        _active = false;
        _playback.Stop();
    }

    public void Dispose()
    {
        Stop();
        _playback.Dispose();
    }
}
