using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FemVoiceStudio.Audio.Abstractions.Windows;

/// <summary>
/// Real Windows microphone capture via the multimedia <c>waveIn</c> API (<c>winmm.dll</c>), behind
/// <see cref="IAudioCaptureService"/>. It captures interleaved S16_LE PCM at the requested rate (WPF baseline
/// 44.1 kHz), down-mixes each buffer to mono float in [-1, 1], and raises <see cref="FrameAvailable"/> on a
/// dedicated capture thread — feeding the SAME float-frame contract the Linux/ALSA and synthetic backends use, so no
/// DSP/scoring/clinical code depends on the platform.
///
/// It mirrors the ALSA backend's guarantees: fail-safe by construction. If <c>winmm</c> is missing (non-Windows) or
/// no device can be opened (no microphone, OS privacy block, device busy), <see cref="IsBackendAvailable"/> reports
/// <c>false</c>, enumeration returns empty, and <see cref="StartAsync"/> raises <see cref="DeviceLost"/> and starts
/// NO loop — it never throws to the app and never fabricates frames.
///
/// Device routing is honoured: <see cref="AudioCaptureOptions.DeviceId"/> is the <c>waveIn</c> device index as a
/// string (or "default"/null → <c>WAVE_MAPPER</c>, the system-default input). <see cref="GetInputDevices"/> exposes a
/// "System default input" entry plus every physical input device by its real driver name, so the UI can both display
/// which microphone is active and let the user pick a specific one.
/// </summary>
public sealed class WinMmAudioCaptureService : IRealAudioCaptureBackend, IDisposable
{
    private const int BufferCount = 4;   // small ring of capture buffers so the driver always has one to fill

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private bool? _availableCache;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    /// <summary>True only when winmm is loadable AND at least one input device is present. Probed once and cached
    /// (cheap device count + a test open/close of the default device); never throws.</summary>
    public bool IsBackendAvailable => _availableCache ??= ProbeCanCapture();

    private static bool ProbeCanCapture()
    {
        try
        {
            if (WinMmInterop.waveInGetNumDevs() == 0) return false;
            // Confirm the default device can actually be opened with the baseline format, then release it. A busy
            // device (already open elsewhere) still counts as present, so treat "allocated" as available.
            var fmt = MakeFormat(44100, 1);
            uint res = WinMmInterop.waveInOpen(out IntPtr hwi, WinMmInterop.WAVE_MAPPER, ref fmt,
                IntPtr.Zero, IntPtr.Zero, 0 /* CALLBACK_NULL */);
            if (res == WinMmInterop.MMSYSERR_NOERROR && hwi != IntPtr.Zero)
            {
                WinMmInterop.waveInClose(hwi);
                return true;
            }
            const uint MMSYSERR_ALLOCATED = 4;   // device present but busy — still a real device
            return res == MMSYSERR_ALLOCATED;
        }
        catch (DllNotFoundException) { return false; }          // winmm.dll absent (non-Windows)
        catch (EntryPointNotFoundException) { return false; }
        catch (Exception ex) { Debug.WriteLine($"WinMmAudioCaptureService probe failed: {ex.Message}"); return false; }
    }

