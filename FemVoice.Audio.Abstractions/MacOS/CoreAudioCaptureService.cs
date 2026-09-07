using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FemVoiceStudio.Audio.Abstractions.MacOS;

/// <summary>
/// Real macOS microphone capture via <b>AudioQueue</b> (AudioToolbox), behind <see cref="IAudioCaptureService"/>.
/// It records interleaved signed 16-bit PCM at the requested rate (WPF baseline default 44.1 kHz), converts each
/// buffer to mono float in [-1, 1], and raises <see cref="FrameAvailable"/> — feeding the SAME float-frame
/// contract the ALSA, winmm and synthetic backends use, so no DSP/scoring/clinical code depends on the platform.
///
/// Fail-safe by construction, exactly like the ALSA backend: when AudioToolbox cannot be loaded (i.e. not macOS)
/// or no input queue can be created (no microphone, or macOS denied microphone access),
/// <see cref="IsBackendAvailable"/> reports <c>false</c>, enumeration returns empty, and
/// <see cref="StartAsync"/> raises <see cref="DeviceLost"/> and starts NOTHING — it never throws to the app and
/// never fabricates frames.
///
/// <para><b>Microphone permission.</b> macOS gates the microphone through TCC. Creating the input queue triggers
/// the system prompt, using the <c>NSMicrophoneUsageDescription</c> string from the app bundle's Info.plist. When
/// permission is refused, macOS does not fail the call — it delivers <i>digital silence</i> instead, which is
/// indistinguishable from a working-but-quiet microphone unless you look for it. This backend therefore watches
/// the opening seconds for perfectly zero samples and raises <see cref="DeviceLost"/> with an actionable message
/// rather than leaving the user staring at a meter that never moves. A real microphone always carries some noise
/// floor, so exact zeros for that long mean the OS is withholding audio.</para>
///
/// Only the system default input is used in this slice (device selection is a follow-up), matching the ALSA
/// backend's single "default" device rather than inventing a second, differently-shaped behaviour on macOS.
/// </summary>
public sealed class CoreAudioCaptureService : IRealAudioCaptureBackend, IDisposable
{
    private const string DefaultDeviceId = "default";

    /// <summary>Number of AudioQueue buffers kept in flight. Three is the conventional choice: one being filled
    /// by the driver, one in the callback, one spare, so a slow callback never starves the queue.</summary>
    private const int BufferCount = 3;

    private readonly object _gate = new();
    private bool? _availableCache;

    private IntPtr _queue;
    // The callback delegate MUST be kept alive for as long as the native queue can invoke it. A local would be
    // collected while AudioQueue still holds the function pointer, crashing the process from the audio thread.
    private CoreAudioInterop.AudioQueueInputCallback? _callback;
    private int _sampleRate = 44100;
    private int _channels = 1;
    private volatile bool _running;

    // Silence watchdog (see the permission note above). Counts frames seen since Start until either real audio
    // arrives or the threshold is crossed; then it stops counting for the rest of the session.
    private long _framesSeen;
    private bool _sawNonZeroAudio;
    private bool _silenceReported;

    public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
    public event EventHandler<AudioDeviceLostEventArgs>? DeviceLost;

    /// <summary>True only when AudioToolbox is loadable AND an input queue can actually be created and disposed.
    /// Probed once and cached; never throws.</summary>
    public bool IsBackendAvailable => _availableCache ??= ProbeCanOpenCapture();

    private static bool ProbeCanOpenCapture()
    {
        try
        {
            var format = BuildFormat(44100, 1);
            // A no-op callback: the probe never starts the queue, so this is never invoked. It still must be a
            // rooted delegate for the duration of the call.
            CoreAudioInterop.AudioQueueInputCallback noop = static (_, _, _, _, _, _) => { };
            int status = CoreAudioInterop.AudioQueueNewInput(
                ref format, noop, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out IntPtr aq);
            GC.KeepAlive(noop);
            if (status != 0 || aq == IntPtr.Zero) return false;
            CoreAudioInterop.AudioQueueDispose(aq, true);
            return true;
        }
        catch (DllNotFoundException) { return false; }          // not macOS
        catch (EntryPointNotFoundException) { return false; }
        catch (Exception ex) { Debug.WriteLine($"CoreAudioCaptureService probe failed: {ex.Message}"); return false; }
    }

