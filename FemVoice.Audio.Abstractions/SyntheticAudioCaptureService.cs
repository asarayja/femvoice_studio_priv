using System.Diagnostics;

namespace FemVoiceStudio.Audio.Abstractions;

/// <summary>
/// A no-op capture service: enumerates no devices and never raises frames. Used for headless/Linux
/// bootstrapping where no microphone backend is wired. Start/Stop are safe and synchronous.
/// </summary>
public sealed class NoopAudioCaptureService : IAudioCaptureService
{
    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    public IReadOnlyList<AudioInputDevice> GetInputDevices() => Array.Empty<AudioInputDevice>();

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        _ = FrameAvailable; _ = DeviceLost; // suppress unused-event warnings; this backend emits nothing
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;
}

/// <summary>
/// Generates deterministic synthetic mono frames (a steady sine at <see cref="Frequency"/>) on a background
/// loop. For tests and Avalonia bootstrapping ONLY — it lets the DSP/scoring pipeline run end-to-end without
/// real hardware. It changes no DSP behaviour; it merely feeds samples a real mic could also produce.
/// </summary>
public sealed class SyntheticAudioCaptureService : IAudioCaptureService
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>Tone frequency in Hz (default 200 Hz — a typical target-zone pitch).</summary>
    public double Frequency { get; init; } = 200.0;

    /// <summary>Peak amplitude in [0, 1].</summary>
    public double Amplitude { get; init; } = 0.2;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    public IReadOnlyList<AudioInputDevice> GetInputDevices()
        => new[] { new AudioInputDevice("synthetic", "Synthetic Sine Source", true) };

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_loop is { IsCompleted: false }) return Task.CompletedTask;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cts.Token;
            _loop = Task.Run(() => Generate(options, token), token);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? loop;
        lock (_gate)
        {
            _cts?.Cancel();
            loop = _loop;
            _loop = null;
        }
        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected on stop */ }
        }
    }

    private async Task Generate(AudioCaptureOptions options, CancellationToken token)
    {
        int sampleRate = options.SampleRate <= 0 ? 44100 : options.SampleRate;
        int bufferSamples = options.BufferSamples <= 0 ? 1024 : options.BufferSamples;
        double frameMs = 1000.0 * bufferSamples / sampleRate;
        long n = 0;
        double twoPiF = 2.0 * Math.PI * Frequency;
        while (!token.IsCancellationRequested)
        {
            var buffer = new float[bufferSamples];
            for (int i = 0; i < bufferSamples; i++, n++)
                buffer[i] = (float)(Amplitude * Math.Sin(twoPiF * n / sampleRate));

            FrameAvailable?.Invoke(this, new AudioFrameAvailableEventArgs(buffer, sampleRate, 1));

            try { await Task.Delay(TimeSpan.FromMilliseconds(frameMs), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
        _ = DeviceLost; // this backend never loses a device
        Debug.WriteLine("SyntheticAudioCaptureService loop ended.");
    }
}
