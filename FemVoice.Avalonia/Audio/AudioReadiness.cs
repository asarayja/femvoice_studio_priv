using System;
using System.Linq;
using FemVoiceStudio.Audio.Abstractions;   // IAudioCaptureService, AudioInputDevice, Synthetic/Noop (allowed Abstractions assembly)
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.Audio;

/// <summary>Truthful classification of the active audio capture backend.</summary>
public enum AudioBackendKind { NotConfigured, Synthetic, Real }

/// <summary>
/// Stage 3A — AVALONIA-OWNED, READ-ONLY audio readiness/status over the approved capture abstraction
/// (<see cref="IAudioCaptureService"/> in FemVoice.Audio.Abstractions). It reports a TRUTHFUL view — backend kind,
/// enumerated device count, whether real capture is available, and a localized status string — WITHOUT starting
/// capture: it only calls <c>GetInputDevices()</c> and NEVER <c>StartAsync</c>. No WPF, no Windows-only audio, no
/// DB, no clinical/scoring/SmartCoach/progression behaviour.
///
/// The Avalonia head currently has only the synthetic/noop backends (the real backend is Windows-only and lives
/// outside this app), so real capture is NOT available yet — this surfaces that honestly and is ready for a future
/// cross-platform backend wired through the same abstraction.
/// </summary>
public sealed class AudioReadiness
{
    private readonly IAudioCaptureService? _capture;

    public AudioReadiness(IAudioCaptureService? capture) => _capture = capture;

    /// <summary>Backend classification from the injected service type (Avalonia uses Synthetic; Noop/none = not configured).</summary>
    public AudioBackendKind BackendKind => _capture switch
    {
        null => AudioBackendKind.NotConfigured,
        NoopAudioCaptureService => AudioBackendKind.NotConfigured,
        SyntheticAudioCaptureService => AudioBackendKind.Synthetic,
        _ => AudioBackendKind.Real,
    };

    /// <summary>Enumerated input device count via the abstraction (never starts capture). 0 on error/none.</summary>
    public int DeviceCount
    {
        get { try { return _capture?.GetInputDevices().Count ?? 0; } catch (Exception) { return 0; } }
    }

    /// <summary>Real microphone capture is available only with a real (non-synthetic, non-noop) backend that has
    /// at least one device. False for the synthetic/display-only backend used today.</summary>
    public bool IsRealCaptureAvailable => BackendKind == AudioBackendKind.Real && DeviceCount > 0;

    /// <summary>Truthful, localized status line for the UI. Never implies real capture when it is not available.</summary>
    public string StatusText => BackendKind switch
    {
        AudioBackendKind.Synthetic => Localized.Get("Shell_MicStatus", "Mikrofon: syntetisk (kun visning)"),
        AudioBackendKind.Real => $"{Localized.Get("Audio_DevicesFound", "Mikrofon: enheter funnet")}: {DeviceCount}",
        _ => Localized.Get("Audio_Backend_NotConfigured", "Mikrofon: lydbackend ikke konfigurert ennå"),
    };
}
