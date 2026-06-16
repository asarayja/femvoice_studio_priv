namespace FemVoiceStudio.Audio.Abstractions;

/// <summary>
/// Platform-neutral microphone capture boundary. The Windows reference implementation wraps NAudio
/// (WASAPI/WaveIn) in FemVoice.Audio.Windows; Linux/macOS/Avalonia bootstrapping use the synthetic/noop
/// implementations in this assembly. This interface intentionally exposes ONLY float-frame streaming +
/// device enumeration so that no DSP, scoring, or clinical behaviour depends on the capture backend.
/// </summary>
public interface IAudioCaptureService
{
    /// <summary>Raised on the capture thread for each frame of mono float samples.</summary>
    event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;

    /// <summary>Raised when the active capture device is lost (driver failure, unplug, OS privacy block).</summary>
    event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    /// <summary>Enumerate available input devices. May return an empty list when no capture backend exists.</summary>
    IReadOnlyList<AudioInputDevice> GetInputDevices();

    /// <summary>Start capture with the given options. Idempotent; safe to await.</summary>
    Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default);

    /// <summary>Stop capture and release the device.</summary>
    Task StopAsync();
}

/// <summary>A captured frame of mono float samples in [-1, 1], plus the format it was captured at.</summary>
public sealed class AudioFrameAvailableEventArgs : EventArgs
{
    public AudioFrameAvailableEventArgs(float[] samples, int sampleRate, int channels)
    {
        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
    }

    public float[] Samples { get; }
    public int SampleRate { get; }
    public int Channels { get; }
}

/// <summary>Signals that the capture device became unavailable; carries an optional human-readable reason.</summary>
public sealed class AudioDeviceLostEventArgs : EventArgs
{
    public AudioDeviceLostEventArgs(string? reason = null) => Reason = reason;
    public string? Reason { get; }
}

/// <summary>A selectable input device. <see cref="Id"/> is backend-specific (e.g. NAudio device number or WASAPI id).</summary>
public sealed record AudioInputDevice(string Id, string Name, bool IsDefault);

/// <summary>Capture configuration. Defaults match the WPF baseline (44.1 kHz mono, 1024-sample buffer).</summary>
public sealed record AudioCaptureOptions(
    int SampleRate = 44100,
    int Channels = 1,
    int BitsPerSample = 16,
    int BufferSamples = 1024,
    string? DeviceId = null);
