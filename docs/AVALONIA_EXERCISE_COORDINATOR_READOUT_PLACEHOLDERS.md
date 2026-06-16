# Avalonia Exercise Coordinator Readout — Placeholders & Deferred Wiring

Date: 2026-06-16. Real vs. placeholder/deferred in this slice.

## Real (read-only, no behaviour change)
- **Coordinator instance**: the genuine `ExerciseIntelligenceCoordinator` (parameterless ctor), VM-local,
  driven read-only. Its hold/progress, `ExerciseLiveState`, comfort-zone/holding flags, safety-lock flag,
  and session-elapsed are **real coordinator outputs** for the metrics fed.
- **Pitch input**: real measured F0 (Hz) from the shared DSP (`PitchDetectionService` → `LiveMetricsService`
  → `PitchTraceStabilizer`).
- **Profile**: real `ExerciseTargetProfile` from the pure `ExerciseProfileFactory` (via `ExerciseProfileMap`
  Id→type, 15/15 mapped) — used for `StartExercise` and the coordinator's internal pitch-range derivation.
- **Lifecycle**: `StartExercise` on Begin, `StopExercise` on Stop/Back/Dispose — all in-memory, no persistence.

## Placeholder / deferred (by design)
| Item | Status | Notes |
| --- | --- | --- |
| `resonance` fed to `UpdateMetrics` | **Neutral placeholder `60.0`** | Resonance is not computed in the Avalonia head. The resonance target is a normalized 0–1 score (e.g. ResonanceHumming **0.50–0.85**; the panel rounds it to "0–1" via `:F0`), so the placeholder 60 is far outside it. Consequence: for resonance-target exercises the coordinator's hold reads 0 % while the pitch-band derived hold accumulates — **both are shown and the difference is documented**. |
| `stability` fed to `UpdateMetrics` | **Neutral placeholder `0.8`** | Not derived from a real stability score this slice. |
| `health` fed to `UpdateMetrics` | **Safe placeholder `100.0`** | High → never trips the health-threshold safety-lock. The lock is display-only anyway. |
| `userId` | **Fixed synthetic `1`** | Required positive id for `StartExercise`. The parameterless coordinator writes no DB/recorder, so this triggers no persistence. |
| Coordinator hold vs derived hold | **Both shown; display-only** | The in-VM derived hold (pitch-band) remains the primary display; the coordinator readout sits alongside with an explicit difference line. |
| `IsSafetyLocked` | **Display-only text, NOT enforced** | Rendered as "Sikkerhetslås: AV/PÅ (kun visning, ikke håndhevet)". Never acted on — no freeze, no score/hold suppression in the Avalonia head. |
| Audio | **Dedicated synthetic capture** | No real mic (Linux/headless). Windows would inject the real `IAudioCaptureService` (deferred). |
| `ExerciseSessionRecorder` / `SubjectiveReport` | **Not wired** | No session persistence. |
| `SmartCoachEngine` / progression / mastery / adaptive-difficulty | **Not wired** | Full-ctor deps; out of scope (SmartCoach pulls DB). |
| `ProgressionSafetyGate` / Voice-Health / Recovery decisions | **Not wired/enforced** | Frozen clinical gates not invoked or faked. |
| Resonance/score engines (`ResonanceProxyEngine` / `FemVoiceScoreEngine` / `ComfortZoneController`) | **Not wired** | Full-ctor deps; null on the parameterless path. |
| Theme/localization parity, compiled bindings | **Skeleton / reflection bindings** | Later slices. |

## Safety note
No clinical scoring, SmartCoach, Voice-Health gate, recovery, safety priority, progression, reports,
persistence, localization semantics, exercise definitions, or target profiles were changed. The slice
drives the parameterless `ExerciseIntelligenceCoordinator` **read-only** and renders its in-memory state;
it makes no clinical decision and enforces no gate or safety-lock. The Safety > Health > Recovery > Comfort
> Voice-Development > Reporting hierarchy is unaffected (nothing in it is invoked or altered).
