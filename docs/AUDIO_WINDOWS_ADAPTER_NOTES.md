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

> **Status: ✅ PERFORMED by the user on Windows — Result: PASS_WITH_WARNINGS.**

```
Date:                          2026-06-16
Tester:                        Asarayja
Windows version:               user-tested Windows machine
Machine:                       user Windows PC
Audio device:                  default microphone (user machine)
Branch:                        linux-portable-core
Commit:                        e2903ea (or later HEAD at test time)
Build result:                  PASS (also GREEN on Windows CI run 27618290291)
App launch result:             PASS
Main dashboard:                PASS
Capture path tested:           WPF app using the Windows audio path (IAudioCaptureService -> NAudioCaptureService registered; App.xaml.cs:146)
Start recording:               PASS
Pitch/signal update:           PASS
Stop recording:                PASS
Repeated start/stop:           PASS
No double-open crash:          PASS (observed during normal smoke)
No-device / blocked-device behavior: NOT FULLY TESTED
Calibration behavior:          NOT FULLY TESTED
Errors:                        none reported
Warnings:                      NU1903 transitive Tmds.DBus.Protocol advisory only; unrelated to WPF/audio adapter
Behavior changed:              no
Result:                        PASS_WITH_WARNINGS
Notes:                         Manual tester reports that on Windows the app builds and appears to work as
                               expected (launch, dashboard, recording start/stop, mic signal). Optional
                               no-device / device-lost edge cases and full calibration were not exhaustively
                               exercised, so recorded as PASS_WITH_WARNINGS rather than full PASS.
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
