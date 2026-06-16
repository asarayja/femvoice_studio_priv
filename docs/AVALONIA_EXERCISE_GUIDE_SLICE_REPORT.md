# Avalonia Exercise Guide + Exercise Detail — Slice Report

Date: 2026-06-16 · Branch: `avalonia-exercise-guide-slice` (off `main` @ `2d49f9c`).

## Chosen exercise data source
**`VoiceFeminizationExerciseService.GetAllEnhancedExercises()`** (in `FemVoice.Core`, namespace `FemVoiceStudio.Services`). Chosen over `ExerciseDataService.GetAllExercises()` because it is **pure and UI-free** (parameterless ctor, no SQLite/DB dependency, no WPF), returns all **15** exercises, and exposes the full metadata the guide needs (name, description, steps, difficulty, category, goal, metrics, target pitch range, duration, frequency, scientific rationale). The underlying catalog was **not modified**.

## Number of exercises loaded
**15** (verified by `--exercise-smoke`: "Exercises: 15"). Categories present: Oppvarming, Resonans, Pitch-kontroll, Intonasjon, Pust, Stabilitet, Praksis, Avansert.

## Fields displayed
- **Guide list (cards):** name, category badge, short description, difficulty, goal/focus, target pitch range, duration; "Åpne" → detail.
- **Detail:** title, category, difficulty, goal/focus, frequency, duration, target pitch range, target voice-skills (metrics), purpose (description + rationale), instructions (steps), safety/health note (general), "Start" (placeholder) + status, "Tilbake".

## Localization approach
Catalog content is shown as-is (the catalog is already Norwegian). UI chrome uses Norwegian literals (consistent with the dashboard slice). `LocalizationService` remains available via DI. Key-based localized exercise text (`ExerciseTextService`/`ExerciseGuideTextLocalizer`) is deferred. **No RESX/localization-semantics change.**

## Navigation approach
Lightweight `ShellViewModel` + a `ContentControl` with `DataTemplate`s in `MainWindow` (no nav framework). A top nav bar switches Dashboard ↔ Exercise Guide; guide cards open Detail; Detail "Tilbake" returns to the guide. The dashboard layout moved into `Views/DashboardView.axaml` (UserControl) — a pure UI move; `MainDashboardViewModel` is unchanged, and `--dashboard-smoke` still passes.

## Placeholders
"Start øvelse" (no clinical runtime), per-exercise safety warnings (generic note), resonance target (not in `EnhancedExercise`), localized exercise text, category-grouped layout, theme/compiled-bindings. Full list: `AVALONIA_EXERCISE_GUIDE_PLACEHOLDERS.md`.

## Known gaps
No real Linux microphone capture; exercise runtime workflow not included; resonance/profile metadata not surfaced; theme/localization skeleton; compiled bindings off; the 14 pre-existing test failures remain (unrelated).

## Test/build results
Avalonia build **0 warnings / 0 errors**; `--smoke`/`--dashboard-smoke`/`--exercise-smoke` all **OK**; portable tests **1570/1580** baseline (occasional 1569/11 = documented ComfortZone timing flake). Details: `AVALONIA_EXERCISE_GUIDE_GATE_RESULTS.md`.

## Dependency/security status
`dotnet list --vulnerable --include-transitive` → **no vulnerable packages**; Tmds.DBus.Protocol 0.21.3 pin retained from `main`. Build emits 0 warnings (no NU1903).

## Leak guard result
`FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; no forbidden tokens (`System.Windows`/`Microsoft.Win32`/`MessageBox`/`OxyPlot.Wpf`/`FemVoice.Audio.Windows`/NAudio capture/`ThemeManager`/`LocExtension`/`LocConverter`) in source or AXAML.

## Behaviour changes: **NO**
No clinical/domain behaviour changed. New code: read-only exercise VMs + views, a navigation shell, the `DashboardView` UI move, and `--exercise-smoke`. The shared exercise catalog/services are read-only.

## New files
`ViewModels/`: `ShellViewModel.cs`, `ExerciseGuideViewModel.cs`, `ExerciseCardViewModel.cs`, `ExerciseDetailViewModel.cs`, `ExerciseDisplay.cs`.
`Views/`: `DashboardView.axaml(.cs)`, `ExerciseGuideView.axaml(.cs)`, `ExerciseDetailView.axaml(.cs)`. Plus `MainWindow.axaml(.cs)` (shell host) + `Program.cs` (DI + `--exercise-smoke`).

## Recommended next phase
Per the work order, this slice's PR is opened (not merged). After review/merge, the next phase would extend toward the exercise **runtime** workflow and/or the OxyPlot.Avalonia chart + real FeedbackPipeline wiring slices.
