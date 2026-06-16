# Avalonia Exercise Runtime — Slice Report

Date: 2026-06-16 · Branch: `avalonia-exercise-runtime-slice` (off `main` @ `ed0bd43`).

## Selected runtime architecture
`ExerciseDetailViewModel.Start` now navigates (via `ShellViewModel.ShowRuntime`) to a new `ExerciseRuntimeViewModel` + `ExerciseRuntimeView` (UserControl, hosted by the shell's `ContentControl`/`DataTemplate`). Back/Stop returns to the Exercise Detail. The runtime VM drives the shared, UI-free DSP services read-only from a dedicated synthetic capture.

## Synthetic audio / runtime strategy
The runtime owns a `SyntheticAudioCaptureService` aimed at the exercise target-band midpoint (StablePitch), so the scaffold visibly sits "in target". Frames → `PitchDetectionService` → `PitchTraceStabilizer` → `LiveMetricsService` → current pitch; compared to the exercise's `TargetPitchMin/Max`. A dedicated instance avoids cross-talk with the dashboard's shared capture subscription. No real microphone; Windows would inject the real `IAudioCaptureService` (deferred).

## What is real
Pitch detection/smoothing/stabilization (shared services, read-only); target band from `EnhancedExercise`; pitch-status classification (under/in/over/no-voice); navigation detail↔runtime.

## What is display-only
Hold accumulation (seconds-in-target / 5 s) and elapsed time — computed in the VM for display; **not** clinical hold-progress/safety state.

## What is deferred
`ExerciseIntelligenceCoordinator` (real hold + `ExerciseLiveState`/`IsSafetyLocked`), `ExerciseSessionRecorder` (persistence), `ProgressionSafetyGate`/Voice-Health/Recovery decisions, SmartCoach/progression/mastery/adaptive-difficulty, `FeedbackPipeline`, resonance/formant targets, real Linux mic capture, theme/localization parity. See `AVALONIA_EXERCISE_RUNTIME_PLACEHOLDERS.md`.

## Services inspected
`ExerciseIntelligenceCoordinator` (ctor + `StartExercise`/`UpdateMetrics`/`GetHoldProgress`/`StopExercise` + `ExerciseLiveState`), `ExerciseProfileFactory`/`ExerciseTargetProfile`, `ComfortZoneController`, `ProgressionSafetyGate`, `FeedbackConsistencyGuard`, `ExerciseSessionRecorder`, `VoiceFeminizationExerciseService`/`EnhancedExercise`, plus the dashboard's `PitchDetectionService`/`PitchTraceStabilizer`/`LiveMetricsService` + `SyntheticAudioCaptureService`/`IAudioCaptureService`/`IUiDispatcher`.

## Services used
`VoiceFeminizationExerciseService` (catalog, read-only), `PitchDetectionService` + `PitchTraceStabilizer` + `LiveMetricsService` (read-only DSP), `SyntheticAudioCaptureService` (synthetic input), `IUiDispatcher` (marshalling). `EnhancedExercise.TargetPitchMin/Max` for the band.

## Services intentionally not used
`ExerciseIntelligenceCoordinator`, `ExerciseSessionRecorder`, `ProgressionSafetyGate`, `FeedbackConsistencyGuard`, `ComfortZoneController` (the runtime uses the exercise's own target band), `MasteryEvaluator`, `AdaptiveDifficultyService` — to avoid faking clinical/gating/persistence behaviour. None were modified.

## Smoke results
`--smoke`/`--dashboard-smoke`/`--exercise-smoke` OK; `--exercise-runtime-smoke` OK (Pitch 160 Hz in 140–180 target, Status "Innenfor målområde", Hold 0.7s/14%, nav runtime→back-to-detail). See `AVALONIA_EXERCISE_RUNTIME_GATE_RESULTS.md`.

## Leak guard result
Clean — Avalonia references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; no forbidden tokens in source/AXAML.

## Vulnerability result
`dotnet list --vulnerable --include-transitive` → no vulnerable packages.

## Portable test result
1570/1580 baseline (10 pre-existing localization-data failures; occasional ComfortZone timing flake). No regression from this slice.

## Windows CI status
To run on PR open (`Windows WPF Verification` workflow). WPF unaffected (only `FemVoice.Avalonia` changed).

## Behaviour changes: **NO**
No clinical/domain behaviour changed. New code: `ExerciseRuntimeViewModel` + `ExerciseRuntimeView`, detail→runtime nav wiring, `--exercise-runtime-smoke`. The `ExerciseDetailViewModel` "Start" now navigates instead of setting a placeholder string.

## New/changed files
New: `ViewModels/ExerciseRuntimeViewModel.cs`, `Views/ExerciseRuntimeView.axaml(.cs)`. Changed: `ViewModels/ExerciseDetailViewModel.cs` (Start → navigate), `ViewModels/ShellViewModel.cs` (+IUiDispatcher, ShowRuntime), `MainWindow.axaml` (+runtime DataTemplate), `Program.cs` (+`--exercise-runtime-smoke`).

## Recommended next phase
After review/merge: wire real hold-progress via `ExerciseIntelligenceCoordinator` (read-only/display-only, verified), then session lifecycle (still non-persistent), and later the OxyPlot.Avalonia chart + real FeedbackPipeline slices.
