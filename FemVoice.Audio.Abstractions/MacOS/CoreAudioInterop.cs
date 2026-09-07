using System.Runtime.InteropServices;

namespace FemVoiceStudio.Audio.Abstractions.MacOS;

/// <summary>
/// P/Invoke surface for macOS audio capture AND playback via <b>AudioQueue</b> (AudioToolbox.framework). Deliberately
/// dependency-free — no NuGet, no Xamarin/ObjC bindings — matching the ALSA (libasound) and winmm bindings
/// used by the Linux and Windows backends, so <c>FemVoice.Audio.Abstractions</c> stays a plain net10.0
/// assembly that every head can reference.
///
/// AudioQueue is used rather than the lower-level AudioUnit/HAL because it delivers PCM through a simple
/// callback on its own internal thread when <c>inCallbackRunLoop</c> is NULL — no CFRunLoop has to be
/// created or pumped, which would otherwise mean owning a thread with an ObjC run loop just to record.
///
/// Symbols are resolved from the absolute framework path. That is the documented location on every
/// supported macOS and avoids relying on a dylib search path.
/// </summary>
internal static class CoreAudioInterop
{
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

    // 'lpcm' — linear PCM. Four-char codes are big-endian packed ASCII.
    internal const uint kAudioFormatLinearPCM = 0x6C70636D;

    // Signed integer samples, packed (no padding bits). kAudioFormatFlagIsBigEndian is deliberately NOT set:
    // both Intel and Apple Silicon Macs are little-endian, which matches the S16LE conversion in the caller.
    internal const uint kAudioFormatFlagIsSignedInteger = 1u << 2;
    internal const uint kAudioFormatFlagIsPacked = 1u << 3;
    internal const uint LinearPcmS16Flags = kAudioFormatFlagIsSignedInteger | kAudioFormatFlagIsPacked;

    /// <summary>Mirrors <c>AudioStreamBasicDescription</c>. Field order and types must match exactly.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioStreamBasicDescription
    {
        public double mSampleRate;
        public uint mFormatID;
        public uint mFormatFlags;
        public uint mBytesPerPacket;
        public uint mFramesPerPacket;
        public uint mBytesPerFrame;
        public uint mChannelsPerFrame;
        public uint mBitsPerChannel;
        public uint mReserved;
    }

    /// <summary>
    /// Mirrors <c>AudioQueueBuffer</c>. Only the first three fields are read by the capture callback
    /// (capacity, data pointer, and the byte count actually filled); the rest are present so the struct's
    /// size and field offsets match the native layout. The uint/pointer alternation is padded by the
    /// runtime the same way the C compiler pads it on 64-bit (both x86_64 and arm64).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioQueueBuffer
    {
        public uint mAudioDataBytesCapacity;
        public IntPtr mAudioData;
        public uint mAudioDataByteSize;
        public IntPtr mUserData;
        public uint mPacketDescriptionCapacity;
        public IntPtr mPacketDescriptions;
        public uint mPacketDescriptionCount;
    }

    /// <summary>
    /// Mirrors <c>AudioQueueInputCallback</c>. The timestamp and packet-description arguments are taken as
    /// raw pointers because this backend records constant-bitrate linear PCM, where they carry nothing the
    /// caller needs — marshalling them would only add cost on the audio thread.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void AudioQueueInputCallback(
        IntPtr inUserData,
        IntPtr inAQ,
        IntPtr inBuffer,
        IntPtr inStartTime,
        uint inNumberPacketDescriptions,
        IntPtr inPacketDescs);

    /// <summary>Create a recording audio queue. Pass <c>IntPtr.Zero</c> for the run loop so callbacks arrive
    /// on an AudioQueue-owned thread instead of requiring a CFRunLoop.</summary>
    [DllImport(AudioToolbox)]
    internal static extern int AudioQueueNewInput(
        ref AudioStreamBasicDescription inFormat,
        AudioQueueInputCallback inCallbackProc,
        IntPtr inUserData,
        IntPtr inCallbackRunLoop,
        IntPtr inCallbackRunLoopMode,
        uint inFlags,
        out IntPtr outAQ);

    /// <summary>
    /// Mirrors <c>AudioQueueOutputCallback</c>. AudioQueue hands a buffer back once it has finished playing it,
    /// which is how the playback backend recycles its buffer pool.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void AudioQueueOutputCallback(IntPtr inUserData, IntPtr inAQ, IntPtr inBuffer);

    /// <summary>Create a playback audio queue. Same run-loop rule as the input queue: <c>IntPtr.Zero</c> means
    /// callbacks arrive on an AudioQueue-owned thread rather than requiring a CFRunLoop.</summary>
    [DllImport(AudioToolbox)]
    internal static extern int AudioQueueNewOutput(
        ref AudioStreamBasicDescription inFormat,
        AudioQueueOutputCallback inCallbackProc,
        IntPtr inUserData,
        IntPtr inCallbackRunLoop,
        IntPtr inCallbackRunLoopMode,
        uint inFlags,
        out IntPtr outAQ);

    [DllImport(AudioToolbox)]
    internal static extern int AudioQueueAllocateBuffer(IntPtr inAQ, uint inBufferByteSize, out IntPtr outBuffer);

    [DllImport(AudioToolbox)]
    internal static extern int AudioQueueEnqueueBuffer(IntPtr inAQ, IntPtr inBuffer, uint inNumPacketDescs, IntPtr inPacketDescs);

    [DllImport(AudioToolbox)]
    internal static extern int AudioQueueStart(IntPtr inAQ, IntPtr inStartTime);

    [DllImport(AudioToolbox)]
    internal static extern int AudioQueueStop(IntPtr inAQ, [MarshalAs(UnmanagedType.U1)] bool inImmediate);

    [DllImport(AudioToolbox)]
    internal static extern int AudioQueueDispose(IntPtr inAQ, [MarshalAs(UnmanagedType.U1)] bool inImmediate);

    /// <summary>Render an OSStatus as the four-char code Apple documents it by when it is printable ASCII
    /// (e.g. 'nope' for a permissions failure), otherwise as the raw number. Purely for diagnostics.</summary>
    internal static string DescribeStatus(int status)
    {
        if (status == 0) return "noErr";
        Span<char> chars = stackalloc char[4];
        for (int i = 0; i < 4; i++)
        {
            int b = (status >> (8 * (3 - i))) & 0xFF;
            if (b < 32 || b > 126) return status.ToString();
            chars[i] = (char)b;
        }
        return $"'{new string(chars)}' ({status})";
    }
}
