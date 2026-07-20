using System.Diagnostics;
using Android.Media;
using FemVoiceStudio.Audio.Abstractions;

namespace FemVoice.Android;

/// <summary>
/// Real Android speaker playback via <see cref="AudioTrack"/> (streaming PCM), for the "hear your own voice" monitor.
/// It plays the interleaved S16 frames handed to it by the shared <see cref="BufferedAudioPlaybackService"/> base
/// (converted from the mono float capture frames). Wired from <c>MainActivity</c> via
/// <see cref="AudioPlaybackBackendFactory.PlatformPlaybackFactory"/>. Fail-safe: init failure → silent no-op, never
/// throws. On a phone with the mic open, use headphones to avoid feedback/howling.
/// </summary>
public sealed class AndroidAudioPlaybackService : BufferedAudioPlaybackService
{
    private AudioTrack? _track;

    public override bool IsAvailable
    {
        get { try { return AudioTrack.GetMinBufferSize(44100, ChannelOut.Mono, Encoding.Pcm16bit) > 0; } catch { return true; } }
    }

    protected override bool OpenDevice(int sampleRate, int channels)
    {
        try
        {
            int minBytes = AudioTrack.GetMinBufferSize(sampleRate, ChannelOut.Mono, Encoding.Pcm16bit);
            if (minBytes <= 0) minBytes = sampleRate;   // fallback
            int bufBytes = System.Math.Max(minBytes, sampleRate / 4);   // ~250 ms headroom

            // Modern AudioTrack.Builder (avoids the deprecated stream-type ctor + its ChannelConfiguration enum).
            var attrs = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build();
            var format = new AudioFormat.Builder()!
                .SetEncoding(Encoding.Pcm16bit)!
                .SetSampleRate(sampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .Build();
            var track = new AudioTrack.Builder()
                .SetAudioAttributes(attrs!)
                .SetAudioFormat(format!)
                .SetBufferSizeInBytes(bufBytes)
                .SetTransferMode(AudioTrackMode.Stream)
                .Build();
            if (track.State != AudioTrackState.Initialized)
            {
                track.Release();
                return false;
            }
            track.Play();
            _track = track;
            return true;
        }
        catch (System.Exception ex) { Debug.WriteLine($"AndroidAudioPlaybackService open failed: {ex.Message}"); return false; }
    }

    protected override void WriteFrames(short[] pcm, int count)
    {
        var t = _track;
        if (t is null || count <= 0) return;
        try { t.Write(pcm, 0, count); } catch { /* underrun/stopped — fail-safe */ }
    }

    protected override void CloseDevice()
    {
        var t = _track;
        _track = null;
        if (t is not null)
        {
            try { t.Stop(); } catch { /* best effort */ }
            try { t.Release(); } catch { /* best effort */ }
        }
    }
}
