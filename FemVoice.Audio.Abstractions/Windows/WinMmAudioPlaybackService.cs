using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FemVoiceStudio.Audio.Abstractions.Windows;

/// <summary>
/// Real Windows speaker playback via the multimedia <c>waveOut</c> API (<c>winmm.dll</c>), for the "hear your own
/// voice" monitor. Dependency-free P/Invoke (no NuGet, no COM), mirroring the winmm capture backend. It plays
/// interleaved S16 frames handed to it by the shared <see cref="BufferedAudioPlaybackService"/> base, using a small
/// ring of prepared buffers signalled by an event when the driver finishes one. Fail-safe: winmm absent (non-Windows)
/// or no output device → <see cref="IsAvailable"/> false and playback is a silent no-op; never throws.
/// </summary>
public sealed class WinMmAudioPlaybackService : BufferedAudioPlaybackService
{
    private const int BufferCount = 4;

    private IntPtr _hwo;
    private readonly IntPtr[] _headers = new IntPtr[BufferCount];
    private readonly IntPtr[] _datas = new IntPtr[BufferCount];
    private readonly int[] _dataBytes = new int[BufferCount];
    private AutoResetEvent? _bufDone;
    private int _next;
    private bool? _availableCache;

    public override bool IsAvailable => _availableCache ??= ProbeCanPlay();

    private static bool ProbeCanPlay()
    {
        try { return WinMmInterop.waveOutGetNumDevs() > 0; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (Exception ex) { Debug.WriteLine($"WinMmAudioPlaybackService probe failed: {ex.Message}"); return false; }
    }

    protected override bool OpenDevice(int sampleRate, int channels)
    {
        try
        {
            _bufDone = new AutoResetEvent(true);   // starts signalled so the first writes proceed
            ushort ch = (ushort)(channels <= 0 ? 1 : channels);
            ushort blockAlign = (ushort)(ch * 2);
            var fmt = new WinMmInterop.WAVEFORMATEX
            {
                wFormatTag = WinMmInterop.WAVE_FORMAT_PCM,
                nChannels = ch,
                nSamplesPerSec = (uint)sampleRate,
                nAvgBytesPerSec = (uint)(sampleRate * blockAlign),
                nBlockAlign = blockAlign,
                wBitsPerSample = 16,
                cbSize = 0,
            };
            uint res = WinMmInterop.waveOutOpen(out _hwo, WinMmInterop.WAVE_MAPPER_OUT, ref fmt,
                _bufDone!.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, WinMmInterop.CALLBACK_EVENT);
            if (res != WinMmInterop.MMSYSERR_NOERROR || _hwo == IntPtr.Zero) { _hwo = IntPtr.Zero; return false; }
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"WinMmAudioPlaybackService open failed: {ex.Message}"); _hwo = IntPtr.Zero; return false; }
    }

    protected override void WriteFrames(short[] pcm, int count)
    {
        if (_hwo == IntPtr.Zero || count <= 0) return;
        uint hdrSize = (uint)Marshal.SizeOf<WinMmInterop.WAVEHDR>();
        int bytes = count * 2;
        int slot = _next;
        _next = (_next + 1) % BufferCount;

        // Wait until this ring slot's previous write has finished (WHDR_DONE), then recycle it.
        if (_headers[slot] != IntPtr.Zero)
        {
            int guard = 0;
            while (guard++ < 50)
            {
                var h = Marshal.PtrToStructure<WinMmInterop.WAVEHDR>(_headers[slot]);
                if ((h.dwFlags & WinMmInterop.WHDR_DONE) != 0) break;
                _bufDone?.WaitOne(50);
            }
            WinMmInterop.waveOutUnprepareHeader(_hwo, _headers[slot], hdrSize);
        }

        // (Re)allocate the slot's native buffer if needed, copy the samples in.
        if (_datas[slot] == IntPtr.Zero || _dataBytes[slot] < bytes)
        {
            if (_datas[slot] != IntPtr.Zero) Marshal.FreeHGlobal(_datas[slot]);
            _datas[slot] = Marshal.AllocHGlobal(bytes);
            _dataBytes[slot] = bytes;
        }
        Marshal.Copy(pcm, 0, _datas[slot], count);

        if (_headers[slot] == IntPtr.Zero) _headers[slot] = Marshal.AllocHGlobal((int)hdrSize);
        var hdr = new WinMmInterop.WAVEHDR { lpData = _datas[slot], dwBufferLength = (uint)bytes };
        Marshal.StructureToPtr(hdr, _headers[slot], false);

        if (WinMmInterop.waveOutPrepareHeader(_hwo, _headers[slot], hdrSize) == WinMmInterop.MMSYSERR_NOERROR)
            WinMmInterop.waveOutWrite(_hwo, _headers[slot], hdrSize);
    }

    protected override void CloseDevice()
    {
        if (_hwo != IntPtr.Zero)
        {
            try { WinMmInterop.waveOutReset(_hwo); } catch { /* best effort */ }
            uint hdrSize = (uint)Marshal.SizeOf<WinMmInterop.WAVEHDR>();
            for (int i = 0; i < BufferCount; i++)
            {
                if (_headers[i] != IntPtr.Zero)
                {
                    try { WinMmInterop.waveOutUnprepareHeader(_hwo, _headers[i], hdrSize); } catch { }
                    Marshal.FreeHGlobal(_headers[i]); _headers[i] = IntPtr.Zero;
                }
                if (_datas[i] != IntPtr.Zero) { Marshal.FreeHGlobal(_datas[i]); _datas[i] = IntPtr.Zero; _dataBytes[i] = 0; }
            }
            try { WinMmInterop.waveOutClose(_hwo); } catch { /* best effort */ }
            _hwo = IntPtr.Zero;
        }
        _bufDone?.Dispose();
        _bufDone = null;
    }
}
