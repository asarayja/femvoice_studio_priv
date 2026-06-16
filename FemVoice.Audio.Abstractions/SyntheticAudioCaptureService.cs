using System.Diagnostics;

namespace FemVoiceStudio.Audio.Abstractions;

/// <summary>Synthetic signal shapes for headless/Linux dashboard testing (no real microphone).</summary>
public enum SyntheticAudioMode
{
    StablePitch,
    UnstablePitch,
    PitchRampUp,
    PitchRampDown,
    Silence,
}

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
/// Generates deterministic synthetic mono frames on a background loop, selectable via <see cref="Mode"/>
/// (steady tone, wobbling tone, rising/falling glide, or silence). For tests and Avalonia/Linux
/// dashboard bootstrapping ONLY — it lets the DSP/scoring pipeline run end-to-end without real hardware.
/// It changes no DSP behaviour; it merely feeds samples a real mic could also produce. Uses a running
/// phase accumulator so frequency changes are click-free.
/// </summary>
public sealed class SyntheticAudioCaptureService : IAudioCaptureService
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private double _phase;   // radians, continuous across frames
    private long _n;         // sample counter (for time base)

    /// <summary>Center/base frequency in Hz (default 200 — a typical target-zone pitch).</summary>
    public double BaseFrequency { get; init; } = 200.0;

    /// <summary>Peak amplitude in [0, 1].</summary>
    public double Amplitude { get; init; } = 0.2;

    /// <summary>Signal shape. Settable at runtime; takes effect on the next frame.</summary>
    public SyntheticAudioMode Mode { get; set; } = SyntheticAudioMode.StablePitch;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    public IReadOnlyList<AudioInputDevice> GetInputDevices()
        => new[] { new AudioInputDevice("synthetic", "Synthetic Signal Source", true) };

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

    /// <summary>Instantaneous target frequency for the current mode at time t (seconds).</summary>
    private double FrequencyAt(double t)
    {
        const double rampSeconds = 6.0;
        return Mode switch
        {
            SyntheticAudioMode.StablePitch  => BaseFrequency,
            SyntheticAudioMode.UnstablePitch => BaseFrequency + 25.0 * Math.Sin(2 * Math.PI * 7.0 * t)
                                                              + 12.0 * Math.Sin(2 * Math.PI * 13.3 * t),
            SyntheticAudioMode.PitchRampUp   => 150.0 + (t % rampSeconds) / rampSeconds * 110.0,   // 150 -> 260
            SyntheticAudioMode.PitchRampDown => 260.0 - (t % rampSeconds) / rampSeconds * 110.0,   // 260 -> 150
            SyntheticAudioMode.Silence       => 0.0,
            _ => BaseFrequency,
        };
    }

    private async Task Generate(AudioCaptureOptions options, CancellationToken token)
    {
        int sampleRate = options.SampleRate <= 0 ? 44100 : options.SampleRate;
        int bufferSamples = options.BufferSamples <= 0 ? 1024 : options.BufferSamples;
        double frameMs = 1000.0 * bufferSamples / sampleRate;

        while (!token.IsCancellationRequested)
        {
            var buffer = new float[bufferSamples];
            bool silent = Mode == SyntheticAudioMode.Silence;
            for (int i = 0; i < bufferSamples; i++, _n++)
            {
                if (silent)
                {
                    buffer[i] = 0f;
                    continue;
                }
                double t = (double)_n / sampleRate;
                double f = FrequencyAt(t);
                _phase += 2 * Math.PI * f / sampleRate;
                if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
                buffer[i] = (float)(Amplitude * Math.Sin(_phase));
            }

            FrameAvailable?.Invoke(this, new AudioFrameAvailableEventArgs(buffer, sampleRate, 1));

            try { await Task.Delay(TimeSpan.FromMilliseconds(frameMs), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
        _ = DeviceLost; // this backend never loses a device
        Debug.WriteLine("SyntheticAudioCaptureService loop ended.");
    }
}
