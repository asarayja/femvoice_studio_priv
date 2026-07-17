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
/// The Avalonia runtime still uses the synthetic backend (display-only). A real cross-platform backend now exists
/// behind the same abstraction (<see cref="CrossPlatformAudioCaptureService"/> — real ALSA capture on Linux; other
/// OSes report "unavailable" pending their own bindings); this surfaces its true state honestly. Whether real
/// capture is wired into the clinical runtime is a separate, later step.
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

    /// <summary>Whether the backend is a working capture backend. The synthetic backend "works" (display-only);
    /// the cross-platform skeleton reports its own availability (false until a native binding exists); noop/none
    /// are unavailable.</summary>
    public bool IsBackendAvailable => _capture switch
    {
        null or NoopAudioCaptureService => false,
        SyntheticAudioCaptureService => true,
        IRealAudioCaptureBackend real => real.IsBackendAvailable,   // e.g. CrossPlatform/ALSA: true only if a device opens
        _ => true,
    };

    /// <summary>Real microphone capture is available only with a real (non-synthetic) backend that is available and
    /// has at least one device. False for the synthetic/display-only backend and the not-yet-implemented skeleton.</summary>
    public bool IsRealCaptureAvailable => BackendKind == AudioBackendKind.Real && IsBackendAvailable && DeviceCount > 0;

    /// <summary>Truthful, localized status line for the UI. Never implies real capture when it is not available.</summary>
    public string StatusText
    {
        get
        {
            if (BackendKind == AudioBackendKind.Synthetic)
                return Localized.Get("Shell_MicStatus", "Mikrofon: syntetisk (kun visning)");
            if (BackendKind == AudioBackendKind.NotConfigured)
                return Localized.Get("Audio_Backend_NotConfigured", "Mikrofon: lydbackend ikke konfigurert ennå");
            // Real backend:
            if (!IsBackendAvailable)
                return Localized.Get("Audio_Backend_Unavailable", "Mikrofon: lydbackend utilgjengelig");
            if (DeviceCount == 0)
                return Localized.Get("Audio_NoDevices", "Mikrofon: ingen enheter funnet");
            return $"{Localized.Get("Audio_DevicesFound", "Mikrofon: enheter funnet")}: {DeviceCount}";
        }
    }
}
