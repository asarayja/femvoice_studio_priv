using System.Collections.Concurrent;

namespace FemVoiceStudio.Audio.Abstractions;

/// <summary>
/// Platform-neutral audio OUTPUT (speaker) boundary — the mirror of <see cref="IAudioCaptureService"/>. Used by the
/// "hear your own voice" monitor to play captured microphone frames back to the speaker/headset in real time. It
/// takes ONLY mono float frames in [-1, 1] so no DSP depends on the platform. Every implementation is fail-safe: when
/// no output device/binding exists it reports <see cref="IsAvailable"/> false and all calls are harmless no-ops.
/// </summary>
public interface IAudioPlaybackService : IDisposable
{
    /// <summary>True when a real output device/binding is wired for this OS.</summary>
    bool IsAvailable { get; }

    /// <summary>Open the output device at the given format. Idempotent; safe when unavailable (no-op).</summary>
    void Start(int sampleRate, int channels);

    /// <summary>Enqueue mono float samples for playback (called on the capture thread). Never blocks the caller.</summary>
    void Write(float[] samples);

    /// <summary>Stop playback and release the device.</summary>
    void Stop();
}

/// <summary>
/// Shared base for the real playback backends. It owns a bounded queue + a dedicated playback thread so the capture
/// thread's <see cref="Write"/> never blocks (real-time monitoring): the thread opens the device, drains the queue,
/// converts each frame to interleaved S16, and hands it to <see cref="WriteFrames"/>. A backlog beyond
/// <see cref="MaxQueuedFrames"/> is dropped to keep latency bounded (better a tiny glitch than growing delay). All
/// device access (open/write/close) happens on the ONE playback thread, so the native APIs stay single-threaded.
/// </summary>
public abstract class BufferedAudioPlaybackService : IAudioPlaybackService
{
    private const int MaxQueuedFrames = 24;   // ~ a few hundred ms cap; drops excess to bound latency

    private readonly ConcurrentQueue<float[]> _queue = new();
    private readonly object _gate = new();
    private System.Threading.Thread? _thread;
    private System.Threading.CancellationTokenSource? _cts;
    private System.Threading.AutoResetEvent? _dataReady;
    private volatile bool _open;
    private int _queued;

    public abstract bool IsAvailable { get; }

    /// <summary>Open the native output device on the playback thread. Return false to abort (device unavailable).</summary>
    protected abstract bool OpenDevice(int sampleRate, int channels);
    /// <summary>Write <paramref name="count"/> interleaved S16 samples to the device (playback thread only).</summary>
    protected abstract void WriteFrames(short[] pcm, int count);
    /// <summary>Release the native output device (playback thread only). Best-effort; never throws.</summary>
    protected abstract void CloseDevice();

    public void Start(int sampleRate, int channels)
    {
        if (!IsAvailable) return;
        lock (_gate)
        {
            if (_thread is { IsAlive: true }) return;   // idempotent
            _queue.Clear();
            _queued = 0;
            _dataReady = new System.Threading.AutoResetEvent(false);
            _cts = new System.Threading.CancellationTokenSource();
            var token = _cts.Token;
            int sr = sampleRate <= 0 ? 44100 : sampleRate;
            int ch = channels <= 0 ? 1 : channels;
            _thread = new System.Threading.Thread(() => PlaybackLoop(sr, ch, token))
            {
                IsBackground = true,
                Name = "FemVoice-Playback",
            };
            _thread.Start();
        }
    }

    public void Write(float[] samples)
    {
        if (!_open || samples is null || samples.Length == 0) return;
        // Bounded: drop the oldest-equivalent by simply not enqueueing when the backlog is full.
        if (System.Threading.Volatile.Read(ref _queued) >= MaxQueuedFrames) return;
        _queue.Enqueue(samples);
        System.Threading.Interlocked.Increment(ref _queued);
        _dataReady?.Set();
    }

    public void Stop()
    {
        System.Threading.Thread? thread;
        lock (_gate)
        {
            _cts?.Cancel();
            _dataReady?.Set();
            thread = _thread;
            _thread = null;
        }
        thread?.Join(TimeSpan.FromSeconds(2));
    }

    private void PlaybackLoop(int sampleRate, int channels, System.Threading.CancellationToken token)
    {
        try
        {
            if (!OpenDevice(sampleRate, channels)) return;
            _open = true;
            short[] scratch = System.Array.Empty<short>();
            while (!token.IsCancellationRequested)
            {
                if (!_queue.TryDequeue(out var frame))
                {
                    _dataReady?.WaitOne(100);
                    continue;
                }
                System.Threading.Interlocked.Decrement(ref _queued);
                int n = frame.Length;
                int needed = n * channels;
                if (scratch.Length < needed) scratch = new short[needed];
                // Mono float → interleaved S16 (duplicate to each channel).
                for (int i = 0; i < n; i++)
                {
                    short s = (short)System.Math.Clamp((int)System.Math.Round(frame[i] * 32767f), short.MinValue, short.MaxValue);
                    for (int c = 0; c < channels; c++) scratch[i * channels + c] = s;
                }
                WriteFrames(scratch, needed);
            }
        }
        catch { /* fail-safe: a device error just ends monitoring, never crashes the app */ }
        finally
        {
            _open = false;
            try { CloseDevice(); } catch { /* best effort */ }
        }
    }

    public void Dispose() => Stop();
}

/// <summary>Display-only / headless no-op playback: always unavailable; every call is harmless.</summary>
public sealed class NoopAudioPlaybackService : IAudioPlaybackService
{
    public bool IsAvailable => false;
    public void Start(int sampleRate, int channels) { }
    public void Write(float[] samples) { }
    public void Stop() { }
    public void Dispose() { }
}

/// <summary>
/// Selects the audio OUTPUT backend per OS, mirroring <see cref="AudioCaptureBackendFactory"/>: Linux/ALSA and
/// Windows/winmm live in this assembly; a platform head (Android) injects its own via
/// <see cref="PlatformPlaybackFactory"/>. macOS and unknown OSes get the no-op (silent, safe).
/// </summary>
public static class AudioPlaybackBackendFactory
{
    /// <summary>Optional platform-provided playback backend (e.g. Android's AudioTrack), set by a head at startup.</summary>
    public static Func<IAudioPlaybackService>? PlatformPlaybackFactory { get; set; }

    /// <summary>The runtime output backend: the platform-provided one if a head set it, else the built-in Linux/
    /// Windows one, else a no-op. Never throws.</summary>
    public static IAudioPlaybackService CreateForRuntime()
    {
        try
        {
            if (PlatformPlaybackFactory is not null) return PlatformPlaybackFactory();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                return new Linux.AlsaAudioPlaybackService();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                return new Windows.WinMmAudioPlaybackService();
        }
        catch { /* fall through to no-op */ }
        return new NoopAudioPlaybackService();
    }
}
