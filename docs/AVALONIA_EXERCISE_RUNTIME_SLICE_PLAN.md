# Avalonia Exercise Runtime — Slice Plan (PREP ONLY)

Date: 2026-06-16 · Branch: `avalonia-exercise-runtime-slice` (off `main` @ `ed0bd43`, incl. PR #1–#4).

> **Status: IMPLEMENTED (Linux-verified).** This was the prep plan; the runtime scaffold is now built (synthetic-audio runtime view, display-only hold/elapsed, detail↔runtime nav, `--exercise-runtime-smoke`). See `AVALONIA_EXERCISE_RUNTIME_SLICE_REPORT.md`, `AVALONIA_EXERCISE_RUNTIME_GATE_RESULTS.md`, `AVALONIA_EXERCISE_RUNTIME_PLACEHOLDERS.md`. The scope below is what was implemented; the coordinator-based real hold and session lifecycle remain deferred.

## 1. Current merged baseline
`main` (`ed0bd43`) includes: portable core, Audio.Abstractions, Audio.Windows, Avalonia shell, Main Dashboard slice (synthetic/noop audio, `--dashboard-smoke`), Exercise Guide + Detail slice (`--exercise-smoke`, 15 exercises, shell nav), Tmds.DBus.Protocol 0.21.3 pin. Linux build green; no vulnerable packages; Avalonia references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`.

## 2. Existing Guide/Detail behaviour
`ShellViewModel` navigates dashboard ↔ guide ↔ detail (`ContentControl`/`DataTemplate`, no nav framework). `ExerciseGuideViewModel` lists the 15 `EnhancedExercise` records (read-only) from `VoiceFeminizationExerciseService`. `ExerciseDetailViewModel` shows metadata + instructions + a general safety note + a **placeholder** "Start øvelse" that only sets a status string.

## 3. Placeholder being replaced
`ExerciseDetailViewModel.Start()` (the no-op "Start øvelse"). It will navigate to a new **Runtime Session View** instead of just setting a status string.

## 4. Proposed runtime view/viewmodel
- `Views/ExerciseRuntimeView.axaml(.cs)` (UserControl) + `ViewModels/ExerciseRuntimeViewModel.cs`.
- Shell adds `ShowRuntime(EnhancedExercise)` → `CurrentPage = new ExerciseRuntimeViewModel(...)`; Detail "Start" calls it; runtime "Stopp/Tilbake" returns to Detail (or Guide).
- Runtime VM exposes: selected exercise title/category/target; `IsRunning`; `CurrentPitch`; `TargetPitchLow/High` (from `EnhancedExercise.TargetPitchMin/Max`); `InTargetRange` (derived); `HoldProgress` (derived or via coordinator — see §6/§7); `ElapsedSeconds`; `RuntimeStatus` (non-clinical string); `StartCommand`/`StopCommand`/`BackCommand`.

## 5. Shared services to inspect (do not change any)
`ExerciseIntelligenceCoordinator` (parameterless ctor; `StartExercise(ExerciseTargetProfile,userId)`, `UpdateMetrics(resonance,pitch,stability,health)`, `GetHoldProgress()`, `StopExercise()`, emits `ExerciseLiveState`), `ExerciseProfileFactory` (`ExerciseProfileType`→`ExerciseTargetProfile`), `ExerciseTargetProfile`, `ComfortZoneController`, `PitchDetectionService`/`PitchTraceStabilizer`/`LiveMetricsService` (already used by the dashboard), `ProgressionSafetyGate`, `FeedbackConsistencyGuard`, `MasteryEvaluator`, `AdaptiveDifficultyService`, `ExerciseSessionRecorder`, `Models/ExerciseLiveState`, `SubjectiveReport`.

## 6. Safe for first wiring (read-only / synthetic)
- Reuse the **dashboard pitch pipeline** (`PitchDetectionService` + `PitchTraceStabilizer` + `LiveMetricsService`) driven by `SyntheticAudioCaptureService` (the 5 modes) — already proven on Linux.
- Use the exercise's own `TargetPitchMin/Max` for the target band; derive `InTargetRange` and a **simple, non-clinical hold indicator** (e.g., seconds-in-range / target) in the VM.
- Use `IUiDispatcher` (Avalonia/Inline) for marshalling, like the dashboard.
- A plain in-VM elapsed timer (`Stopwatch`/`DispatcherTimer`/synthetic frame count).
- (Candidate, verify first) `ExerciseIntelligenceCoordinator` driven **read-only** via `UpdateMetrics` + `GetHoldProgress()`/`ExerciseLiveState` to get the *real* hold-progress — only if confirmed UI-free and behaviour-neutral when fed synthetic metrics; its `IsSafetyLocked` would be **display-only** in this scaffold, not an enforced gate.

## 7. Deferred (not wired in the next slice)
`ExerciseSessionRecorder` (SQLite session save), `ProgressionSafetyGate`/real Voice-Health gate **decisions/enforcement**, `MasteryEvaluator`, `AdaptiveDifficultyService`, `FeedbackConsistencyGuard` full routing, SmartCoach progression updates, `SubjectiveReport` persistence, real hold-progress *enforcement*/safety-freeze, full WPF session parity, real Linux mic capture, reporting/professional workflow, Android.

## 8. Synthetic audio / runtime strategy
Linux/headless uses `SyntheticAudioCaptureService` (StablePitch/UnstablePitch/PitchRampUp/PitchRampDown/Silence) behind `IAudioCaptureService`. The runtime view shows the current pitch vs the exercise target band and updates live from synthetic frames. No real microphone capture. Windows would later use `NAudioCaptureService` (not referenced by Avalonia).

## 9. Smoke test design (`--exercise-runtime-smoke`)
Headless (inline dispatcher): pick an exercise, construct the runtime VM, Start, feed synthetic frames (~0.5s per mode incl. an in-target mode), assert: `IsRunning`, `CurrentPitch>0` when voiced, `TargetPitchLow/High` set from the exercise, `ElapsedSeconds` advances, hold indicator moves when in range, Stop sets `IsRunning=false`, and shell nav detail→runtime→back works. Print concise CI-friendly lines (e.g. `Runtime smoke OK / Exercise: <name> / Pitch: <hz> / Target: <lo>-<hi> / Hold: <x>`).

## 10. Leak guard requirements
`FemVoice.Avalonia` must keep referencing only `FemVoice.Core` + `FemVoice.Audio.Abstractions`. No `System.Windows`/`Microsoft.Win32`/`MessageBox`/`OxyPlot.Wpf`/`FemVoice.Audio.Windows`/NAudio capture/`ThemeManager`/`LocExtension`/`LocConverter` in source or AXAML. Keep the Tmds.DBus.Protocol 0.21.3 pin.

## 11. Build/test gate
`dotnet build FemVoice.Avalonia` (0 warnings) · `--smoke`/`--dashboard-smoke`/`--exercise-smoke`/`--exercise-runtime-smoke` OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable` at baseline (1570/1580) · leak guard clean · Windows CI green via PR.

## 12. Risks
- **Coordinator wiring**: `ExerciseIntelligenceCoordinator` emits safety/hold state; wiring it must stay read-only/display-only and not enforce or alter gate decisions. If any doubt → use the derived in-VM hold instead and defer the coordinator.
- **Timer/threading**: synthetic frames arrive on a background thread; marshal via `IUiDispatcher` (use `InlineUiDispatcher` for the smoke).
- **Scope creep** toward session save / real gates — explicitly deferred.

## 13. Explicit non-goals
No clinical/domain behaviour change; no session persistence; no real safety/health gate enforcement; no SmartCoach/progression updates; no real Linux mic capture; no Android; no full WPF parity.
