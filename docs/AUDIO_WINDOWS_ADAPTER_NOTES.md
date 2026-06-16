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
