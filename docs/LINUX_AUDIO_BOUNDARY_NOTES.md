# Linux Audio Boundary Notes (Phase L3)

Date: 2026-06-16.

## What was done
- Created `FemVoice.Audio.Abstractions` (net10.0) with the capture boundary:
  - `IAudioCaptureService` (FrameAvailable / DeviceLost events, `GetInputDevices()`, `StartAsync`/`StopAsync`).
  - DTOs: `AudioFrameAvailableEventArgs` (float samples + sampleRate + channels), `AudioDeviceLostEventArgs`, `AudioInputDevice`, `AudioCaptureOptions` (defaults match the WPF baseline: 44.1 kHz mono, 1024-sample buffer, 16-bit).
- Two portable implementations for Linux/tests/Avalonia bootstrap:
  - `NoopAudioCaptureService` — no devices, no frames (headless default).
  - `SyntheticAudioCaptureService` — emits a steady sine (default 200 Hz) on a background loop, so the DSP/scoring pipeline can run end-to-end without hardware. Changes no DSP behaviour — it only produces samples a real mic could also produce.
- The Avalonia head registers `IAudioCaptureService → NoopAudioCaptureService` (swap to `SyntheticAudioCaptureService` to drive the pipeline). Verified via the headless smoke (`devices=0`).

## What did NOT change (frozen)
- The DSP analyzers (pitch/formant/resonance/strain/calibration/metrics) moved into `FemVoice.Core` **unchanged**. Sample rates, buffer sizes, noise gate, high-pass, watchdog, device-lost semantics, calibration-profile behaviour, pitch stabilization, and the resonance proxy are all preserved.
- `ResonanceProxyEngine` uses only NAudio's **FFT math** (`NAudio.Dsp.Complex` / `FastFourierTransform`); its `using NAudio.Wave;` is unused. NAudio 2.2.1 (netstandard2.0) loads on net10.0, so this compiles and runs on Linux. No capture is performed in Core.

## What stays Windows-only (NAudio capture)
`AudioCaptureService.cs`, `AudioAnalysisEngine.cs` (capture half), `AudioAnalyzerService.cs`, `RealtimeAnalysisEngine.cs`, `AsyncAudioPipeline.cs` remain in the WPF project (`net10.0-windows`). They use NAudio WASAPI/WaveIn/MMDevice — Windows-only.

## Open questions / NEEDS REVIEW
1. **Windows NAudio adapter not yet written.** The prompt's target `FemVoice.Audio.Windows` project (NAudio implementing `IAudioCaptureService`) was **not created** — capture still lives in the WPF app, not behind the interface in production. Reason: it is Windows-only and cannot be built/verified on this Linux host. Follow-up (Windows): create `FemVoice.Audio.Windows` (net10.0-windows), add a `NAudioAudioCaptureService : IAudioCaptureService` adapter wrapping the existing `AudioCaptureService` (it already raises float frames + a device-lost event), and inject it in the WPF/Avalonia-on-Windows composition root.
2. **Cross-platform real capture (Linux/macOS) is out of scope** (prompt: "Do not attempt Windows audio capture on Linux"). When tackled: evaluate a portable backend (PortAudio / miniaudio / OpenAL bindings) implementing `IAudioCaptureService`. The DSP downstream is already portable, so only the capture source needs a backend.
3. **Device id semantics**: `AudioInputDevice.Id` is backend-specific (NAudio device number vs WASAPI id vs PortAudio index). The adapter must map `AudioCaptureOptions.DeviceId` accordingly.
4. **Frame format contract**: the abstraction emits mono float [-1,1]. Confirm the NAudio adapter converts (it already produces float frames in `AudioCaptureService`), and that multi-channel handling matches the current mono assumption.
