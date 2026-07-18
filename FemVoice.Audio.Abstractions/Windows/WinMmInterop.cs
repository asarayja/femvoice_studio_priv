using System.Runtime.InteropServices;

namespace FemVoiceStudio.Audio.Abstractions.Windows;

/// <summary>
/// Minimal P/Invoke surface for the Windows multimedia <c>waveIn</c> capture API (<c>winmm.dll</c>). Chosen over
/// WASAPI COM interop because it is dependency-free (no NuGet, no COM marshalling), works on every supported Windows
/// version, enumerates input devices by name, and streams PCM directly — exactly what <see cref="WinMmAudioCaptureService"/>
/// needs. Only the small subset used by that backend is declared here; nothing else references it. On non-Windows
/// hosts the DLL is simply absent, so every call throws <see cref="DllNotFoundException"/>, which the backend treats
/// as "unavailable" (it is never invoked off Windows anyway — the OS dispatcher guards it).
/// </summary>
internal static class WinMmInterop
{
    internal const uint WAVE_MAPPER = 0xFFFFFFFF;      // route to the system-default input device
    internal const uint CALLBACK_EVENT = 0x00050000;   // dwCallback is an event handle signalled per finished buffer
    internal const ushort WAVE_FORMAT_PCM = 1;
    internal const uint WHDR_DONE = 0x00000001;        // the driver has filled this buffer
    internal const uint WHDR_PREPARED = 0x00000002;
    internal const uint MMSYSERR_NOERROR = 0;

    /// <summary>PCM wave format descriptor. Packed to the Win32 layout; <c>cbSize</c> is 0 for plain PCM.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    /// <summary>A capture buffer header handed to/back from the driver.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    /// <summary>Input-device capabilities (Unicode). <c>szPname</c> is the human-readable device name.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WAVEINCAPSW
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwFormats;
        public ushort wChannels;
        public ushort wReserved1;
    }

    [DllImport("winmm.dll")]
    internal static extern uint waveInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    internal static extern uint waveInGetDevCapsW(IntPtr uDeviceID, ref WAVEINCAPSW pwic, uint cbwic);

    [DllImport("winmm.dll")]
    internal static extern uint waveInOpen(out IntPtr phwi, uint uDeviceID, ref WAVEFORMATEX pwfx,
        IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

    [DllImport("winmm.dll")]
    internal static extern uint waveInClose(IntPtr hwi);

    [DllImport("winmm.dll")]
    internal static extern uint waveInPrepareHeader(IntPtr hwi, IntPtr pwh, uint cbwh);

    [DllImport("winmm.dll")]
    internal static extern uint waveInUnprepareHeader(IntPtr hwi, IntPtr pwh, uint cbwh);

    [DllImport("winmm.dll")]
    internal static extern uint waveInAddBuffer(IntPtr hwi, IntPtr pwh, uint cbwh);

    [DllImport("winmm.dll")]
    internal static extern uint waveInStart(IntPtr hwi);

    [DllImport("winmm.dll")]
    internal static extern uint waveInStop(IntPtr hwi);

    [DllImport("winmm.dll")]
    internal static extern uint waveInReset(IntPtr hwi);
}
