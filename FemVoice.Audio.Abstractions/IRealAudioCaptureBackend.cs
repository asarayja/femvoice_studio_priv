namespace FemVoiceStudio.Audio.Abstractions;

/// <summary>
/// A real (hardware) capture backend that can report whether it is actually usable on this machine right now.
/// Distinguishes the truthful "real capture available" state (a native binding is wired AND a device can be
/// opened) from the synthetic/display-only and not-configured states, without starting capture.
/// </summary>
public interface IRealAudioCaptureBackend : IAudioCaptureService
{
    /// <summary>True only when a native capture binding is wired for this OS and a capture device can be opened.</summary>
    bool IsBackendAvailable { get; }
}
