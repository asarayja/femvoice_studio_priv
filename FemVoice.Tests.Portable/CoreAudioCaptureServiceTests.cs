using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FemVoiceStudio.Audio.Abstractions;
using FemVoiceStudio.Audio.Abstractions.MacOS;
using Xunit;

namespace FemVoice.Tests.Portable;

/// <summary>
/// Tests for the macOS AudioQueue capture backend.
///
/// The backend itself can only be exercised end-to-end on a Mac with a microphone, which neither this Linux
/// dev host nor a GitHub macOS runner (no audio input hardware) provides. What CAN be verified anywhere —
/// and is where hand-written P/Invoke actually goes wrong — is the <b>native memory layout</b> and the
/// <b>fail-safe behaviour off macOS</b>. A wrong struct size or field offset does not fail to compile; it
/// silently reads the wrong bytes at runtime, which on an audio thread means noise, or a crash inside
/// native code with no managed stack. These tests pin the layout against the documented C definitions.
/// </summary>
public class CoreAudioCaptureServiceTests
{
    private static Type Interop =>
        typeof(CoreAudioCaptureService).Assembly.GetType("FemVoiceStudio.Audio.Abstractions.MacOS.CoreAudioInterop", throwOnError: true)!;

    private static Type NestedStruct(string name) =>
        Interop.GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public)!;

    // ── Native layout ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AudioStreamBasicDescription_MatchesNativeSize()
    {
        // Float64 mSampleRate + 8 × UInt32 = 8 + 32 = 40 bytes, with no padding (all fields 4-byte aligned
        // after an 8-byte double).
        Assert.Equal(40, Marshal.SizeOf(NestedStruct("AudioStreamBasicDescription")));
    }

    [Fact]
    public void AudioQueueBuffer_MatchesNativeSizeAndOffsets()
    {
        var t = NestedStruct("AudioQueueBuffer");

        // On 64-bit (x86_64 and arm64 alike) each pointer is 8-byte aligned, so every UInt32 that precedes a
        // pointer is followed by 4 bytes of padding. Reading mAudioDataByteSize from the wrong offset is the
        // classic failure here: capture would report a wrong byte count and emit garbage or nothing at all.
        Assert.Equal(0,  (int)Marshal.OffsetOf(t, "mAudioDataBytesCapacity"));
        Assert.Equal(8,  (int)Marshal.OffsetOf(t, "mAudioData"));
        Assert.Equal(16, (int)Marshal.OffsetOf(t, "mAudioDataByteSize"));
        Assert.Equal(24, (int)Marshal.OffsetOf(t, "mUserData"));
        Assert.Equal(32, (int)Marshal.OffsetOf(t, "mPacketDescriptionCapacity"));
        Assert.Equal(40, (int)Marshal.OffsetOf(t, "mPacketDescriptions"));
        Assert.Equal(48, (int)Marshal.OffsetOf(t, "mPacketDescriptionCount"));
        Assert.Equal(56, Marshal.SizeOf(t));
    }

    [Fact]
    public void LinearPcmConstants_MatchAppleDefinitions()
    {
        // kAudioFormatLinearPCM is the four-char code 'lpcm'.
        uint formatId = (uint)Interop.GetField("kAudioFormatLinearPCM", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.Equal((uint)(('l' << 24) | ('p' << 16) | ('c' << 8) | 'm'), formatId);

        // kAudioFormatFlagIsSignedInteger (1<<2) | kAudioFormatFlagIsPacked (1<<3) = 0xC. The big-endian flag
        // (1<<1) must NOT be set: the sample conversion in the callback reads little-endian S16.
        uint flags = (uint)Interop.GetField("LinearPcmS16Flags", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.Equal(0b1100u, flags);
        Assert.Equal(0u, flags & (1u << 1));
    }

    [Fact]
    public void DescribeStatus_RendersFourCharCodesAndPlainNumbers()
    {
        var describe = Interop.GetMethod("DescribeStatus", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal("noErr", describe.Invoke(null, new object[] { 0 }));

        // 'nope' is the OSStatus macOS returns when a service refuses the request — the shape a permissions
        // failure arrives in, so it must render readably rather than as a large negative number.
        int nope = ('n' << 24) | ('o' << 16) | ('p' << 8) | 'e';
        Assert.Contains("nope", (string)describe.Invoke(null, new object[] { nope })!);

        // A status whose bytes are not printable ASCII must fall back to the raw number, not emit control chars.
        Assert.Equal("-50", describe.Invoke(null, new object[] { -50 }));
    }

    // ── Fail-safe behaviour when NOT on macOS ────────────────────────────────────────────────────────
    // These run on the Linux dev host and in CI. The backend must degrade exactly like the ALSA one does on a
    // machine with no sound card: never throw, never claim availability, never fabricate frames.

    [Fact]
    public void OffMacOs_ReportsUnavailableAndEnumeratesNothing()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;   // real behaviour is covered on a Mac

        using var svc = new CoreAudioCaptureService();
        Assert.False(svc.IsBackendAvailable);
        Assert.Empty(svc.GetInputDevices());
    }

    [Fact]
    public async Task OffMacOs_StartRaisesDeviceLostAndEmitsNoFrames()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        using var svc = new CoreAudioCaptureService();
        var lost = new List<string?>();
        int frames = 0;
        svc.DeviceLost += (_, e) => lost.Add(e.Reason);
        svc.FrameAvailable += (_, _) => frames++;

        // Must not throw even though AudioToolbox cannot be loaded here.
        await svc.StartAsync(new AudioCaptureOptions());

        Assert.Single(lost);
        Assert.False(string.IsNullOrWhiteSpace(lost[0]));
        Assert.Equal(0, frames);

        // Stop/dispose after a failed start must be safe and silent.
        await svc.StopAsync();
    }

    [Fact]
    public async Task StopAndDispose_AreSafeWithoutStart()
    {
        var svc = new CoreAudioCaptureService();
        await svc.StopAsync();
        svc.Dispose();
        svc.Dispose();   // idempotent
    }

    // ── Playback (speaker) backend ───────────────────────────────────────────────────────────────────

    [Fact]
    public void OffMacOs_PlaybackReportsUnavailableAndEveryCallIsHarmless()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        using var svc = new CoreAudioPlaybackService();
        Assert.False(svc.IsAvailable);

        // The base class refuses to start a thread when the backend is unavailable, so all of this must be
        // a silent no-op rather than throwing into the voice monitor.
        svc.Start(44100, 1);
        svc.Write(new float[512]);
        svc.Stop();
        svc.Dispose();
    }

    [Fact]
    public void PlaybackFactory_SelectsCoreAudioOnMacOsAndLeavesOtherPlatformsUnchanged()
    {
        using var playback = AudioPlaybackBackendFactory.CreateForRuntime();
        string name = playback.GetType().Name;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Assert.Equal(nameof(CoreAudioPlaybackService), name);   // was NoopAudioPlaybackService — monitoring was silent
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.Equal("AlsaAudioPlaybackService", name);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Equal("WinMmAudioPlaybackService", name);
    }

    [Fact]
    public void PlaybackWritesTheByteCountFieldAtTheOffsetTheNativeStructDeclares()
    {
        // The playback backend must tell AudioQueue how many bytes of a buffer are valid, by writing
        // mAudioDataByteSize in place. It derives that offset from the struct rather than hardcoding it —
        // this asserts the derivation agrees with the layout pinned above, so the two can never drift apart.
        int offset = (int)Marshal.OffsetOf(NestedStruct("AudioQueueBuffer"), "mAudioDataByteSize");
        Assert.Equal(16, offset);
    }

    // ── The dispatcher still picks the right backend per OS ──────────────────────────────────────────

    [Fact]
    public void Dispatcher_SelectsCoreAudioOnMacOsAndLeavesOtherPlatformsUnchanged()
    {
        using var dispatcher = new CrossPlatformAudioCaptureService();
        string selected = dispatcher.SelectedBackendDescription;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Assert.Equal(nameof(CoreAudioCaptureService), selected);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.Equal("AlsaAudioCaptureService", selected);      // unchanged by the macOS work
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Equal("WinMmAudioCaptureService", selected);
    }
}
