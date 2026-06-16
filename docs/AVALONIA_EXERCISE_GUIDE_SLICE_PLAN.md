# Avalonia Exercise Guide + Exercise Detail — Slice Plan (PREP ONLY)

Date: 2026-06-16 · Branch: `avalonia-exercise-guide-slice` (off `main` @ `2d49f9c`, which includes PR #1/#2/#3).

> **Status: PREPARATION ONLY — not implemented in this prompt.** This doc captures scope + a wiring approach so the next prompt can implement directly. No Exercise Guide code exists on this branch yet (it's the same as `main` plus this plan doc).

## Goal
Second real Avalonia parity slice: an Exercise Guide list + Exercise Detail screen, navigable from the dashboard, driven by the **shared, UI-free** exercise services (`FemVoice.Core`). No clinical/domain behaviour change; no Windows-only dependency in `FemVoice.Avalonia`.

## In scope
- Exercise Guide list (the 15-exercise catalog).
- Exercise category/group display.
- Exercise cards.
- Exercise detail screen.
- Exercise instructions.
- Target pitch/resonance/voice-skill metadata.
- Safety/health warnings (display).
- Difficulty display.
- Localization basics.
- Navigation from dashboard → Exercise Guide.
- Synthetic/noop audio only where needed.

## Out of scope
Real Linux microphone capture; reports/professional tools; full settings parity; full SmartCoach dashboard; full progression analytics; Android; clinical/domain behaviour changes.

## Likely shared services (read-only; verify exact APIs when implementing)
- `VoiceFeminizationExerciseService` — the 15-exercise catalog (`GetAllEnhancedExercises()`), or `ExerciseDataService.GetAllExercises()` (SQLite-seeded). Confirm which is the right read path for the list.
- `Models/Exercise.cs`, `ExerciseDefinition.cs`, `ExerciseTargetProfile.cs`, `IndicatorPackage.cs` — metadata (target ranges, indicators, difficulty).
- `ExerciseTextService` / `ExerciseGuideTextLocalizer` — localized exercise/guide text + instructions.
- `ExerciseProfileFactory` (`ExerciseProfileType` → `ExerciseTargetProfile`) — target profiles.
- `LocalizationService` (via DI) for labels.
- Reuse the existing `IAudioCaptureService` (synthetic/noop) + `MainDashboardViewModel` patterns; add an `ExerciseGuideViewModel` + `ExerciseDetailViewModel` (Avalonia-safe, like the dashboard VM).

## Constraints (carry forward)
Forbidden in `FemVoice.Avalonia`: `System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot.Wpf`, `FemVoice.Audio.Windows`, NAudio capture APIs, WPF `ThemeManager`/`LocExtension`/`LocConverter`. Keep the Tmds.DBus.Protocol 0.21.3 pin. Portable tests stay at the known baseline (1570/1580). Use the same Linux gate (build + `--smoke` + portable tests + vuln scan + leak guard).

## Verification gate (to run when implemented)
`dotnet build FemVoice.Avalonia` (green) · `--smoke` OK · a new `--exercise-smoke` (or extend dashboard smoke) listing the 15 exercises + a detail readout · `FemVoice.Tests.Portable` baseline · `dotnet list --vulnerable` clean · leak guard clean · Windows CI green via PR.
