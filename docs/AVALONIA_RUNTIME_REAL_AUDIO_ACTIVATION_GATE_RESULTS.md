# Avalonia — Runtime real-audio activation (dashboard) — Gate Results

Date: 2026-07-17 · Branch: `avalonia-runtime-real-audio-activation-slice` (off `main` @ `bbfc63f`) · Host: Linux (.NET 10 `10.0.110`, real mic via ALSA).

> Route the real cross-platform mic into the live dashboard runtime: DI now selects real-when-available, synthetic
> otherwise. Only the frame SOURCE changes; DSP/pitch/scoring unchanged. Exercise-runtime real capture deferred.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Error(s).**

## Smokes (40 — all OK, all exit 0)
39 prior + **`--runtime-real-audio-activation-smoke` (new, 40th)** → **40/40 OK.**
- `--runtime-real-audio-activation-smoke`: `realDevice=True fallbackSafe=True recording=True frames=21 gotFrames=True driven=True stopped=True wired=True` · `runtime backend = CrossPlatformAudioCaptureService`. On a headless/no-mic host the same smoke passes via the synthetic fallback path.
- `--smoke`: now reports `capture backend -> CrossPlatformAudioCaptureService (devices=1)` (DI resolves the real runtime backend).
- All display-only smokes that construct their own synthetic/noop backends directly (dashboard/shell/theme-loc/audio-readiness/audio-backend/real-audio) remain green — none assert the DI default type.

## Real GUI boot
Booted on the display with the real backend wired → stayed alive 5 s, **no exceptions/ALSA errors** (dashboard subscribes to the real backend at construction; capture starts only on the Start button).

## Reference / leak guard
- `FemVoice.Avalonia` references **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- No pitch/stability/health/DSP/scoring/SmartCoach/recovery/Core/WPF change; no DB. Only the DI backend selection +
  a new factory method + one smoke changed. Exercise-runtime source unchanged (still target-tuned synthetic).
- Diff scope: `FemVoice.Audio.Abstractions/CrossPlatformAudioCaptureService.cs`, `FemVoice.Avalonia/Program.cs`, `docs/`.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** — documented baseline (10 pre-existing localization-data
failures). **0 regressions.** Core untouched.

## Behaviour change
The live dashboard is now driven by the **real microphone** when one is available (Linux/ALSA), with a fail-safe
synthetic fallback on headless/no-mic hosts; the status strip reports the true backend. The analysis pipeline
consuming the frames is unchanged, so no clinical/scoring behaviour changes. Exercise-runtime real capture and
macOS/Windows capture remain deferred.
