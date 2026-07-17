# Avalonia — Real cross-platform microphone capture (Linux/ALSA) — Slice Report

Date: 2026-07-17 · Branch: `avalonia-real-audio-linux-alsa-slice` (off `main` @ `af3af06`) · Host: Linux (.NET 10 SDK `10.0.110`, `libasound.so.2` present, real USB capture device + PulseAudio server).

## Goal

Turn the cross-platform REAL-capture slot (the `CrossPlatformAudioCaptureService` skeleton from Stage 3B) into an
actual working microphone capture backend, behind the existing `IAudioCaptureService` abstraction, starting with the
platform this workstation can verify end-to-end: **Linux via ALSA**. macOS and Windows keep reporting "unavailable"
from this dispatcher pending their own native slices (Windows real capture already exists as the NAudio adapter in
its own Windows-only project).

This is the "real backend exists + is truthful" rung of the ladder. It deliberately does **not** route real audio
into the display-only clinical runtime — that activation is the next, separately-approved slice.

## What changed (files)

- **`FemVoice.Audio.Abstractions/Linux/AlsaInterop.cs` (new)** — minimal, dependency-free P/Invoke surface for
  `libasound.so.2` (`snd_pcm_open`/`set_params`/`readi`/`recover`/`prepare`/`drop`/`close`/`strerror`) + the few
  enum constants for blocking interleaved S16_LE capture. Pure managed interop: **no NuGet package, no native binary
  shipped**; entry points only resolve when actually used on Linux.
- **`FemVoice.Audio.Abstractions/Linux/AlsaAudioCaptureService.cs` (new)** — real `IRealAudioCaptureBackend`:
  probes availability by opening+closing the default capture PCM (cached); enumerates one "default" input device
  when available; a dedicated capture thread reads interleaved S16_LE mono at the requested rate (WPF baseline
  44.1 kHz), down-mixes to mono float in `[-1, 1]`, and raises `FrameAvailable`. Overruns are recovered via
  `snd_pcm_recover`; an unrecoverable error raises `DeviceLost` and stops. **Fail-safe**: missing library / no
  device / privacy block → `IsBackendAvailable=false`, empty enumeration, `StartAsync` raises `DeviceLost` and
  starts no loop. Never throws to the app, never fabricates frames. All libasound access stays on the capture
  thread (Stop signals cancellation + bounded join).
- **`FemVoice.Audio.Abstractions/IRealAudioCaptureBackend.cs` (new)** — `IAudioCaptureService` + `IsBackendAvailable`,
  so readiness can distinguish "real capture available (device opens)" from synthetic/not-configured without
  starting capture.
- **`FemVoice.Audio.Abstractions/CrossPlatformAudioCaptureService.cs` (rewritten)** — now an **OS dispatcher**: on
  Linux it delegates to `AlsaAudioCaptureService` and forwards its frames/device-lost events unchanged; on
  macOS/Windows it holds a null native backend and degrades gracefully to "unavailable". Adds a
  `SelectedBackendDescription` for diagnostics and an `AudioCaptureBackendFactory` (`CreateReal()` / `CreateSynthetic()`)
  so composition roots/smokes pick the backend per-OS in one place. Carries no Windows-audio/WPF/DB code refs.
- **`FemVoice.Avalonia/Audio/AudioReadiness.cs`** — `IsBackendAvailable` now matches any `IRealAudioCaptureBackend`
  (was a `CrossPlatformAudioCaptureService`-only case); doc updated to state a real cross-platform backend now
  exists (real ALSA on Linux) and that routing it into the clinical runtime is a separate later step. No new keys;
  the existing `Audio_DevicesFound` fallback ("Mikrofon: enheter funnet") already covers the available state.
- **`FemVoice.Avalonia/Program.cs`** — new **`--real-audio-capture-smoke` (38th)** that proves real frames when a
  device exists (and graceful degradation otherwise); the existing `--avalonia-audio-backend-smoke` reworked to
  assert **environment-agnostic** invariants (it can no longer assume "always unavailable" now that Linux is real),
  with a shared source-scan guard extended to the ALSA sources. DI is **unchanged** — the synthetic backend is
  still the active runtime backend.

## What did NOT change

No DI default (synthetic stays the runtime backend) · no clinical/scoring/SmartCoach/progression/recovery/DSP
change · no WPF, no Windows-audio project reference, no NAudio · no DB/SQLite · no Core `Strings.*` edits · no
macOS/Windows capture in this dispatcher (deferred) · Avalonia head still references only Core + Audio.Abstractions.

## Verification highlight

`--real-audio-capture-smoke` on this box opened the default ALSA input and captured **real frames off the physical
microphone** (`frames=21 samples=21504 badSample=False devices=1 realAvailable=True`) — all samples finite and in
range — then stopped cleanly. On headless CI with no device the same smoke asserts graceful degradation.

## Follow-up (deferred, separately approved)

1. **Activate real capture in the runtime** (route the real backend's frames into the exercise/dashboard pipeline
   instead of the synthetic source) — the "app stops being display-only" step.
2. **macOS capture** (CoreAudio/AVFoundation) behind the same dispatcher.
3. Windows: wire the existing NAudio adapter through the factory in a Windows composition root.
4. Per-card ALSA device enumeration (this slice uses the "default" PCM only).
