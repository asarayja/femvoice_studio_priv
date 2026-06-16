# Audio.Windows Adapter Notes (Agent 4)

Date: 2026-06-16. Project: `FemVoice.Audio.Windows` (`net10.0-windows`, `EnableWindowsTargeting=true`). **Builds clean on Linux (0/0); capture runs only on Windows.**

## What was wrapped
`NAudioCaptureService : IAudioCaptureService` is a **thin adapter** that delegates to the existing `AudioCaptureService` (moved verbatim into this project). It owns an `AudioCaptureService` instance and translates its events into the platform-neutral abstraction:
- `AudioCaptureService.AudioDataAvailable (EventHandler<float[]>)` → `IAudioCaptureService.FrameAvailable (AudioFrameAvailableEventArgs{ Samples, SampleRate, Channels })`.
- `AudioCaptureService.DeviceLost (EventHandler<string>)` → `IAudioCaptureService.DeviceLost (AudioDeviceLostEventArgs{ Reason })`.
- `GetInputDevices()` → maps `AudioCaptureService.GetAvailableDevices()` (string[]) to `AudioInputDevice(Id=index, Name, IsDefault=index 0)`.
- `StartAsync(options)` → `new AudioCaptureService(rate, channels, bits)`, optional `BufferSize = options.BufferSamples`, then `Initialize()` + `StartRecording()`.
- `StopAsync()` → `StopRecording()` + `Dispose()`.

## What behaviour was preserved (delegated, unchanged)
Everything in `AudioCaptureService` runs as before — the adapter adds no DSP and changes no thresholds:
- WASAPI/WaveIn selection, sample rate / channels / bit depth, buffer size / target latency.
- Noise gate, high-pass filter, input-processing toggle.
- Watchdog (stall detection), device-loss safety event.
- Device enumeration (NAudio), default-device selection.
- Calibration profile load/apply (`ApplyStoredCalibration`).
- Self-monitoring ("hear own voice") capability (via `AudioCaptureService` API; not changed).

## What was NOT changed
- No edit to `AudioCaptureService` logic (only relocated, namespace `FemVoiceStudio.Audio` preserved).
- No DSP change (DSP lives in `FemVoice.Core`).
- No Linux microphone capture implemented (out of scope). Linux/Avalonia uses `NoopAudioCaptureService`/`SyntheticAudioCaptureService` from `FemVoice.Audio.Abstractions`.
- Existing WPF capture call sites (`new AudioCaptureService(...)` in MainViewModel/ResonanceWindow/AnalyzerWindow) are **untouched**; migrating them to inject `IAudioCaptureService` is deferred to the UI-parity phases. The DI registration `IAudioCaptureService → NAudioCaptureService` was added (additive).

## DI wiring
- WPF (`App.xaml.cs`): `services.AddSingleton<IAudioCaptureService, NAudioCaptureService>();` (added; existing direct construction unchanged).
- Avalonia (`Program.cs`): `services.AddSingleton<IAudioCaptureService, NoopAudioCaptureService>();` (unchanged — no NAudio capture on Linux).

## Build verification
- `dotnet build FemVoice.Audio.Windows` on Linux → **Build succeeded, 0 warnings, 0 errors** (NAudio is netstandard2.0; `EnableWindowsTargeting=true` lets net10.0-windows compile on Linux). Runtime capture is Windows-only.

## Manual Windows audio test needed (NOT yet done — requires Windows + a mic)
1. On Windows, build `FemVoiceStudio.slnx`; launch the WPF app; confirm mic capture, pitch/resonance graphs, calibration, and device-lost handling behave exactly as the frozen baseline.
2. (Optional) Add a tiny Windows console harness that resolves `IAudioCaptureService → NAudioCaptureService`, calls `StartAsync`, and logs `FrameAvailable` counts + `GetInputDevices()` to confirm the adapter forwards frames/devices/device-loss identically.

## Known risks
- Adapter is build-verified but **not runtime-verified** (no mic on this Linux host) — see manual test above.
- `AudioCaptureOptions.DeviceId` is currently ignored (the wrapped `AudioCaptureService` auto-selects the default device, matching current behaviour); explicit device selection is a future enhancement, not a regression.
- `AudioCaptureService.cs` carried `[assembly: InternalsVisibleTo("FemVoiceStudio.Tests")]`; that grant now applies to the `FemVoice.Audio.Windows` assembly, so the Windows-only `AudioCaptureServiceTests`/`AudioSafetyTests` retain access to its internals.

## Manual Windows Mic Smoke Result

> **Status: ⛔ NOT RUN — awaiting a human tester on a real Windows machine with a microphone.**
> This step cannot be performed by the AI agent (the dev host is Linux with no WPF runtime and no
> microphone) and **must not be faked**. Static prerequisites are confirmed below; the runtime/mic
> rows must be filled by the tester. Fill this in, then update the recommendation in
> `WINDOWS_AND_AUDIO_GATE_REPORT.md` per the rule (PASS / PASS_WITH_WARNINGS → MERGE-READY; FAIL → blocked).

```
Date:                          <fill in>
Tester:                        <fill in>
Windows version:               <fill in>
Machine:                       <fill in>
Audio device:                  <fill in>
Branch:                        linux-portable-core
Commit:                        e2903ea (or HEAD at test time)
Build result:                  GREEN (confirmed by Windows CI run 27618290291; re-confirm locally if desired)
App launch result:             <fill in — checklist 1-3>
Capture path tested:           IAudioCaptureService -> NAudioCaptureService  (DI registration CONFIRMED, App.xaml.cs:146)
Start recording:               <fill in — checklist 5-7>
Pitch/signal update:           <fill in — checklist 7>
Stop recording:                <fill in — checklist 8>
Repeated start/stop:           <fill in — checklist 9-10>
No-device / blocked-device behavior: <fill in — checklist 11-12>
Calibration behavior:          <fill in — checklist 13>
Errors:                        <fill in>
Warnings:                      <fill in>
Behavior changed:              <yes/no — checklist 14; expected: no>
Result:                        <PASS / PASS_WITH_WARNINGS / FAIL>
Notes:                         <fill in>
```

### Statically pre-confirmed for the tester (not a substitute for the runtime smoke)
- **DI wiring (checklist #4):** `FemVoiceStudio/App.xaml.cs:146` → `services.AddSingleton<IAudioCaptureService, NAudioCaptureService>();` (CONFIRMED). Note: existing windows still construct `AudioCaptureService` directly (call-site migration to inject `IAudioCaptureService` is deferred to the UI-parity phases), so the **live front-page capture path on Windows is unchanged** from the frozen baseline; the adapter is the new abstraction seam.
- **Build (checklist prerequisite):** WPF + `FemVoice.Audio.Windows` build GREEN on Windows CI (run 27618290291) and on Linux compile-check (`EnableWindowsTargeting`).
- **Adapter (#4):** `FemVoice.Audio.Windows/NAudioCaptureService.cs` delegates to the unchanged `AudioCaptureService` — capture behaviour (#5-13) is expected identical to the baseline because no capture logic changed.

### How to run (PowerShell, on Windows)
```powershell
git fetch origin; git checkout linux-portable-core; git pull
git rev-parse --short HEAD
dotnet restore FemVoiceStudio.slnx
dotnet build   FemVoiceStudio.slnx -c Debug
# launch the WPF app (Visual Studio, or the built FemVoiceStudio.exe) and walk the 14-item checklist above
```
