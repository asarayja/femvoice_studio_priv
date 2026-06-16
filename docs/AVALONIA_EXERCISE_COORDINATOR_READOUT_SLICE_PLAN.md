# Avalonia Exercise Coordinator Readout — Slice Plan (PREP ONLY)

Date: 2026-06-16 · Branch: `avalonia-exercise-coordinator-readout-slice` (off `main` @ `51565d9`, incl. PR #1–#6).

> **Status: PLANNING ONLY — no coordinator-readout code in this prompt.** This branch currently equals `main` + this plan doc.

## 1. Current merged baseline
`main` (`51565d9`): portable core, Audio.Abstractions/Windows, Avalonia shell + dashboard, Exercise Guide + Detail, Exercise Runtime scaffold, Runtime **target-profile integration** (read-only `ExerciseTargetProfile`/`IndicatorPackage` panel, Id→ProfileType map 15/15, `RequiredHoldSeconds` as display-only hold target). 5 smokes green; no vulnerable packages; Avalonia refs only Core + Abstractions.

## 2. Runtime integration behaviour (today)
Synthetic pitch → shared DSP → pitch-vs-target status + display-only hold/elapsed; "Mål-profil" panel from `ExerciseProfileFactory`/`ExerciseTargetProfile`; hold target = profile `RequiredHoldSeconds`.

## 3. Current display-only limitations
Hold/progress is an in-VM derived value (not the coordinator's real hold); no in-target/safety interpretation from shared logic; resonance/health are not computed; no persistence/SmartCoach/progression/gate.

## 4. Candidate service inventory (inspected)
`ExerciseIntelligenceCoordinator` (parameterless ctor sets only `_currentProfile` + `LocalizationService.Instance`; `StartExercise(profile,userId)`/`StopExercise()`/`UpdateMetrics(resonance,pitch,stability,health)`/`GetHoldProgress()`/`IsExerciseActive`; events `ExerciseUpdated(ExerciseLiveState)` + `InlineCoachUpdated`; **no DB/Recorder/gate field references** — pure in-memory state machine), `ComfortZoneController`, `FeedbackConsistencyGuard`, `ProgressionSafetyGate`, `MasteryEvaluator`, `AdaptiveDifficultyService`, `ExerciseSessionRecorder`, `SubjectiveReport`, `VocalHealthSupervisor`, `RecoveryScorer`, `RecoveryIntelligenceService`.

## 5. Is ExerciseIntelligenceCoordinator safe to call? — YES (read-only)
**Yes, via the parameterless ctor.** It holds no persistence, no DB, no `ExerciseSessionRecorder`, no `ProgressionSafetyGate`, no `SmartCoachEngine` (those are only in the *full* ctor, which we will NOT use). `StartExercise`/`UpdateMetrics`/`GetHoldProgress`/`StopExercise` mutate only in-memory state and raise `ExerciseLiveState` events. Nothing persists, gates, or affects the user outside the VM. Its `IsSafetyLocked` in `ExerciseLiveState` is a computed flag — surfaced **display-only**, never enforced.

## 6. Required constructor dependencies
- **Parameterless ctor:** none (defaults `_currentProfile` + localization). ← use this.
- Full ctor (NOT used): `ResonanceProxyEngine`, `FemVoiceScoreEngine`, `ComfortZoneController`, `SmartCoachEngine` (the last pulls `IDatabaseService`/DB — avoided).

## 7. Pure / read-only dependencies
Parameterless coordinator + `ExerciseTargetProfile` (from `ExerciseProfileFactory`) + `LocalizationService` — all pure/read-only. `UpdateMetrics` is fed synthetic-derived values (pitch from DSP; resonance/stability/health as documented placeholders).

## 8. Dependencies that mutate state / need session lifecycle
`ExerciseSessionRecorder` (SQLite writes), `ProgressionSafetyGate`/`MasteryEvaluator`/`AdaptiveDifficultyService` (history/gating), `VocalHealthSupervisor`/`RecoveryScorer`/`RecoveryIntelligenceService` (clinical decisions), `SubjectiveReport` (persistence), `SmartCoachEngine` (DB). None used.

### Classification
| Service | Class |
| --- | --- |
| ExerciseIntelligenceCoordinator (parameterless) | **Safe to call read-only** (display-only safety flag) |
| ComfortZoneController | Safe to call read-only (display-only) |
| FeedbackConsistencyGuard | Safe only with synthetic adapter (read-only Submit) |
| ProgressionSafetyGate / MasteryEvaluator / AdaptiveDifficultyService | Deferred |
| VocalHealthSupervisor / RecoveryScorer / RecoveryIntelligenceService | Display-only with caveats → Deferred this slice |
| ExerciseSessionRecorder / SubjectiveReport persistence | Forbidden until full session pipeline |

## 9. Proposed coordinator-readout UI
A "Coordinator-readout" panel in the runtime view (read-only): coordinator hold-progress %, in-comfort-zone, holding-correctly, **safety-lock indicator (display-only text)**, session-elapsed (from `ExerciseLiveState`), and a side-by-side comparison of coordinator hold vs the current in-VM derived hold. Clearly labelled "readout — ikke håndhevet" (not enforced).

## 10. Proposed smoke test (`--exercise-coordinator-smoke`)
Headless: construct parameterless coordinator; `StartExercise(profileForExercise1, syntheticUserId)`; feed several `UpdateMetrics(...)` with in-target pitch; assert `IsExerciseActive`, `GetHoldProgress()` increases, an `ExerciseUpdated` `ExerciseLiveState` is received with sane fields; `StopExercise()` zeroes/ends; assert no exception and no persistence call. Print concise lines (hold %, in-zone, safety-lock=false). Verify the coordinator never touches DB (parameterless).

## 11. Leak guard requirements
`FemVoice.Avalonia` keeps referencing only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; no `System.Windows`/`Microsoft.Win32`/`MessageBox`/`OxyPlot.Wpf`/`FemVoice.Audio.Windows`/NAudio capture/`ThemeManager`/`LocExtension`/`LocConverter`. Keep Tmds 0.21.3.

## 12. Build/test gate
`dotnet build FemVoice.Avalonia` (0 warnings) · all smokes incl. the new one OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable` baseline (1570/1580) · leak guard clean · Windows CI green via PR.

## 13. Risks
- **userId**: `StartExercise` requires a positive userId — use a fixed synthetic probe id; verify it triggers no DB/recorder write (the parameterless coordinator has none, but confirm `StartExercise` body doesn't call out).
- **UpdateMetrics inputs**: resonance/stability/health are placeholders (pitch is real); document and keep display-only.
- **Safety-lock**: must be display-only; never enforce a freeze or act on it.
- **Threading**: coordinator is lock-guarded; marshal `ExerciseUpdated` to the UI via `IUiDispatcher`.
- **Scope creep** toward the full ctor (SmartCoach/DB) — explicitly avoided.

## 14. Explicit non-goals
No clinical/domain behaviour change; no persistence; no safety/health/recovery enforcement or decisions; no SmartCoach/progression/mastery/difficulty; no real mic; no Android; no full WPF parity.

## 15. Recommendation
**Implement the readout** — wire the **parameterless** `ExerciseIntelligenceCoordinator` read-only in the runtime VM: feed synthetic-derived metrics, surface its hold-progress + `ExerciseLiveState` fields (incl. a display-only safety-lock indicator), and compare against the in-VM derived hold. It is safe (in-memory, no persistence/gate/DB). Keep the existing derived hold as the primary display; show the coordinator readout alongside, labelled non-enforced. Add `--exercise-coordinator-smoke`. (Fallback if implementation surfaces any hidden state mutation: downgrade to a "service-readiness report" only — but inspection shows the parameterless path is clean.)
