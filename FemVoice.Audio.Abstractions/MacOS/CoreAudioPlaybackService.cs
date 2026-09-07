using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FemVoiceStudio.Audio.Abstractions.MacOS;

/// <summary>
/// Real macOS audio OUTPUT via <b>AudioQueue</b> (AudioToolbox) — the speaker side of
/// <see cref="CoreAudioCaptureService"/>, used by the "hear your own voice" monitor. Before this existed macOS fell
/// through to <see cref="NoopAudioPlaybackService"/>, so monitoring was simply silent on a Mac with nothing saying why.
///
/// All the threading, the bounded queue and the mono-float → interleaved-S16 conversion live in
/// <see cref="BufferedAudioPlaybackService"/>; this class only opens the device, writes PCM and closes it, and every
/// one of those runs on the single playback thread the base class owns.
///
/// <para><b>Buffer recycling.</b> An output AudioQueue owns its buffers: you fill one, enqueue it, and AudioQueue
/// hands it back through the output callback once it has been played. This keeps a small pool of free buffers and
/// blocks briefly in <see cref="WriteFrames"/> when they are all in flight — which is the correct backpressure,
/// because the base class has already bounded the queue and drops frames rather than letting latency grow.</para>
///
/// Fail-safe like every other backend: off macOS (or with no output device) <see cref="IsAvailable"/> is false, the
/// base class then never starts a thread, and every call is a harmless no-op. It never throws to the app.
/// </summary>
public sealed class CoreAudioPlaybackService : BufferedAudioPlaybackService
{
    /// <summary>Buffers in the pool. Three matches the capture side: one playing, one queued, one being filled.</summary>
    private const int BufferCount = 3;

    /// <summary>Capacity of each buffer. The monitor writes one capture frame at a time (1024 samples by default,
    /// 2 KB mono S16); 16 KB leaves generous headroom for larger frames or multi-channel output without
    /// reallocating, and an oversized write is clamped rather than overrunning native memory.</summary>
    private const int BufferBytes = 16 * 1024;

    /// <summary>Offset of <c>mAudioDataByteSize</c> inside <c>AudioQueueBuffer</c>. Computed rather than hardcoded:
    /// this is the one field that must be written back before enqueueing, and CoreAudioCaptureServiceTests pins the
    /// offset against Apple's C definition so a layout mistake fails a test instead of corrupting audio.</summary>
    private static readonly int ByteSizeOffset =
        (int)Marshal.OffsetOf<CoreAudioInterop.AudioQueueBuffer>(nameof(CoreAudioInterop.AudioQueueBuffer.mAudioDataByteSize));

    private readonly ConcurrentQueue<IntPtr> _freeBuffers = new();
    private readonly AutoResetEvent _bufferReturned = new(false);

    private IntPtr _queue;
    // Rooted for the queue's lifetime: AudioQueue keeps the function pointer, and a collected delegate would crash
    // the process from the audio thread.
    private CoreAudioInterop.AudioQueueOutputCallback? _callback;
    private volatile bool _open;
    private bool? _availableCache;

    /// <summary>True only when AudioToolbox is loadable AND an output queue can actually be created and disposed.
    /// Probed once and cached; never throws.</summary>
    public override bool IsAvailable => _availableCache ??= ProbeCanOpenOutput();

    private static bool ProbeCanOpenOutput()
    {
        try
        {
            var format = BuildFormat(44100, 1);
            CoreAudioInterop.AudioQueueOutputCallback noop = static (_, _, _) => { };
            int status = CoreAudioInterop.AudioQueueNewOutput(
                ref format, noop, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out IntPtr aq);
            GC.KeepAlive(noop);
            if (status != 0 || aq == IntPtr.Zero) return false;
            CoreAudioInterop.AudioQueueDispose(aq, true);
            return true;
        }
        catch (DllNotFoundException) { return false; }          // not macOS
        catch (EntryPointNotFoundException) { return false; }
        catch (Exception ex) { Debug.WriteLine($"CoreAudioPlaybackService probe failed: {ex.Message}"); return false; }
    }

    private static CoreAudioInterop.AudioStreamBasicDescription BuildFormat(int sampleRate, int channels)
    {
        uint bytesPerFrame = (uint)(2 * channels);
        return new CoreAudioInterop.AudioStreamBasicDescription
        {
            mSampleRate = sampleRate,
            mFormatID = CoreAudioInterop.kAudioFormatLinearPCM,
            mFormatFlags = CoreAudioInterop.LinearPcmS16Flags,
            mBytesPerPacket = bytesPerFrame,
            mFramesPerPacket = 1,
            mBytesPerFrame = bytesPerFrame,
            mChannelsPerFrame = (uint)channels,
            mBitsPerChannel = 16,
            mReserved = 0,
        };
    }

