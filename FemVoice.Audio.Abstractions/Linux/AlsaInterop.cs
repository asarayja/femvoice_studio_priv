using System.Runtime.InteropServices;

namespace FemVoiceStudio.Audio.Abstractions.Linux;

/// <summary>
/// Minimal, dependency-free P/Invoke surface for ALSA (<c>libasound.so.2</c>) capture. This is pure managed
/// interop — NO NuGet package, NO native binary shipped — so the assembly still compiles and packages on every
/// platform; the entry points are only ever resolved when <see cref="AlsaAudioCaptureService"/> is actually used
/// on Linux. Every caller guards these calls so a missing library or device degrades gracefully (never throws to
/// the app). Only the small subset needed for blocking interleaved S16_LE mono capture is declared.
/// </summary>
internal static class AlsaInterop
{
    private const string Lib = "libasound.so.2";

    // snd_pcm_stream_t
    internal const int SND_PCM_STREAM_PLAYBACK = 0;
    internal const int SND_PCM_STREAM_CAPTURE = 1;

    // snd_pcm_format_t
    internal const int SND_PCM_FORMAT_S16_LE = 2;

    // snd_pcm_access_t
    internal const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;

    // open mode (0 = blocking)
    internal const int OPEN_BLOCKING = 0;

    /// <summary>snd_pcm_open(&amp;pcm, name, stream, mode) → 0 on success, negative errno otherwise.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);

    /// <summary>Convenience configuration: format/access/channels/rate/soft_resample/latency(µs). 0 on success.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int snd_pcm_set_params(IntPtr pcm, int format, int access, uint channels, uint rate, int softResample, uint latencyMicros);

    /// <summary>Read interleaved frames. Returns frames read (&gt;0) or a negative errno (e.g. -EPIPE on overrun).</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern long snd_pcm_readi(IntPtr pcm, byte[] buffer, ulong frames);

    /// <summary>Write interleaved frames to the output. Returns frames written (&gt;0) or a negative errno
    /// (e.g. -EPIPE on underrun). Used by the playback backend ("hear your own voice").</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern long snd_pcm_writei(IntPtr pcm, byte[] buffer, ulong frames);

    /// <summary>Recover the stream after an error (xrun/suspend). 0 on success, else negative errno.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);

    /// <summary>Prepare the PCM for use (after open or recovery). 0 on success.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int snd_pcm_prepare(IntPtr pcm);

    /// <summary>Stop the PCM immediately, dropping pending frames. 0 on success.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int snd_pcm_drop(IntPtr pcm);

    /// <summary>Close the PCM and free its resources. 0 on success.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int snd_pcm_close(IntPtr pcm);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr snd_strerror(int errnum);

    /// <summary>Human-readable text for an ALSA error code (never throws; empty string on failure).</summary>
    internal static string StrError(int errnum)
    {
        try
        {
            IntPtr p = snd_strerror(errnum);
            return p == IntPtr.Zero ? $"errno {errnum}" : (Marshal.PtrToStringAnsi(p) ?? $"errno {errnum}");
        }
        catch { return $"errno {errnum}"; }
    }
}
