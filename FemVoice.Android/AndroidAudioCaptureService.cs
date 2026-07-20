using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;
using FemVoiceStudio.Audio.Abstractions;

namespace FemVoice.Android;

/// <summary>
/// Real Android microphone capture via <see cref="AudioRecord"/> (the platform's PCM capture API), behind
/// <see cref="IAudioCaptureService"/>. It captures S16 mono at the requested rate, converts each buffer to mono
/// float in [-1, 1], and raises <see cref="FrameAvailable"/> on a dedicated capture thread — feeding the SAME
/// float-frame contract the Linux/ALSA and Windows/winmm backends use, so no DSP/scoring/clinical code depends on
/// the platform. Wired from <c>MainActivity</c> via <see cref="AudioCaptureBackendFactory.PlatformRealBackendFactory"/>.
///
/// Device enumeration uses <see cref="AudioManager"/> so the Settings picker shows the real inputs (built-in mic,
/// wired/Bluetooth headset), and <see cref="AudioCaptureOptions.DeviceId"/> routes capture to a chosen one via
/// <see cref="AudioRecord.SetPreferredDevice"/>. Fail-safe: any error → <see cref="DeviceLost"/>, no throw, no frames.
/// The RECORD_AUDIO runtime permission is requested by <c>MainActivity</c>; without it, capture yields no frames.
/// </summary>
public sealed class AndroidAudioCaptureService : IRealAudioCaptureBackend, IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private AudioRecord? _recorder;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    /// <summary>True when the device exposes at least one audio input (essentially always on a phone). Never throws.</summary>
    public bool IsBackendAvailable
    {
        get { try { return GetInputDevices().Count > 0; } catch { return true; } }
    }

    /// <summary>Real input devices (built-in mic, wired/Bluetooth headset, USB) via AudioManager, plus a leading
    /// "System default input" entry. Empty on error. Never throws.</summary>
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        var list = new List<AudioInputDevice> { new("default", "Systemstandard (Android)", true) };
        try
        {
            var ctx = global::Android.App.Application.Context;
            if (ctx?.GetSystemService(global::Android.Content.Context.AudioService) is AudioManager am)
            {
                var devices = am.GetDevices(GetDevicesTargets.Inputs);
                if (devices != null)
                {
                    foreach (var d in devices)
                    {
                        if (d is null) continue;
                        string name = !string.IsNullOrWhiteSpace(d.ProductName?.ToString())
                            ? $"{d.ProductName} ({DescribeType(d.Type)})"
                            : DescribeType(d.Type);
                        list.Add(new AudioInputDevice(d.Id.ToString(), name, false));
                    }
                }
            }
        }
        catch { /* enumeration is best-effort; the default entry always remains */ }
        return list;
    }

    private static string DescribeType(AudioDeviceType type) => type switch
    {
        AudioDeviceType.BuiltinMic => "Innebygd mikrofon",
        AudioDeviceType.BluetoothSco => "Bluetooth",
        AudioDeviceType.BluetoothA2dp => "Bluetooth",
        AudioDeviceType.WiredHeadset => "Kablet headset",
        AudioDeviceType.UsbDevice => "USB",
        AudioDeviceType.UsbHeadset => "USB-headset",
        _ => "Inngang",
    };

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true }) return Task.CompletedTask;   // idempotent

            int sampleRate = options.SampleRate <= 0 ? 44100 : options.SampleRate;
            int requested = options.BufferSamples <= 0 ? 1024 : options.BufferSamples;

            try
            {
                int minBytes = AudioRecord.GetMinBufferSize(sampleRate, ChannelIn.Mono, Encoding.Pcm16bit);
                if (minBytes <= 0) minBytes = sampleRate * 2;   // 1 s fallback
                int bufBytes = Math.Max(minBytes, requested * 2 * 2);   // headroom

                var recorder = new AudioRecord(AudioSource.Mic, sampleRate, ChannelIn.Mono, Encoding.Pcm16bit, bufBytes);
                if (recorder.State != State.Initialized)
                {
                    recorder.Release();
                    RaiseDeviceLost("AudioRecord could not be initialized (microphone busy or permission denied).");
                    return Task.CompletedTask;
                }

                TryRouteToPreferredDevice(recorder, options.DeviceId);

                recorder.StartRecording();
                if (recorder.RecordingState != RecordState.Recording)
                {
                    recorder.Release();
                    RaiseDeviceLost("AudioRecord failed to start recording (permission denied?).");
                    return Task.CompletedTask;
                }

                _recorder = recorder;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = _cts.Token;
                int readShorts = Math.Max(256, requested);
                _thread = new Thread(() => CaptureLoop(recorder, sampleRate, readShorts, token))
                {
                    IsBackground = true,
                    Name = "FemVoice-Android-Capture",
                };
                _thread.Start();
            }
            catch (Exception ex)
            {
                RaiseDeviceLost($"Android capture start error: {ex.Message}");
            }
        }
        return Task.CompletedTask;
    }

    // Route capture to the chosen input device (API 23+) when a specific one is selected. Best-effort.
    private static void TryRouteToPreferredDevice(AudioRecord recorder, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId == "default") return;
        if (!int.TryParse(deviceId, out int id)) return;
        try
        {
            var ctx = global::Android.App.Application.Context;
            if (ctx?.GetSystemService(global::Android.Content.Context.AudioService) is AudioManager am)
            {
                foreach (var d in am.GetDevices(GetDevicesTargets.Inputs) ?? Array.Empty<AudioDeviceInfo>())
                    if (d != null && d.Id == id) { recorder.SetPreferredDevice(d); return; }
            }
        }
        catch { /* routing is best-effort; default device is used otherwise */ }
    }

    private void CaptureLoop(AudioRecord recorder, int sampleRate, int readShorts, CancellationToken token)
    {
        var buffer = new short[readShorts];
        try
        {
            while (!token.IsCancellationRequested)
            {
                int n = recorder.Read(buffer, 0, buffer.Length);
                if (n < 0)
                {
                    RaiseDeviceLost($"AudioRecord read error ({n}).");
                    break;
                }
                if (n == 0) continue;

                var samples = new float[n];
                for (int i = 0; i < n; i++) samples[i] = buffer[i] / 32768f;
                FrameAvailable?.Invoke(this, new AudioFrameAvailableEventArgs(samples, sampleRate, 1));
            }
        }
        catch (Exception ex)
        {
            RaiseDeviceLost($"Android capture loop error: {ex.Message}");
        }
    }

    public Task StopAsync()
    {
        Thread? thread;
        AudioRecord? recorder;
        lock (_gate)
        {
            _cts?.Cancel();
            thread = _thread;
            recorder = _recorder;
            _thread = null;
            _recorder = null;
        }
        thread?.Join(TimeSpan.FromSeconds(2));
        if (recorder is not null)
        {
            try { recorder.Stop(); } catch { /* best effort */ }
            try { recorder.Release(); } catch { /* best effort */ }
        }
        return Task.CompletedTask;
    }

    private void RaiseDeviceLost(string reason)
        => DeviceLost?.Invoke(this, new AudioDeviceLostEventArgs(reason));

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
