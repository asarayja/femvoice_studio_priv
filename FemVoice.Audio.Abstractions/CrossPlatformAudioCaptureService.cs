using System.Diagnostics;
using System.Runtime.InteropServices;
using FemVoiceStudio.Audio.Abstractions.Linux;
using FemVoiceStudio.Audio.Abstractions.Windows;

namespace FemVoiceStudio.Audio.Abstractions;

/// <summary>
/// The cross-platform REAL-capture backend behind <see cref="IAudioCaptureService"/>. It is an OS DISPATCHER: at
/// construction it selects the native capture binding for the current operating system and forwards its frames and
/// device-lost events unchanged. It carries NO NuGet native dependency itself — the Linux path is pure ALSA
/// P/Invoke (<see cref="AlsaAudioCaptureService"/>) — so <c>FemVoice.Avalonia</c> keeps referencing only Core + this
/// abstractions assembly.
///
/// Platform coverage in this slice:
/// <list type="bullet">
///   <item>Linux → real ALSA capture (verified end-to-end).</item>
///   <item>Windows → real capture via the multimedia <c>waveIn</c> API (<see cref="WinMmAudioCaptureService"/>),
///         also dependency-free P/Invoke — no NuGet, no COM.</item>
///   <item>macOS → reported "unavailable" here; a CoreAudio/AVFoundation binding is a follow-up slice.</item>
/// </list>
/// When no native binding is wired for the current OS (or the device can't be opened), it degrades GRACEFULLY:
/// <see cref="IsBackendAvailable"/> is <c>false</c>, enumeration is empty, and <see cref="StartAsync"/> raises
/// <see cref="DeviceLost"/> and starts NO loop — it never throws and never fabricates frames.
/// </summary>
public sealed class CrossPlatformAudioCaptureService : IRealAudioCaptureBackend, IDisposable
{
    private readonly IRealAudioCaptureBackend? _native;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    public CrossPlatformAudioCaptureService()
    {
        _native = SelectNativeBackend();
        if (_native is not null)
        {
            // Forward the native backend's events unchanged (no DSP, no reshaping).
            _native.FrameAvailable += (_, e) => FrameAvailable?.Invoke(this, e);
            _native.DeviceLost += (_, e) => DeviceLost?.Invoke(this, e);
        }
    }

    /// <summary>Pick the native capture binding for the current OS, or null when none is wired for it.</summary>
    private static IRealAudioCaptureBackend? SelectNativeBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new AlsaAudioCaptureService();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WinMmAudioCaptureService();   // dependency-free winmm/waveIn capture (no NuGet, no COM)
        // macOS native capture is a follow-up slice; it falls through to "unavailable" via a null native backend on
        // this dispatcher (graceful degradation to the synthetic display-only source).
        return null;
    }

    /// <summary>Human-readable note about which native backend was selected for the current OS (for diagnostics/logs).</summary>
    public string SelectedBackendDescription => _native?.GetType().Name ?? "none (no native binding for this OS)";

    /// <summary>True only when a native binding is wired for this OS AND its device can actually be opened.</summary>
    public bool IsBackendAvailable => _native?.IsBackendAvailable ?? false;

    /// <summary>Enumerate the native backend's input devices, or empty when no backend is wired. Never throws.</summary>
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        try { return _native?.GetInputDevices() ?? Array.Empty<AudioInputDevice>(); }
        catch (Exception ex) { Debug.WriteLine($"CrossPlatformAudioCaptureService enumerate failed: {ex.Message}"); return Array.Empty<AudioInputDevice>(); }
    }

    /// <summary>Delegate to the native backend. With no backend wired for this OS, fail-safe: signal device-lost and
    /// start NO capture loop (no frames, no clinical runtime fed). Never throws.</summary>
    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        if (_native is null)
        {
            DeviceLost?.Invoke(this, new AudioDeviceLostEventArgs("No cross-platform capture backend is wired for this OS yet."));
            return Task.CompletedTask;
        }
        return _native.StartAsync(options, cancellationToken);
    }

    public Task StopAsync() => _native?.StopAsync() ?? Task.CompletedTask;

    public void Dispose() => (_native as IDisposable)?.Dispose();
}

/// <summary>
/// Selects the audio-capture backend per operating system, behind the neutral <see cref="IAudioCaptureService"/>
/// contract. Keeps backend choice in one place so composition roots (and smokes) don't hard-code platform types.
/// </summary>
public static class AudioCaptureBackendFactory
{
    /// <summary>Optional platform-provided real backend, set by a platform head whose native capture API cannot live
    /// in this dependency-free abstractions assembly (e.g. Android's <c>AudioRecord</c>, wired from FemVoice.Android
    /// at startup). When set, it is used for BOTH device enumeration (<see cref="CreateReal"/>) and the runtime
    /// backend (<see cref="CreateForRuntime"/>). Null on desktop → the built-in Linux/Windows dispatcher is used.</summary>
    public static Func<IRealAudioCaptureBackend>? PlatformRealBackendFactory { get; set; }

    /// <summary>The real (hardware) capture backend for the current OS: the platform-provided one when a head has set
    /// <see cref="PlatformRealBackendFactory"/> (Android), otherwise the built-in cross-platform OS dispatcher
    /// (Linux/ALSA, Windows/winmm; degrades to "unavailable" elsewhere). Does NOT start capture.</summary>
    public static IRealAudioCaptureBackend CreateReal()
        => PlatformRealBackendFactory?.Invoke() ?? new CrossPlatformAudioCaptureService();

    /// <summary>The synthetic, display-only backend (deterministic frames, no hardware). Used as the fail-safe
    /// fallback for the runtime and for headless/CI hosts.</summary>
    public static IAudioCaptureService CreateSynthetic() => new SyntheticAudioCaptureService();

    /// <summary>The backend the live runtime should use: the REAL cross-platform capture backend when a microphone
    /// is actually available on this OS (Linux/ALSA today), otherwise the synthetic display-only backend. Only the
    /// SOURCE of audio frames differs — the shared DSP/pitch/scoring services consuming them are unchanged. Never
    /// throws; the availability probe is cheap (open+close of the default capture device) and the real backend is
    /// disposed when we fall back.</summary>
    public static IAudioCaptureService CreateForRuntime()
    {
        var real = CreateReal();
        if (real.IsBackendAvailable)
        {
            System.Diagnostics.Debug.WriteLine("AudioCaptureBackendFactory: real capture backend selected for runtime.");
            return real;
        }
        (real as IDisposable)?.Dispose();
        System.Diagnostics.Debug.WriteLine("AudioCaptureBackendFactory: no real device; synthetic display-only backend selected for runtime.");
        return CreateSynthetic();
    }
}
