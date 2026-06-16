# Avalonia Exercise Runtime Integration — Slice Plan (PREP ONLY)

Date: 2026-06-16 · Branch: `avalonia-exercise-runtime-integration-slice` (off `main` @ `cb5f201`, incl. PR #1–#5).

> **Status: PLANNING ONLY — no integration code in this prompt.** This branch currently equals `main` + this plan doc.

## 1. Current merged baseline
`main` (`cb5f201`): portable core, Audio.Abstractions, Audio.Windows, Avalonia shell + dashboard, Exercise Guide + Detail, Exercise Runtime **scaffold** (synthetic audio, display-only hold/elapsed), Tmds 0.21.3 pin. Linux build green; all four smokes OK; no vulnerable packages; Avalonia refs only `FemVoice.Core` + `FemVoice.Audio.Abstractions`.

## 2. Runtime scaffold behaviour
`ExerciseRuntimeViewModel`/`ExerciseRuntimeView`: a dedicated `SyntheticAudioCaptureService` (aimed at the exercise target-band midpoint) → shared DSP (`PitchDetectionService`/`PitchTraceStabilizer`/`LiveMetricsService`, read-only) → current pitch vs `EnhancedExercise.TargetPitchMin/Max`; **display-only** hold (seconds-in-target / 5 s) + elapsed; status under/in/over/no-voice; detail↔runtime nav.

## 3. Known placeholders (today)
Hold/progress is display-only (not the coordinator's clinical hold/safety state); target band from `EnhancedExercise` only (no resonance/profile metadata); no persistence, no SmartCoach/progression, no safety/health/recovery decisions, no real mic. See `AVALONIA_EXERCISE_RUNTIME_PLACEHOLDERS.md`.

## 4. Candidate service inventory (inspected)
- `ExerciseProfileFactory.CreateProfile(ExerciseProfileType) → ExerciseTargetProfile` (pure mapping).
- `ExerciseTargetProfile`: `UsesResonance/Pitch/Stability/Intensity`, localization KEYS (`ClinicalPurposeKey`, `PhysicalFocusKey`, `CommonMistakesKey`, `SafetyInfoKey`, `FeedbackModeKey`, `ThresholdStrategyKey`, `IndicatorPackageSummaryKey`), `MinPitch`/`MaxPitch`, `TargetResonanceMin/Max`, `RequiredHoldSeconds`, `StabilityThreshold`.
- `IndicatorPackage`: `SummaryKey` + `Indicators` (`IndicatorType` list).
- `ComfortZoneController`, `FeedbackConsistencyGuard`, `ProgressionSafetyGate`, `MasteryEvaluator`, `AdaptiveDifficultyService`, `ExerciseSessionRecorder`, `SubjectiveReport`, `VocalHealthSupervisor`, `RecoveryScorer`/`RecoveryIntelligenceService`, `ExerciseIntelligenceCoordinator` (parameterless ctor; `StartExercise`/`UpdateMetrics`/`GetHoldProgress`/`StopExercise`; emits `ExerciseLiveState` incl. `IsSafetyLocked`).

## 5. Safe to wire now (read-only display)
- **`ExerciseProfileFactory` + `ExerciseTargetProfile`**: surface target resonance range, `RequiredHoldSeconds` (use as the hold *target* instead of the hardcoded 5 s — display-only), `StabilityThreshold`, and the localized purpose/physical-focus/common-mistakes/safety-info text via `ILocalizationService` (key → string). All read-only; changes nothing.
- **`IndicatorPackage`**: display the indicator summary/list (localized) for the exercise.
- `ILocalizationService` (already in DI) to resolve the profile's localization keys.

## 6. Read-only / display-only services (cautious; verify before wiring)
- `ExerciseIntelligenceCoordinator`: could provide a *real* hold-progress (`GetHoldProgress`) and `ExerciseLiveState` if driven read-only via `UpdateMetrics(resonance,pitch,stability,health)` from synthetic-derived metrics. Its `IsSafetyLocked`/safety state must be **display-only** (never enforced) in this slice, and must be verified UI-free + behaviour-neutral first. If any doubt → keep the in-VM derived hold.
- `ComfortZoneController`: read-only comfort-zone display only (the exercise target band already covers the core need).

## 7. Requires synthetic adapter
- `FeedbackConsistencyGuard`: only if it can be called **read-only** (`Submit` returns a decision) with a synthetic candidate + context; render the *approved* message. Needs careful synthetic feeding; otherwise defer.

## 8. Deferred (not this slice)
`MasteryEvaluator`, `AdaptiveDifficultyService`, `ProgressionSafetyGate` (decisions/enforcement), `ExerciseIntelligenceCoordinator` *enforcement* of safety-freeze.

## 9. Forbidden until full session pipeline
`ExerciseSessionRecorder` (SQLite persistence), `SubjectiveReport` persistence, `VocalHealthSupervisor`/`RecoveryScorer`/`RecoveryIntelligenceService` *decisions/gating*, real Voice-Health/Recovery enforcement, real Linux mic capture.

## 10. Proposed next runtime UI additions
- A "Mål-profil" panel: target resonance range, required hold seconds, stability threshold, which signals the exercise uses (`UsesResonance/Pitch/Stability/Intensity`).
- Localized "Hensikt / Fysisk fokus / Vanlige feil / Sikkerhet" text from the profile keys.
- Indicator-package summary/list.
- Use `RequiredHoldSeconds` as the (display-only) hold target.
- (Optional, verified) real hold-progress from `ExerciseIntelligenceCoordinator` (display-only).

## 11. Synthetic audio strategy
Unchanged: dedicated `SyntheticAudioCaptureService` aimed at the target band (5 modes available). No real mic. If the coordinator is wired read-only, feed it synthetic-derived metrics (pitch from DSP; resonance/stability placeholders) — display-only.

## 12. Smoke test design (`--exercise-runtime-integration-smoke`, or extend the runtime smoke)
Headless: resolve an exercise → its `ExerciseTargetProfile` via `ExerciseProfileFactory` (verify mapping); assert profile metadata present (target resonance, RequiredHoldSeconds, ≥1 localized key resolves to non-empty text); runtime shows the profile panel; synthetic pitch in-band; hold target = RequiredHoldSeconds; nav detail→runtime→back. Print concise CI-friendly lines.

## 13. Leak guard requirements
`FemVoice.Avalonia` keeps referencing only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; no `System.Windows`/`Microsoft.Win32`/`MessageBox`/`OxyPlot.Wpf`/`FemVoice.Audio.Windows`/NAudio capture/`ThemeManager`/`LocExtension`/`LocConverter`. Keep Tmds 0.21.3.

## 14. Build/test gate
`dotnet build FemVoice.Avalonia` (0 warnings) · all smokes incl. the new one OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable` baseline (1570/1580) · leak guard clean · Windows CI green via PR.

## 15. Risks & explicit non-goals
**Risks:** (a) exercise→`ExerciseProfileType` mapping — `EnhancedExercise` doesn't carry a profile type; need a safe read-only mapping (by Goal/Id) or another derivation, documented; if none is clean, surface only what `EnhancedExercise` provides. (b) Coordinator wiring must stay read-only/display-only — never enforce safety/gates. (c) Localization keys may be missing for some profiles → fallback text, documented. **Non-goals:** no clinical/domain behaviour change; no persistence; no safety/health/recovery enforcement or decisions; no SmartCoach/progression; no real mic; no Android; no full WPF parity.
