# Avalonia — Runtime real-audio activation (dashboard) — Slice Report

Date: 2026-07-17 · Branch: `avalonia-runtime-real-audio-activation-slice` (off `main` @ `bbfc63f`) · Host: Linux (.NET 10 `10.0.110`, real mic via ALSA).

## Goal

Route the real cross-platform microphone (Stage 3C) **into the live runtime** so the app stops being display-only:
the main dashboard's live pitch/stability/health/feedback + chart are now driven by the **actual microphone** when
one is available, falling back to the synthetic display-only backend on headless/CI/no-mic hosts.

User explicitly approved touching the (otherwise frozen) runtime for this. **Only the SOURCE of audio frames
changes** — the shared pitch/stability/health services consuming them are byte-for-byte unchanged, so there is no
clinical/DSP/scoring behaviour change.

## What changed (files) — minimal + surgical

- **`FemVoice.Audio.Abstractions/CrossPlatformAudioCaptureService.cs`** — `AudioCaptureBackendFactory.CreateForRuntime()`:
  returns the REAL backend when `IsBackendAvailable` (Linux/ALSA today), else disposes the probe and returns the
  synthetic backend. Cheap availability probe (open+close of the default device); never throws.
- **`FemVoice.Avalonia/Program.cs`** — the DI registration for `IAudioCaptureService` changed from always-synthetic
  to `AudioCaptureBackendFactory.CreateForRuntime()`. Because `MainDashboardViewModel` and `ShellViewModel` both
  receive the DI-registered capture service, this activates the real mic for the **dashboard live pitch** and makes
  the **status strip** report truthfully ("Mikrofon: enheter funnet: N"). New `--runtime-real-audio-activation-smoke`
  (40th).

`MainDashboardViewModel` already consumed the injected `IAudioCaptureService` generically (subscribes to
`FrameAvailable`/`DeviceLost`, `Start()` → `StartAsync`, runs the shared `PitchDetectionService`/`LiveMetricsService`
on each frame). No VM/DSP edit was needed — the abstraction did its job.

## What did NOT change

No pitch/stability/health/DSP/scoring code · no `MainDashboardViewModel` analysis logic · no SmartCoach/recovery/
progression · no DB/Core/WPF · the **exercise runtime** keeps its deliberately target-tuned synthetic source
(`ExerciseRuntimeViewModel`) — routing real mic there is a separate follow-up. `FemVoice.Avalonia` still references
only Core + Audio.Abstractions. `DeviceLost` still surfaces "Mikrofon utilgjengelig" safely.

## Verification highlight

`--runtime-real-audio-activation-smoke` drove the actual `MainDashboardViewModel` with the runtime backend:
`realDevice=True recording=True frames=21 gotFrames=True driven=True stopped=True wired=True` and
`runtime backend = CrossPlatformAudioCaptureService` — i.e. the dashboard was fed **real microphone frames** through
the unchanged pipeline. Base `--smoke` now reports `capture backend -> CrossPlatformAudioCaptureService (devices=1)`.
On a headless/no-mic host the factory returns the synthetic backend and the same smoke passes via the fallback path.
The real GUI booted cleanly with the real backend wired.

## Follow-up (deferred)

1. **Exercise-runtime real capture** — route the real mic into `ExerciseRuntimeViewModel` (currently a target-tuned
   synthetic source), with a per-exercise target/UX design.
2. macOS/Windows real capture through the same factory.
3. Fuller feedback wiring (FeedbackConsistencyGuard/VocalHealthSupervisor) remains a separate, approval-gated step.
