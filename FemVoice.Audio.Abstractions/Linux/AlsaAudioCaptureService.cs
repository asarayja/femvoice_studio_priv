using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FemVoiceStudio.Audio.Abstractions.Linux;

/// <summary>
/// Real Linux microphone capture via ALSA (<c>libasound.so.2</c>), behind <see cref="IAudioCaptureService"/>. It
/// captures interleaved S16_LE mono at the requested rate (WPF baseline default 44.1 kHz), converts each frame to
/// mono float in [-1, 1], and raises <see cref="FrameAvailable"/> on a dedicated capture thread — feeding the SAME
/// float-frame contract the Windows adapter and the synthetic backend use, so no DSP/scoring/clinical code depends
/// on the platform.
///
/// It is fail-safe by construction: if <c>libasound</c> is missing or no capture device can be opened (headless CI,
/// no microphone, OS privacy block), <see cref="IsBackendAvailable"/> reports <c>false</c>, enumeration returns
/// empty, and <see cref="StartAsync"/> raises <see cref="DeviceLost"/> and starts NO loop — it never throws to the
/// app and never fabricates frames. Overruns are recovered via <c>snd_pcm_recover</c>; an unrecoverable error
/// raises <see cref="DeviceLost"/> and stops the loop. Only the "default" ALSA capture PCM is used in this slice
/// (routes through PulseAudio/PipeWire when present); per-card enumeration is a follow-up.
/// </summary>
public sealed class AlsaAudioCaptureService : IRealAudioCaptureBackend, IDisposable
{
    private const string DefaultPcm = "default";

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private bool? _availableCache;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    /// <summary>True only when libasound is loadable AND the default capture PCM can be opened + closed cleanly.
    /// Probed once and cached (cheap open/close); never throws.</summary>
    public bool IsBackendAvailable => _availableCache ??= ProbeCanOpenCapture();

    private static bool ProbeCanOpenCapture()
    {
        try
        {
            int err = AlsaInterop.snd_pcm_open(out IntPtr pcm, DefaultPcm, AlsaInterop.SND_PCM_STREAM_CAPTURE, AlsaInterop.OPEN_BLOCKING);
            if (err < 0 || pcm == IntPtr.Zero) return false;
            AlsaInterop.snd_pcm_close(pcm);
            return true;
        }
        catch (DllNotFoundException) { return false; }   // libasound.so.2 not installed
        catch (EntryPointNotFoundException) { return false; }
        catch (Exception ex) { Debug.WriteLine($"AlsaAudioCaptureService probe failed: {ex.Message}"); return false; }
    }

    /// <summary>One "default" input device when capture is available, else empty. Never throws.</summary>
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
        => IsBackendAvailable
            ? new[] { new AudioInputDevice(DefaultPcm, "System default input (ALSA)", true) }
            : Array.Empty<AudioInputDevice>();

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true }) return Task.CompletedTask;   // idempotent

            int sampleRate = options.SampleRate <= 0 ? 44100 : options.SampleRate;
            int channels = options.Channels <= 0 ? 1 : options.Channels;
            int bufferFrames = options.BufferSamples <= 0 ? 1024 : options.BufferSamples;

            IntPtr pcm;
            try
            {
                int err = AlsaInterop.snd_pcm_open(out pcm, DefaultPcm, AlsaInterop.SND_PCM_STREAM_CAPTURE, AlsaInterop.OPEN_BLOCKING);
                if (err < 0 || pcm == IntPtr.Zero)
                {
                    RaiseDeviceLost($"ALSA open failed: {AlsaInterop.StrError(err)}");
                    return Task.CompletedTask;
                }

                err = AlsaInterop.snd_pcm_set_params(pcm, AlsaInterop.SND_PCM_FORMAT_S16_LE, AlsaInterop.SND_PCM_ACCESS_RW_INTERLEAVED,
                    (uint)channels, (uint)sampleRate, softResample: 1, latencyMicros: 100_000);
                if (err < 0)
                {
                    AlsaInterop.snd_pcm_close(pcm);
                    RaiseDeviceLost($"ALSA set_params failed: {AlsaInterop.StrError(err)}");
                    return Task.CompletedTask;
                }
                AlsaInterop.snd_pcm_prepare(pcm);
            }
            catch (DllNotFoundException)
            {
                RaiseDeviceLost("ALSA library (libasound.so.2) not available.");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseDeviceLost($"ALSA start error: {ex.Message}");
                return Task.CompletedTask;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cts.Token;
            _thread = new Thread(() => CaptureLoop(pcm, sampleRate, channels, bufferFrames, token))
            {
                IsBackground = true,
                Name = "FemVoice-ALSA-Capture",
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
        // The loop checks cancellation between reads (~one buffer, tens of ms) then closes the PCM on its own
        // thread, so all libasound access stays single-threaded. Join with a bounded wait to avoid hangs.
        thread?.Join(TimeSpan.FromSeconds(2));
        return Task.CompletedTask;
    }

    private void CaptureLoop(IntPtr pcm, int sampleRate, int channels, int bufferFrames, CancellationToken token)
    {
        int bytesPerFrame = 2 * channels;                 // S16 = 2 bytes/sample
        var raw = new byte[bufferFrames * bytesPerFrame];
        try
        {
            while (!token.IsCancellationRequested)
            {
                long n = AlsaInterop.snd_pcm_readi(pcm, raw, (ulong)bufferFrames);
                if (n < 0)
                {
                    int rec = AlsaInterop.snd_pcm_recover(pcm, (int)n, silent: 1);
                    if (rec < 0)
                    {
                        RaiseDeviceLost($"ALSA read error: {AlsaInterop.StrError((int)n)}");
                        break;
                    }
                    continue;   // recovered (e.g. overrun) — keep capturing
                }
                if (n == 0) continue;

                int frames = (int)n;
                // Down-mix interleaved channels to mono float in [-1, 1] (matches the mono float-frame contract).
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
        }
        catch (Exception ex)
        {
            RaiseDeviceLost($"ALSA capture loop error: {ex.Message}");
        }
        finally
        {
            try { AlsaInterop.snd_pcm_drop(pcm); } catch { /* best effort */ }
            try { AlsaInterop.snd_pcm_close(pcm); } catch { /* best effort */ }
        }
    }

    private void RaiseDeviceLost(string reason)
    {
        Debug.WriteLine($"AlsaAudioCaptureService: {reason}");
        DeviceLost?.Invoke(this, new AudioDeviceLostEventArgs(reason));
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