    protected override bool OpenDevice(int sampleRate, int channels)
    {
        try
        {
            var format = BuildFormat(sampleRate, channels);
            _callback = OnBufferPlayed;

            int status = CoreAudioInterop.AudioQueueNewOutput(
                ref format, _callback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out IntPtr aq);
            if (status != 0 || aq == IntPtr.Zero)
            {
                Debug.WriteLine($"CoreAudioPlaybackService: output queue failed ({CoreAudioInterop.DescribeStatus(status)}).");
                _callback = null;
                return false;
            }

            while (_freeBuffers.TryDequeue(out _)) { }   // a restart must not reuse buffers from the old queue
            for (int i = 0; i < BufferCount; i++)
            {
                int allocStatus = CoreAudioInterop.AudioQueueAllocateBuffer(aq, BufferBytes, out IntPtr buffer);
                if (allocStatus != 0 || buffer == IntPtr.Zero)
                {
                    CoreAudioInterop.AudioQueueDispose(aq, true);
                    _callback = null;
                    Debug.WriteLine($"CoreAudioPlaybackService: buffer allocation failed ({CoreAudioInterop.DescribeStatus(allocStatus)}).");
                    return false;
                }
                _freeBuffers.Enqueue(buffer);
            }

            int startStatus = CoreAudioInterop.AudioQueueStart(aq, IntPtr.Zero);
            if (startStatus != 0)
            {
                CoreAudioInterop.AudioQueueDispose(aq, true);
                _callback = null;
                Debug.WriteLine($"CoreAudioPlaybackService: start failed ({CoreAudioInterop.DescribeStatus(startStatus)}).");
                return false;
            }

            _queue = aq;
            _open = true;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;   // not macOS — the base class then never starts the loop
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CoreAudioPlaybackService open error: {ex.Message}");
            return false;
        }
    }

    /// <summary>AudioQueue returns a played buffer here, on its own thread. Return it to the pool and wake any
    /// write that is waiting for one. Never let an exception escape into native code.</summary>
    private void OnBufferPlayed(IntPtr userData, IntPtr aq, IntPtr buffer)
    {
        try
        {
            if (!_open || buffer == IntPtr.Zero) return;
            _freeBuffers.Enqueue(buffer);
            _bufferReturned.Set();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CoreAudioPlaybackService callback error: {ex.Message}");
        }
    }

    protected override void WriteFrames(short[] pcm, int count)
    {
        if (!_open || _queue == IntPtr.Zero || count <= 0) return;
        try
        {
            // Never write past the buffer's capacity: an oversized frame is clipped rather than overrunning
            // native memory. With a 16 KB buffer and 1024-sample frames this does not trigger in practice.
            int maxSamples = BufferBytes / 2;
            if (count > maxSamples) count = maxSamples;

            if (!TryTakeFreeBuffer(out IntPtr buffer)) return;   // all in flight — drop, as the base class does

            var native = Marshal.PtrToStructure<CoreAudioInterop.AudioQueueBuffer>(buffer);
            if (native.mAudioData == IntPtr.Zero) { _freeBuffers.Enqueue(buffer); return; }

            Marshal.Copy(pcm, 0, native.mAudioData, count);
            // Tell AudioQueue how much of the buffer is valid. Only this field changes; writing the whole struct
            // back would also rewrite fields the C header declares const.
            Marshal.WriteInt32(buffer, ByteSizeOffset, count * 2);

            int status = CoreAudioInterop.AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero);
            if (status != 0)
            {
                _freeBuffers.Enqueue(buffer);   // not queued, so it is still ours
                Debug.WriteLine($"CoreAudioPlaybackService: enqueue failed ({CoreAudioInterop.DescribeStatus(status)}).");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CoreAudioPlaybackService write error: {ex.Message}");
        }
    }

    /// <summary>Take a free buffer, waiting briefly for one to come back. The short bounded wait is deliberate:
    /// this runs on the playback thread (never the capture thread), and monitoring must not build up latency, so
    /// giving up and dropping a frame is better than blocking.</summary>
    private bool TryTakeFreeBuffer(out IntPtr buffer)
    {
        if (_freeBuffers.TryDequeue(out buffer)) return true;
        if (_bufferReturned.WaitOne(50) && _freeBuffers.TryDequeue(out buffer)) return true;
        buffer = IntPtr.Zero;
        return false;
    }

    protected override void CloseDevice()
    {
        _open = false;                 // stops the callback recycling into a queue that is going away
        IntPtr aq = _queue;
        _queue = IntPtr.Zero;
        if (aq != IntPtr.Zero)
        {
            // immediate:true blocks until the queue has stopped and no callback is in flight, so Dispose cannot
            // race one. The queue owns its buffers and frees them with itself.
            try { CoreAudioInterop.AudioQueueStop(aq, true); } catch { /* best effort */ }
            try { CoreAudioInterop.AudioQueueDispose(aq, true); } catch { /* best effort */ }
        }
        while (_freeBuffers.TryDequeue(out _)) { }
        _callback = null;              // safe only after Dispose: native no longer holds the pointer
    }
}
