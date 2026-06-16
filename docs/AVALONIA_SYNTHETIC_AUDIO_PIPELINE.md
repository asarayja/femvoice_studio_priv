# Avalonia Synthetic Audio Pipeline

Date: 2026-06-16. File: `FemVoice.Audio.Abstractions/SyntheticAudioCaptureService.cs`.

## Purpose
Drive the real DSP/analysis pipeline end-to-end on Linux/headless/CI **without a microphone**, so the Avalonia dashboard can be developed and verified. It is a test/bootstrap backend behind `IAudioCaptureService`; it changes no DSP behaviour — it only produces float frames a real mic could also produce. **Not** used on Windows production (that uses `NAudioCaptureService`).

## Modes (`SyntheticAudioMode`)
| Mode | Signal | Dashboard effect |
| --- | --- | --- |
| `StablePitch` | steady sine at `BaseFrequency` (200 Hz) | pitch ≈ 200 Hz, "Veldig stabil" |
| `UnstablePitch` | 200 Hz + 25·sin(2π·7t) + 12·sin(2π·13.3t) wobble | varying pitch, lower stability |
| `PitchRampUp` | 150 → 260 Hz sawtooth glide (6 s) | rising pitch |
| `PitchRampDown` | 260 → 150 Hz glide (6 s) | falling pitch |
| `Silence` | zeros | "Ingen stemme" |

Implementation uses a continuous **phase accumulator** (`_phase += 2π·f/sr` per sample) so frequency changes are click-free; mono float in [-1, 1], default 44.1 kHz, 1024-sample buffer (matches the WPF baseline defaults). Frames are emitted on a background `Task` every ~buffer-duration; `StartAsync`/`StopAsync` are idempotent and cancellation-safe.

## Verified behaviour (`--dashboard-smoke`, Linux)
```
mode=StablePitch    pitch=200.0Hz  stability=Veldig stabil   signal=Stemme (100%)  feedback="Fin, stabil tone i komfortsonen."
mode=PitchRampUp    pitch=166.2Hz  (rising glide caught mid-ramp)
mode=UnstablePitch  pitch=191.2Hz  stability=Stabil
mode=Silence        pitch=  0.0Hz  signal=Ingen stemme       feedback="Ingen stemme oppdaget — prøv å snakke jevnt."
```

## Switching backends
- **Linux/Avalonia (default):** `IAudioCaptureService → SyntheticAudioCaptureService` (`Program.cs`).
- **Headless tests/smoke:** constructed directly + `InlineUiDispatcher`.
- **No-signal default option:** `NoopAudioCaptureService` (no frames).
- **Windows production:** `IAudioCaptureService → NAudioCaptureService` (FemVoice.Audio.Windows) — wired in a Windows composition root, never referenced by `FemVoice.Avalonia`.
