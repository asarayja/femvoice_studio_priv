using System.Diagnostics;

namespace FemVoiceStudio.Audio.Abstractions;

/// <summary>
/// Stage 3B — the cross-platform REAL-capture backend SLOT behind <see cref="IAudioCaptureService"/>. It is a
/// dependency-free MANAGED SKELETON: it compiles and packages on Linux/macOS/Windows and degrades GRACEFULLY to
/// "backend unavailable / no devices" because no native microphone binding is wired yet. It never produces audio
/// frames and never feeds clinical runtime.
///
/// A future slice should implement real capture via a platform-native binding (ALSA/PulseAudio on Linux,
/// CoreAudio/AVFoundation on macOS, WASAPI on Windows) in a SEPARATE implementation project carrying that native
/// dependency — NOT here and NOT inside FemVoice.Avalonia — so the UI keeps referencing only Core + this
/// abstractions assembly. This slot lets the readiness/status path report the real-backend state truthfully today.
/// </summary>
public sealed class CrossPlatformAudioCaptureService : IAudioCaptureService
{
    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    /// <summary>Whether a real platform capture binding is wired. <c>false</c> until a native backend is added.</summary>
    public bool IsBackendAvailable => false;

    /// <summary>No native enumeration yet → empty (degrades gracefully; never throws).</summary>
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        _ = FrameAvailable; // no real device discovery wired; safe in CI/headless
        return Array.Empty<AudioInputDevice>();
    }

    /// <summary>Fail-safe: the backend is not implemented, so it signals device-lost and starts NO capture loop —
    /// no frames are ever produced and no clinical runtime is fed. Never throws.</summary>
    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("CrossPlatformAudioCaptureService.StartAsync: no native backend wired; capture not started.");
        DeviceLost?.Invoke(this, new AudioDeviceLostEventArgs("Cross-platform capture backend not implemented yet."));
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;
}
