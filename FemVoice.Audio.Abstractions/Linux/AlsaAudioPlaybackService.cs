using System.Diagnostics;

namespace FemVoiceStudio.Audio.Abstractions.Linux;

/// <summary>
/// Real Linux speaker playback via ALSA (<c>libasound.so.2</c>), for the "hear your own voice" monitor. It opens the
/// default output PCM, writes interleaved S16_LE frames (converted from the mono float capture frames by the shared
/// <see cref="BufferedAudioPlaybackService"/> base), and recovers from underruns via <c>snd_pcm_recover</c>. Same
/// fail-safe contract as the ALSA capture backend: if <c>libasound</c> is missing or no output device can be opened,
/// <see cref="IsAvailable"/> is false and playback is a silent no-op — it never throws to the app.
/// </summary>
public sealed class AlsaAudioPlaybackService : BufferedAudioPlaybackService
{
    private const string DefaultPcm = "default";
    private IntPtr _pcm;
    private int _channels = 1;
    private bool? _availableCache;

    public override bool IsAvailable => _availableCache ??= ProbeCanOpenPlayback();

    private static bool ProbeCanOpenPlayback()
    {
        try
        {
            int err = AlsaInterop.snd_pcm_open(out IntPtr pcm, DefaultPcm, AlsaInterop.SND_PCM_STREAM_PLAYBACK, AlsaInterop.OPEN_BLOCKING);
            if (err < 0 || pcm == IntPtr.Zero) return false;
            AlsaInterop.snd_pcm_close(pcm);
            return true;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (Exception ex) { Debug.WriteLine($"AlsaAudioPlaybackService probe failed: {ex.Message}"); return false; }
    }

    protected override bool OpenDevice(int sampleRate, int channels)
    {
        try
        {
            int err = AlsaInterop.snd_pcm_open(out _pcm, DefaultPcm, AlsaInterop.SND_PCM_STREAM_PLAYBACK, AlsaInterop.OPEN_BLOCKING);
            if (err < 0 || _pcm == IntPtr.Zero) { _pcm = IntPtr.Zero; return false; }
            err = AlsaInterop.snd_pcm_set_params(_pcm, AlsaInterop.SND_PCM_FORMAT_S16_LE, AlsaInterop.SND_PCM_ACCESS_RW_INTERLEAVED,
                (uint)channels, (uint)sampleRate, softResample: 1, latencyMicros: 80_000);
            if (err < 0) { AlsaInterop.snd_pcm_close(_pcm); _pcm = IntPtr.Zero; return false; }
            AlsaInterop.snd_pcm_prepare(_pcm);
            _channels = channels <= 0 ? 1 : channels;
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"AlsaAudioPlaybackService open failed: {ex.Message}"); _pcm = IntPtr.Zero; return false; }
    }

    protected override void WriteFrames(short[] pcm, int count)
    {
        if (_pcm == IntPtr.Zero || count <= 0) return;
        int frames = count / _channels;   // snd_pcm_writei takes FRAMES; `count` is interleaved samples
        var raw = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            raw[i * 2] = (byte)(pcm[i] & 0xFF);
            raw[i * 2 + 1] = (byte)((pcm[i] >> 8) & 0xFF);
        }
        long n = AlsaInterop.snd_pcm_writei(_pcm, raw, (ulong)frames);
        if (n < 0)
        {
            int rec = AlsaInterop.snd_pcm_recover(_pcm, (int)n, silent: 1);
            if (rec == 0) AlsaInterop.snd_pcm_writei(_pcm, raw, (ulong)frames);   // one retry after recovery
        }
    }

    protected override void CloseDevice()
    {
        if (_pcm == IntPtr.Zero) return;
        try { AlsaInterop.snd_pcm_drop(_pcm); } catch { /* best effort */ }
        try { AlsaInterop.snd_pcm_close(_pcm); } catch { /* best effort */ }
        _pcm = IntPtr.Zero;
    }
}
