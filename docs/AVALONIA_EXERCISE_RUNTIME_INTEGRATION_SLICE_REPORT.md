# Avalonia Exercise Runtime Integration — Slice Report

Date: 2026-06-16 · Branch: `avalonia-exercise-runtime-integration-slice` (off `main` @ `cb5f201`).

## Mapping approach
`EnhancedExercise` carries no `ExerciseProfileType`. The canonical mapping lives in the SQLite seed (`ExerciseDataService`, SortOrder → ProfileType). I copied it **verbatim** into a new Avalonia-only read-only mapper `ExerciseProfileMap` (catalog Id 1–15 → `ExerciseProfileType`), with the source cited in-file. The shared catalog and `ExerciseDataService` are **not modified or read at runtime** (no DB dependency). Unknown ids fail safe to `null` (runtime then shows the exercise's own targets + a documented fallback).

## All 15 exercise mapping results (0 fallback)
| Id | Profile | Id | Profile |
|---|---|---|---|
| 1 | ResonanceHumming | 9 | ResonanceVowels |
| 2 | ResonanceVowels | 10 | ResonanceVowels |
| 3 | CoordinatedGlideUp | 11 | ResonanceHumming |
| 4 | CoordinatedGlideUp | 12 | PitchExercise |
| 5 | StabilityTraining | 13 | CoordinatedGlideUp |
| 6 | StabilityTraining | 14 | StrawPhonation |
| 7 | IntonationExercise | 15 | CoordinatedGlideUp |
| 8 | IntonationExercise | | |

`--exercise-runtime-integration-smoke`: **Mapped 15/15, Fallback 0/15.**

## Target-profile fields surfaced (read-only)
Profile type, target pitch, target resonance range, `RequiredHoldSeconds`, `StabilityThreshold`, voice-skill targets (which signals the profile uses), and localized purpose/physical-focus/safety/common-mistakes text — in a "Mål-profil" panel on the runtime view.

## RequiredHoldSeconds behaviour
The runtime now uses the profile's `RequiredHoldSeconds` as the **display-only** hold target when available (e.g. 3 s for ResonanceHumming), falling back to 5 s if no profile/zero. Hold accumulation remains the in-VM derived value — not clinical.

## Display-only status
Hold/progress, status, and the profile panel are all display-only. No safety-freeze enforcement, no progression, no persistence, no clinical decision.

## Services used
`VoiceFeminizationExerciseService` (catalog, read-only), `ExerciseProfileFactory.CreateProfile` (pure) → `ExerciseTargetProfile`, `LocalizationService.Instance` (key→text, read-only), plus the existing DSP (`PitchDetectionService`/`PitchTraceStabilizer`/`LiveMetricsService`) + `SyntheticAudioCaptureService`.

## Services inspected but not used
`ExerciseIntelligenceCoordinator` (real hold/`ExerciseLiveState`), `ComfortZoneController`, `FeedbackConsistencyGuard`, `ProgressionSafetyGate`, `MasteryEvaluator`, `AdaptiveDifficultyService`, `ExerciseSessionRecorder`, `VocalHealthSupervisor`/`RecoveryScorer`/`RecoveryIntelligenceService` — none invoked or modified.

## Fallback behaviour
Unmapped id → `HasProfile=false`, panel shows "Ingen koblet målprofil … viser øvelsens egne mål" + the exercise's own target pitch. (None of the 15 hit this today.) Missing localization key → readable Norwegian fallback text.

## Localization behaviour
Profile text resolved via `LocalizationService.Instance[key]`; if the key is null or unresolved (returns the key), a documented fallback string is shown. No RESX content/semantics changed.

## Known gaps
Hold/progress display-only (coordinator deferred); resonance target shown as the profile's own (normalized) value, not Hz; no real mic; no persistence/SmartCoach/progression; no safety enforcement; theme/localization skeleton.

## Smoke result
All 5 smokes OK incl. `--exercise-runtime-integration-smoke`. See `AVALONIA_EXERCISE_RUNTIME_INTEGRATION_GATE_RESULTS.md`.

## Leak guard result
Clean — Avalonia references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; no forbidden tokens in source/AXAML.

## Vulnerability result
`dotnet list --vulnerable --include-transitive` → no vulnerable packages.

## Portable test result
1570/1580 baseline (10 pre-existing localization-data failures). No regression.

## Windows CI result
To run on PR open. WPF unaffected (only `FemVoice.Avalonia` changed).

## Behaviour changes: **NO**
New code: `ExerciseProfileMap`, `ExerciseRuntimeTargetProfileDisplay`, runtime VM/view additions, `--exercise-runtime-integration-smoke`. All read-only over shared services; no clinical/domain behaviour changed.

## New/changed files
New: `ViewModels/ExerciseProfileMap.cs`, `ViewModels/ExerciseRuntimeTargetProfileDisplay.cs`. Changed: `ViewModels/ExerciseRuntimeViewModel.cs` (TargetProfile + hold target), `Views/ExerciseRuntimeView.axaml` (Mål-profil panel), `Program.cs` (+`--exercise-runtime-integration-smoke`).

## Recommended next phase
After review/merge: wire the real hold-progress via `ExerciseIntelligenceCoordinator` (read-only/display-only, verified), then the OxyPlot.Avalonia chart and real FeedbackPipeline slices.
