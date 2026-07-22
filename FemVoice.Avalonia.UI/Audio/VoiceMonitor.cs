using System;
using FemVoiceStudio.Audio.Abstractions;

namespace FemVoice.Avalonia.Audio;

/// <summary>
/// "Hear your own voice" — real-time mic-to-speaker monitor. When the user has enabled
/// <see cref="FemVoice.Avalonia.Preferences.UiPreferences.HearOwnVoice"/>, a session routes each captured microphone
/// frame to the speaker via the platform playback backend (<see cref="AudioPlaybackBackendFactory"/>: ALSA on Linux,
/// winmm on Windows, AudioTrack on Android). It is fully opt-in and fail-safe: when the preference is off, or no
/// output backend exists, playback opens nothing and <see cref="Feed"/> is a no-op. Created once per
/// capture-driving view-model; <see cref="Feed"/> is called from the capture thread.
///
/// The preference is re-evaluated LIVE during a running session (throttled), so toggling "hear own voice" in
/// Settings takes effect immediately — start or stop playback within the running session — WITHOUT restarting the
/// session or the app.
/// </summary>
public sealed class VoiceMonitor : IDisposable
{
    private readonly IAudioPlaybackService _playback;
    private volatile bool _active;          // playback currently open+running
    private volatile bool _sessionRunning;  // a capture session is live (frames are flowing)
    private int _sampleRate = 44100;
    private long _lastCheckTicks;
    private static readonly long CheckIntervalTicks = TimeSpan.FromMilliseconds(400).Ticks;

    public VoiceMonitor() : this(AudioPlaybackBackendFactory.CreateForRuntime()) { }

    /// <param name="playback">Injected backend (tests pass a no-op/fake); production uses the runtime one.</param>
    public VoiceMonitor(IAudioPlaybackService playback) => _playback = playback ?? new NoopAudioPlaybackService();

    /// <summary>True when real playback is available on this platform (drives the Settings toggle's usefulness).</summary>
    public bool IsAvailable => _playback.IsAvailable;

    /// <summary>Begin a session. Applies the CURRENT preference immediately; the live re-check in <see cref="Feed"/>
    /// then keeps it in sync with any Settings toggle for the rest of the session.</summary>
    public void Start(int sampleRate)
    {
        _sampleRate = sampleRate;
        _sessionRunning = true;
        _lastCheckTicks = 0;   // force an immediate evaluation
        Evaluate();
    }

    /// <summary>Play back one captured mono frame. Also re-evaluates the live preference (throttled) so a Settings
    /// toggle starts/stops monitoring mid-session without a restart. Called on the capture thread.</summary>
    public void Feed(float[] samples)
    {
        if (!_sessionRunning) return;
        long now = DateTime.UtcNow.Ticks;
        if (now - _lastCheckTicks > CheckIntervalTicks)
        {
            _lastCheckTicks = now;
            Evaluate();
        }
        if (_active) _playback.Write(samples);
    }

    // Bring playback into line with the live preference: start it when wanted+available, stop it when not.
    private void Evaluate()
    {
        bool want = _sessionRunning
                    && _playback.IsAvailable
                    && FemVoice.Avalonia.Preferences.CapturePreferences.HearOwnVoice();
        if (want && !_active)
        {
            _playback.Start(_sampleRate, 1);
            _active = true;
        }
        else if (!want && _active)
        {
            _playback.Stop();
            _active = false;
        }
    }

    public void Stop()
    {
        _sessionRunning = false;
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
