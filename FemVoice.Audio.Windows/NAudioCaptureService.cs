using FemVoiceStudio.Audio.Abstractions;

namespace FemVoiceStudio.Audio.Windows;

/// <summary>
/// Windows NAudio implementation of <see cref="IAudioCaptureService"/>. It is a thin ADAPTER over the
/// existing <see cref="AudioCaptureService"/> — all capture behaviour (WASAPI/WaveIn selection, noise
/// gate, high-pass filter, watchdog, device-loss detection, calibration profile, self-monitoring) is
/// delegated unchanged. This class adds no DSP and changes no thresholds; it only translates
/// AudioCaptureService's events into the platform-neutral abstraction.
/// </summary>
public sealed class NAudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly object _gate = new();
    private AudioCaptureService? _capture;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        var names = AudioCaptureService.GetAvailableDevices();
        var list = new List<AudioInputDevice>(names.Length);
        for (int i = 0; i < names.Length; i++)
            list.Add(new AudioInputDevice(i.ToString(), names[i], i == 0));
        return list;
    }

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_capture is { IsRecording: true })
                return Task.CompletedTask;

            // Same defaults as the WPF baseline (44.1 kHz mono 16-bit, 1024-sample buffer).
            var capture = new AudioCaptureService(options.SampleRate, options.Channels, options.BitsPerSample);
            if (options.BufferSamples > 0)
                capture.BufferSize = options.BufferSamples;

            capture.AudioDataAvailable += OnAudioDataAvailable;
            capture.DeviceLost += OnDeviceLost;

            capture.Initialize();      // device selection + calibration load (unchanged behaviour)
            capture.StartRecording();  // starts NAudio capture + watchdog (unchanged behaviour)

            _capture = capture;
        }
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        AudioCaptureService? capture;
        lock (_gate)
        {
            capture = _capture;
            _capture = null;
        }

        if (capture is not null)
        {
            capture.AudioDataAvailable -= OnAudioDataAvailable;
            capture.DeviceLost -= OnDeviceLost;
            capture.StopRecording();
            capture.Dispose();
        }
        return Task.CompletedTask;
    }

    private void OnAudioDataAvailable(object? sender, float[] samples)
    {
        var capture = _capture;
        if (capture is null) return;
        FrameAvailable?.Invoke(this, new AudioFrameAvailableEventArgs(samples, capture.SampleRate, capture.Channels));
    }

    private void OnDeviceLost(object? sender, string reason)
        => DeviceLost?.Invoke(this, new AudioDeviceLostEventArgs(reason));

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
