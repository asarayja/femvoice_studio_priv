# Avalonia Exercise Runtime — Placeholders & Deferred Wiring

Date: 2026-06-16. What in this slice is real vs. a documented placeholder/scaffold.

## Real (shared services, read-only — no behaviour change)
- **Pitch detection / smoothing / stabilization**: `PitchDetectionService` (YIN) + `PitchTraceStabilizer` + `LiveMetricsService` — the same shared, UI-free DSP the dashboard uses, run read-only.
- **Target band**: from the exercise's own `EnhancedExercise.TargetPitchMin/Max`.
- **Navigation**: Exercise Detail → Runtime (via the `Start` command) → back to Detail; existing dashboard/guide/detail nav preserved.

## Scaffold / display-only / deferred (by design)
| Item | Status | Why |
| --- | --- | --- |
| **Audio source** | Dedicated **synthetic** capture (`SyntheticAudioCaptureService`) aimed at the target-band midpoint. | Linux scaffold; no real microphone. Windows would inject the real `IAudioCaptureService` (NAudio) via DI — deferred. The runtime owns its own synthetic instance to avoid cross-talk with the dashboard's capture subscription. |
| **Hold / progress** | **Display-only**, derived in the VM (seconds-in-target / 5 s). | This is a UI indicator, **not** the clinical hold-progress/safety-freeze from `ExerciseIntelligenceCoordinator`. The coordinator (real hold + `ExerciseLiveState` incl. `IsSafetyLocked`) was deliberately **not** wired so no safety/gate decision is faked. |
| **Elapsed time** | Plain wall-clock since Start (in-VM). | No session timing/persistence semantics. |
| **"Stopp"/"Start på nytt"** | Local start/stop of the synthetic stream only. | No session lifecycle, no recording. |
| **Session persistence** | Not wired (`ExerciseSessionRecorder` untouched). | Out of scope — no SQLite session save. |
| **SmartCoach / progression / mastery / adaptive difficulty** | Not wired. | Out of scope — no progression updates. |
| **Voice Health / Recovery / ProgressionSafetyGate decisions** | Not wired; not enforced. | Frozen clinical gates — not invoked or faked. The runtime makes no safety/health decision. |
| **FeedbackConsistencyGuard / FeedbackPipeline** | Not wired; runtime status text is a simple non-clinical derivation. | Deferred (same as the dashboard slice). |
| **Resonance / formant targets** | Not shown (only pitch). | `ExerciseTargetProfile`/`ResonanceStyleTarget` wiring deferred. |
| **Theme/localization, compiled bindings** | FluentTheme skeleton; Norwegian literals; reflection bindings. | Later slices. |

## Safety note
No clinical scoring, SmartCoach, Voice Health gate, recovery, safety priority, progression, reports, persistence, localization semantics, or exercise definitions/target profiles were changed. The runtime view reads shared DSP output and the exercise's own target band, and renders a display-only hold/elapsed.