    private static CoreAudioInterop.AudioStreamBasicDescription BuildFormat(int sampleRate, int channels)
    {
        uint bytesPerFrame = (uint)(2 * channels);   // S16 = 2 bytes per sample
        return new CoreAudioInterop.AudioStreamBasicDescription
        {
            mSampleRate = sampleRate,
            mFormatID = CoreAudioInterop.kAudioFormatLinearPCM,
            mFormatFlags = CoreAudioInterop.LinearPcmS16Flags,
            mBytesPerPacket = bytesPerFrame,        // linear PCM: one frame per packet
            mFramesPerPacket = 1,
            mBytesPerFrame = bytesPerFrame,
            mChannelsPerFrame = (uint)channels,
            mBitsPerChannel = 16,
            mReserved = 0,
        };
    }

    /// <summary>One "default" input device when capture is available, else empty. Never throws.</summary>
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
        => IsBackendAvailable
            ? new[] { new AudioInputDevice(DefaultDeviceId, "System default input (CoreAudio)", true) }
            : Array.Empty<AudioInputDevice>();

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_running) return Task.CompletedTask;   // idempotent, like the other backends

            int sampleRate = options.SampleRate <= 0 ? 44100 : options.SampleRate;
            int channels = options.Channels <= 0 ? 1 : options.Channels;
            int bufferFrames = options.BufferSamples <= 0 ? 1024 : options.BufferSamples;

            try
            {
                var format = BuildFormat(sampleRate, channels);
                // Rooted in a field, not a local: AudioQueue keeps the pointer for the queue's lifetime.
                _callback = OnInputBuffer;

                int status = CoreAudioInterop.AudioQueueNewInput(
                    ref format, _callback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out IntPtr aq);
                if (status != 0 || aq == IntPtr.Zero)
                {
                    _callback = null;
                    RaiseDeviceLost($"CoreAudio could not open an input queue ({CoreAudioInterop.DescribeStatus(status)}). " +
                                    "Check System Settings → Privacy & Security → Microphone.");
                    return Task.CompletedTask;
                }

                uint bufferBytes = (uint)(bufferFrames * 2 * channels);
                for (int i = 0; i < BufferCount; i++)
                {
                    int allocStatus = CoreAudioInterop.AudioQueueAllocateBuffer(aq, bufferBytes, out IntPtr buffer);
                    if (allocStatus != 0 || buffer == IntPtr.Zero)
                    {
                        CoreAudioInterop.AudioQueueDispose(aq, true);
                        _callback = null;
                        RaiseDeviceLost($"CoreAudio buffer allocation failed ({CoreAudioInterop.DescribeStatus(allocStatus)}).");
                        return Task.CompletedTask;
                    }
                    CoreAudioInterop.AudioQueueEnqueueBuffer(aq, buffer, 0, IntPtr.Zero);
                }

                _sampleRate = sampleRate;
                _channels = channels;
                _framesSeen = 0;
                _sawNonZeroAudio = false;
                _silenceReported = false;
                _queue = aq;
                _running = true;

                int startStatus = CoreAudioInterop.AudioQueueStart(aq, IntPtr.Zero);
                if (startStatus != 0)
                {
                    _running = false;
                    _queue = IntPtr.Zero;
                    CoreAudioInterop.AudioQueueDispose(aq, true);
                    _callback = null;
                    RaiseDeviceLost($"CoreAudio could not start capture ({CoreAudioInterop.DescribeStatus(startStatus)}).");
                    return Task.CompletedTask;
                }
            }
            catch (DllNotFoundException)
            {
                RaiseDeviceLost("AudioToolbox is not available (this backend only runs on macOS).");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseDeviceLost($"CoreAudio start error: {ex.Message}");
                return Task.CompletedTask;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>AudioQueue capture callback — runs on an AudioQueue-owned thread, mirroring the ALSA backend
    /// raising <see cref="FrameAvailable"/> on its capture thread. Copies the PCM out, re-enqueues the buffer,
    /// and never lets an exception escape into native code (that would terminate the process).</summary>
    private void OnInputBuffer(IntPtr userData, IntPtr aq, IntPtr bufferPtr, IntPtr startTime, uint packetCount, IntPtr packetDescs)
    {
        try
        {
            if (!_running || bufferPtr == IntPtr.Zero) return;

            var buffer = Marshal.PtrToStructure<CoreAudioInterop.AudioQueueBuffer>(bufferPtr);
            int byteCount = (int)buffer.mAudioDataByteSize;
            if (byteCount > 0 && buffer.mAudioData != IntPtr.Zero)
            {
                int channels = _channels;
                int bytesPerFrame = 2 * channels;
                int frames = byteCount / bytesPerFrame;
                if (frames > 0)
                {
                    var raw = new byte[byteCount];
                    Marshal.Copy(buffer.mAudioData, raw, 0, byteCount);

                    // Down-mix interleaved channels to mono float in [-1, 1] — identical to the ALSA path.
                    var samples = new float[frames];
                    bool anyNonZero = false;
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
                        if (acc != 0) anyNonZero = true;
                        samples[f] = acc / (channels * 32768f);
                    }

                    CheckForWithheldAudio(frames, anyNonZero);
                    FrameAvailable?.Invoke(this, new AudioFrameAvailableEventArgs(samples, _sampleRate, 1));
                }
            }

            // Hand the buffer back so the driver can refill it; without this, capture stops after BufferCount buffers.
            if (_running) CoreAudioInterop.AudioQueueEnqueueBuffer(aq, bufferPtr, 0, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            // An exception crossing back into native AudioQueue code would tear down the process.
            Debug.WriteLine($"CoreAudioCaptureService callback error: {ex.Message}");
        }
    }

    /// <summary>
    /// Detect macOS withholding audio (microphone permission refused), which surfaces as perfectly zero samples
    /// rather than an error. A working microphone always has a noise floor, so exact digital silence for the
    /// first couple of seconds means the OS is not giving us the signal. Reported once per session as
    /// <see cref="DeviceLost"/> so the UI can say something actionable instead of showing a dead meter.
    /// </summary>
    private void CheckForWithheldAudio(int frames, bool anyNonZero)
    {
        if (_sawNonZeroAudio || _silenceReported) return;
        if (anyNonZero) { _sawNonZeroAudio = true; return; }

        _framesSeen += frames;
        if (_framesSeen < _sampleRate * 2L) return;   // ~2 seconds of uninterrupted digital silence

        _silenceReported = true;
        RaiseDeviceLost("macOS is delivering digital silence from the microphone — this normally means microphone " +
                        "access was refused. Enable FemVoice Studio under System Settings → Privacy & Security → " +
                        "Microphone, then restart the app.");
    }

    public Task StopAsync()
    {
        IntPtr aq;
        lock (_gate)
        {
            if (!_running) return Task.CompletedTask;
            _running = false;          // stops the callback re-enqueuing before the queue goes away
            aq = _queue;
            _queue = IntPtr.Zero;
        }

        if (aq != IntPtr.Zero)
        {
            // Stop with immediate:true blocks until the queue is stopped and no callback is in flight, so the
            // dispose below cannot race a running callback.
            try { CoreAudioInterop.AudioQueueStop(aq, true); } catch { /* best effort */ }
            try { CoreAudioInterop.AudioQueueDispose(aq, true); } catch { /* best effort */ }
        }
        _callback = null;              // safe only after Dispose: native no longer holds the pointer
        return Task.CompletedTask;
    }

    private void RaiseDeviceLost(string reason)
    {
        Debug.WriteLine($"CoreAudioCaptureService: {reason}");
        DeviceLost?.Invoke(this, new AudioDeviceLostEventArgs(reason));
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
