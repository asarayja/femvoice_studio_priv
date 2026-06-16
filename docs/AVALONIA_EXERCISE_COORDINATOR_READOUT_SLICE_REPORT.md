# Avalonia Exercise Coordinator Readout — Slice Report

Date: 2026-06-16 · Branch: `avalonia-exercise-coordinator-readout-slice` (off `main` @ `51565d9`, incl. PR #1–#6).

> **Status: IMPLEMENTED (Linux-verified, headless).** Adds a **display-only** readout of the VM-local,
> parameterless `ExerciseIntelligenceCoordinator` to the Avalonia Exercise Runtime screen. Nothing is
> persisted, gated, scored, or enforced. See `_GATE_RESULTS.md` / `_PLACEHOLDERS.md` and the plan doc.

## 1. What this slice does
Wires the **parameterless** `ExerciseIntelligenceCoordinator` into `ExerciseRuntimeViewModel` purely as a
**readout source**. The runtime feeds it synthetic-derived metrics (`UpdateMetrics`) each frame, reads its
in-memory hold/progress (`GetHoldProgress`) and latest `ExerciseLiveState` (via the `ExerciseUpdated`
event), and renders them in a new **"Koordinator-readout"** panel alongside the existing in-VM derived
hold for side-by-side comparison. The coordinator's `IsSafetyLocked` is surfaced as **read-only text,
explicitly labelled non-enforced**; it is never acted on.

## 2. Coordinator safe path used
- **Constructor:** the **parameterless** `ExerciseIntelligenceCoordinator()` only — it sets
  `_currentProfile = ExerciseTargetProfile.ResonanceExercise()` and `_localization = LocalizationService.Instance`.
  The full ctor (which would wire `ResonanceProxyEngine` / `FemVoiceScoreEngine` / `ComfortZoneController` /
  `SmartCoachEngine`, the last pulling `IDatabaseService`) is **NOT** used.
- **Re-verified read-only (live source inspection this slice):**
  - `StartExercise(profile, userId)` → validates args, `StopExercise()` (no-op if idle), `SetExerciseContext()`,
    sets `_isActive` + `_sessionStartTimestamp`. All `lock`-guarded in-memory; no DB/recorder call.
  - `SetExerciseContext()` resets hold/safety state in-memory; `_resonanceEngine?.SetVoiceStyle(...)` is
    null-safe (engine is null on the parameterless path); emits a default `ExerciseLiveState`.
  - `UpdateMetrics(resonance, pitch, stability, health)` caches values, derives pitch min/max from the
    profile, calls the in-memory `EvaluateExerciseStateFromCache()` (throttled ~100 ms) → publishes
    `ExerciseLiveState`. No persistence.
  - `GetHoldProgress()` / `IsExerciseActive` → `lock`-guarded reads of in-memory state (`0–1` fraction / bool).
  - `StopExercise()` resets all cached fields to literal zeros and publishes a zeroed state.
- **No DB / recorder / gate / SmartCoach / Voice-Health / Recovery / progression / mastery / adaptive-difficulty
  field exists on the parameterless instance.** Confirmed in `ExerciseIntelligenceCoordinator.cs` (engine
  fields are nullable `?`, wired only by the full ctor).

## 3. Input metrics fed (synthetic-derived)
Per audio frame, from the dedicated `SyntheticAudioCaptureService` + shared DSP:
- `pitch` — **real** measured F0 (Hz) from `PitchDetectionService` → `LiveMetricsService` → `PitchTraceStabilizer` (0 when unvoiced).
- `resonance = 60.0` — **neutral placeholder** (resonance is not computed in the Avalonia head).
- `stability = 0.8` — **neutral placeholder**.
- `health = 100.0` — **safe placeholder** (high → never trips a health-threshold safety-lock).
- `userId = 1` — fixed positive synthetic probe id (required by `StartExercise`; the coordinator writes no DB).

## 4. Coordinator outputs surfaced (all display-only)
`ExerciseCoordinatorReadoutDisplay` (immutable snapshot, rebuilt each frame and on start/stop):
`IsCoordinatorActive`, `CoordinatorHoldProgressPercent`, `CoordinatorHoldSeconds`, `CoordinatorStatusText`
(holding-correctly / in-comfort-zone / out-of-target), `CoordinatorSafetyLockDisplay` (read-only,
"kun visning, ikke håndhevet"), `CoordinatorGuidanceText`, `CoordinatorRawStateSummary` (raw
`ExerciseLiveState` fields), `DerivedHoldProgressPercent`, `DerivedHoldSeconds`, `HoldDifferenceDisplay`
(coordinator − derived), `ReadoutMode` ("Visning-bare koordinator-readout (ikke håndhevet)").

## 5. Comparison with the derived hold (observed)
For exercise #1 *Grunnleggende humming* (`ResonanceHumming` profile, target resonance **0.50–0.85** — a
normalized 0–1 score; the "Mål-profil" panel rounds it to "0–1" via `:F0` formatting — `RequiredHoldSeconds = 3`):
- **Derived hold** (pitch-band based, 160 Hz in 140–180 Hz) accumulates → ~24 % after ~0.7 s.
- **Coordinator hold** stays **0 %** because the coordinator evaluates *resonance* against the profile's
  0.50–0.85 target and we feed a **neutral placeholder** resonance (60, far outside that band), so it
  reports `inZone=True, holding=False`.