    /// <summary>A "System default input" entry (routes via WAVE_MAPPER) plus every physical input device by its real
    /// driver name. Empty when no backend/device is available. Never throws.</summary>
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        try
        {
            uint count = WinMmInterop.waveInGetNumDevs();
            if (count == 0) return Array.Empty<AudioInputDevice>();

            var list = new List<AudioInputDevice>((int)count + 1)
            {
                new AudioInputDevice("default", "System default input (Windows)", true),
            };
            for (uint i = 0; i < count; i++)
            {
                var caps = default(WinMmInterop.WAVEINCAPSW);
                uint res = WinMmInterop.waveInGetDevCapsW((IntPtr)i, ref caps,
                    (uint)Marshal.SizeOf<WinMmInterop.WAVEINCAPSW>());
                string name = res == WinMmInterop.MMSYSERR_NOERROR && !string.IsNullOrWhiteSpace(caps.szPname)
                    ? caps.szPname
                    : $"Input device {i}";
                list.Add(new AudioInputDevice(i.ToString(), name, false));
            }
            return list;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WinMmAudioCaptureService enumerate failed: {ex.Message}");
            return Array.Empty<AudioInputDevice>();
        }
    }

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true }) return Task.CompletedTask;   // idempotent

            int sampleRate = options.SampleRate <= 0 ? 44100 : options.SampleRate;
            int channels = options.Channels <= 0 ? 1 : options.Channels;
            int bufferFrames = options.BufferSamples <= 0 ? 1024 : options.BufferSamples;
            uint deviceId = ResolveDeviceId(options.DeviceId);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cts.Token;
            _thread = new Thread(() => CaptureLoop(deviceId, sampleRate, channels, bufferFrames, token))
            {
                IsBackground = true,
                Name = "FemVoice-WinMM-Capture",
            };
            _thread.Start();
        }
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Thread? thread;
        lock (_gate)
        {
            _cts?.Cancel();
            thread = _thread;
            _thread = null;
        }
        // The loop wakes on cancellation (its wait is bounded), stops/resets/closes the device on its own thread, so
        // all winmm access stays single-threaded. Join with a bounded wait to avoid hangs.
        thread?.Join(TimeSpan.FromSeconds(2));
        return Task.CompletedTask;
    }

    /// <summary>"default"/null/empty → WAVE_MAPPER; a numeric string → that device index; anything else → WAVE_MAPPER.</summary>
    private static uint ResolveDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId == "default") return WinMmInterop.WAVE_MAPPER;
        return uint.TryParse(deviceId, out uint idx) ? idx : WinMmInterop.WAVE_MAPPER;
    }

    private static WinMmInterop.WAVEFORMATEX MakeFormat(int sampleRate, int channels)
    {
        ushort bits = 16;
        ushort blockAlign = (ushort)(channels * (bits / 8));
        return new WinMmInterop.WAVEFORMATEX
        {
            wFormatTag = WinMmInterop.WAVE_FORMAT_PCM,
            nChannels = (ushort)channels,
            nSamplesPerSec = (uint)sampleRate,
            nAvgBytesPerSec = (uint)(sampleRate * blockAlign),
            nBlockAlign = blockAlign,
            wBitsPerSample = bits,
            cbSize = 0,
        };
    }

    private void CaptureLoop(uint deviceId, int sampleRate, int channels, int bufferFrames, CancellationToken token)
    {
        IntPtr hwi = IntPtr.Zero;
        var headers = new IntPtr[BufferCount];
        var datas = new IntPtr[BufferCount];
        int bytesPerFrame = 2 * channels;                 // S16 = 2 bytes/sample
        uint bufferBytes = (uint)(bufferFrames * bytesPerFrame);
        // AutoReset event the driver signals whenever a buffer finishes; the loop waits on it, bounded, so cancellation
        // is observed promptly even if no audio arrives.
        using var dataReady = new AutoResetEvent(false);

        try
        {
            var fmt = MakeFormat(sampleRate, channels);
            uint res = WinMmInterop.waveInOpen(out hwi, deviceId, ref fmt,
                dataReady.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, WinMmInterop.CALLBACK_EVENT);
            if (res != WinMmInterop.MMSYSERR_NOERROR || hwi == IntPtr.Zero)
            {
                RaiseDeviceLost($"waveInOpen failed (mmresult {res}).");
                return;
            }

            uint hdrSize = (uint)Marshal.SizeOf<WinMmInterop.WAVEHDR>();
            for (int i = 0; i < BufferCount; i++)
            {
                datas[i] = Marshal.AllocHGlobal((int)bufferBytes);
                var hdr = new WinMmInterop.WAVEHDR { lpData = datas[i], dwBufferLength = bufferBytes };
                headers[i] = Marshal.AllocHGlobal((int)hdrSize);
                Marshal.StructureToPtr(hdr, headers[i], false);
                if (WinMmInterop.waveInPrepareHeader(hwi, headers[i], hdrSize) != WinMmInterop.MMSYSERR_NOERROR ||
                    WinMmInterop.waveInAddBuffer(hwi, headers[i], hdrSize) != WinMmInterop.MMSYSERR_NOERROR)
                {
                    RaiseDeviceLost("waveIn buffer setup failed.");
                    return;
                }
            }

            if (WinMmInterop.waveInStart(hwi) != WinMmInterop.MMSYSERR_NOERROR)
            {
                RaiseDeviceLost("waveInStart failed.");
                return;
            }

            while (!token.IsCancellationRequested)
            {
                // Bounded wait so cancellation is observed even in silence; a signal means ≥1 buffer is done.
                dataReady.WaitOne(200);
                for (int i = 0; i < BufferCount && !token.IsCancellationRequested; i++)
                {
                    var hdr = Marshal.PtrToStructure<WinMmInterop.WAVEHDR>(headers[i]);
                    if ((hdr.dwFlags & WinMmInterop.WHDR_DONE) == 0) continue;

                    int recordedFrames = (int)(hdr.dwBytesRecorded / bytesPerFrame);
                    if (recordedFrames > 0)
                    {
                        var raw = new byte[hdr.dwBytesRecorded];
                        Marshal.Copy(hdr.lpData, raw, 0, (int)hdr.dwBytesRecorded);
                        EmitFrame(raw, recordedFrames, channels, bytesPerFrame, sampleRate);
                    }
                    // Recycle this buffer back to the driver (clears WHDR_DONE).
                    if (WinMmInterop.waveInAddBuffer(hwi, headers[i], hdrSize) != WinMmInterop.MMSYSERR_NOERROR)
                    {
                        RaiseDeviceLost("waveInAddBuffer (recycle) failed — device lost.");
                        return;
                    }
                }
            }
        }
        catch (DllNotFoundException)
        {
            RaiseDeviceLost("Windows multimedia library (winmm.dll) not available.");
        }
        catch (Exception ex)
        {
            RaiseDeviceLost($"waveIn capture loop error: {ex.Message}");
        }
        finally
        {
            if (hwi != IntPtr.Zero)
            {
                try { WinMmInterop.waveInStop(hwi); } catch { /* best effort */ }
                try { WinMmInterop.waveInReset(hwi); } catch { /* best effort */ }  // returns all buffers as done
                uint hdrSize = (uint)Marshal.SizeOf<WinMmInterop.WAVEHDR>();
                for (int i = 0; i < BufferCount; i++)
                {
                    if (headers[i] != IntPtr.Zero)
                    {
                        try { WinMmInterop.waveInUnprepareHeader(hwi, headers[i], hdrSize); } catch { /* best effort */ }
                        Marshal.FreeHGlobal(headers[i]);
                    }
                    if (datas[i] != IntPtr.Zero) Marshal.FreeHGlobal(datas[i]);
                }
                try { WinMmInterop.waveInClose(hwi); } catch { /* best effort */ }
            }
        }
    }

    /// <summary>Down-mix an interleaved S16_LE buffer to mono float in [-1, 1] and raise <see cref="FrameAvailable"/>.</summary>
    private void EmitFrame(byte[] raw, int frames, int channels, int bytesPerFrame, int sampleRate)
    {
        var samples = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            int acc = 0;
            int baseIdx = f * bytesPerFrame;
            for (int c = 0; c < channels; c++)
            {
                int bi = baseIdx + c * 2;
                short s = (short)(raw[bi] | (raw[bi + 1] << 8));
                acc += s;
            }
            samples[f] = acc / (channels * 32768f);
        }
        FrameAvailable?.Invoke(this, new AudioFrameAvailableEventArgs(samples, sampleRate, 1));
    }

    private void RaiseDeviceLost(string reason)
    {
        Debug.WriteLine($"WinMmAudioCaptureService: {reason}");
        DeviceLost?.Invoke(this, new AudioDeviceLostEventArgs(reason));
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