- The panel **shows both** and the difference (`-0.7 s (koordinator − avledet)`), as required. This is the
  expected, documented consequence of feeding a placeholder resonance — it is **display-only** and makes no
  clinical claim.

## 6. Display-only limitations / safety-lock behaviour
- The readout is **informational only**. It never persists, gates, scores, freezes, or alters runtime behaviour.
- `CoordinatorSafetyLockDisplay` renders the coordinator's `IsSafetyLocked` flag as text with an explicit
  "kun visning, ikke håndhevet" (display-only / NOT enforced) label. No freeze/lock is ever applied.
- The existing in-VM **derived hold remains the primary, unchanged display**; the coordinator readout sits alongside.

## 7. Services intentionally NOT used
`ExerciseSessionRecorder` (SQLite), `SubjectiveReport`, `ProgressionSafetyGate`, `MasteryEvaluator`,
`AdaptiveDifficultyService`, `SmartCoachEngine`, `VocalHealthSupervisor`, `RecoveryScorer`,
`RecoveryIntelligenceService`, `ResonanceProxyEngine`, `FemVoiceScoreEngine`, `ComfortZoneController`
(full-ctor deps). No real microphone; no Android; no full WPF parity.

## 8. Files changed
- **New** `FemVoice.Avalonia/ViewModels/ExerciseCoordinatorReadoutDisplay.cs` — immutable display-only readout snapshot.
- **Edit** `FemVoice.Avalonia/ViewModels/ExerciseRuntimeTargetProfileDisplay.cs` — added `ResolveProfile(exercise)`
  (pure Id→`ExerciseTargetProfile`) so the VM reuses the one mapping for the coordinator's `StartExercise`.
- **Edit** `FemVoice.Avalonia/ViewModels/ExerciseRuntimeViewModel.cs` — VM-local parameterless coordinator,
  subscribe `ExerciseUpdated`, `StartExercise` on Begin, `UpdateMetrics` per frame, build `CoordinatorReadout`,
  `StopExercise` on Stop/Back, unsubscribe + `StopExercise` + `Dispose` on dispose; `Dispose` also clears `IsRunning`.
- **Edit** `FemVoice.Avalonia/ViewModels/ShellViewModel.cs` — `OnCurrentPageChanging` disposes a transient,
  disposable outgoing page (the runtime VM) so leaving a running exercise via the always-visible top nav
  stops the synthetic capture + clears the VM-local coordinator (review finding; not a hard-rule issue).
  Retained singletons (`_dashboard`/`_guide`) are never disposed.
- **Edit** `FemVoice.Avalonia/Views/ExerciseRuntimeView.axaml` — new "Koordinator-readout" panel.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--exercise-coordinator-smoke` (asserts active, live-state,
  display-only labels, stop-clears, re-Begin re-activates with fresh state, and nav-away-disposes).
- **Docs** this report + `_GATE_RESULTS.md` + `_PLACEHOLDERS.md` + tracker + plan-doc status.

## 9. Behaviour changes
**None to clinical/domain behaviour.** No scoring, SmartCoach, Voice-Health, recovery, safety priority,
progression, reports, persistence, localization semantics, exercise definitions, or target profiles were
changed. The WPF app is untouched. The coordinator is driven read-only and its output is rendered only.

## 10. Verification (see `_GATE_RESULTS.md`)
Avalonia build 0 warnings · all 6 smokes OK (incl. `--exercise-coordinator-smoke`) · no vulnerable packages ·
leak guard clean (only documentary comments match) · portable tests at known baseline (1570/1580) · Windows
CI status = pending PR.
