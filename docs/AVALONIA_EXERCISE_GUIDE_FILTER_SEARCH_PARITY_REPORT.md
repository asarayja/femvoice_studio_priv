# Exercise Guide Filter / Search Parity — Slice Report

Date: 2026-06-17 · Branch: `avalonia-exercise-guide-filter-search-parity-slice` (off `main` @ `9dc1a65`).

> **Display/UI-only, behavior-neutral.** No persistence, analytics, session writes, DB reads, clinical scoring,
> SmartCoach, progression, safety gates, exercise-definition or target-profile changes. WPF untouched.

## WPF source-of-truth conclusion
Inspected `FemVoiceStudio/Views/ExerciseWindow.xaml` + `FemVoiceStudio/ViewModels/ExerciseListViewModel.cs`:
- **Category-filter chips: YES.** `ExerciseWindow.xaml` has 6 chip Buttons (styled `CategoryButton`) in a horizontal
  scroller above the list: **Alle, Pitch, Resonance, Intonation, Breathing, Practice** (Tags 0–5), with an explicit
  **"All/Alle"** chip. The selected chip is shown via the `CategoryButton` selected visual state.
- **Search: a VM-level concept.** `ExerciseListViewModel` has `SearchText`; `FilterExercises()` matches
  `ex.Name.Contains(SearchText, OrdinalIgnoreCase) || ex.Description.Contains(...)`. There is **no prominent search
  TextBox** in `ExerciseWindow.xaml` (the only TextBox there is the unrelated subjective-notes field).
- **Combine:** category AND search — `if (matchesCategory && matchesSearch)`.
- **"All/Alle":** `IsAllCategory` → matches everything. Category match normalizes the exercise category
  (pitch/resonans/intonasjon/pust/praksis).
- **Empty results:** WPF simply produces an empty `FilteredExercises` (no dedicated empty-state UI observed).
- WPF's list is DB-backed (`ExerciseDataService`); that dependency is **not** ported.

## Mapping decision (documented)
The Avalonia catalog's `EnhancedExercise.Category` is freeform (e.g. "Grunnleggende", "Samtale") — **not** the
clean WPF filter axis. The clean WPF category axis maps to the exercise **Goal** (`GoalCategory`), which the
Avalonia card already renders as **Tonehøyde / Resonans / Intonasjon / Pust / Kombinert**. So the chip set is
**"Alle" + the distinct exercise goals present** in the in-memory catalog (built dynamically, so every exercise is
reachable). "Kombinert" stands in for WPF's catch-all "Praksis/Practice" slot. Search replicates WPF exactly:
**Name OR Description (ShortDescription), case-insensitive `Contains`**.

## Avalonia changes
- `ExerciseGuideViewModel`: added `CategoryChips` (`["Alle"]` + distinct goals), `SearchText`, `SelectedCategory`,
  `FilteredExercises` (the bound list), `SelectCategoryCommand`, `HasResults`/`IsEmpty`/`FilteredCount`,
  `SearchPlaceholder` ("Søk i øvelser …"), `EmptyText`. `ApplyFilter()` mirrors WPF (`matchesCategory && matchesSearch`)
  over the in-memory cards. `Exercises`/`Categories`/`Count`/`OpenExercise`/progress placeholders **unchanged**.
- `CategoryChipViewModel` (new): `Label` + observable `IsSelected` (drives a converter-free `selected` style class).
- `ExerciseGuideView.axaml`: added a search `TextBox` (Watermark) + a horizontal chip row above the list; bound the
  list to `FilteredExercises`; added an empty-state TextBlock (`IsEmpty`). Preserved the WPF-parity rows (no
  target-Hz), whole-row click, "Dagens fremgang" card, and dark baseline.
- `App.axaml`: added `Button.chipFilter` (+ `.selected`) dark-baseline pill styles (hover via the FluentTheme
  `/template/ ContentPresenter` idiom).

## Behavior
- **Category chips:** "Alle" shows all; selecting a goal chip filters to that goal; exactly one chip is selected at
  a time (selected chip visually distinct via accent fill).
- **Search:** filters the visible list by Name OR Description, case-insensitive; clearing returns all.
- **Combined:** category AND search (intersection), exactly like WPF.
- **Empty state:** a no-match query shows "Ingen øvelser matcher søket." and hides the list.
- **Row click:** unchanged — a filtered card opens the exercise (runtime) page directly.
- All **display-only**: no persistence, analytics, saved search, session writes, or DB-backed progress.

## Smoke
New `--exercise-guide-filter-search-smoke` (24th) verifies: chips exist incl. "Alle"; default shows all; a category
yields a valid non-empty subset (all matching that goal) with exactly one chip selected; clearing returns all;
search filters by name/description; category+search combine; empty state; clearing search returns all; a filtered
card opens the exercise page; and the Guide rows carry no target-Hz (source check, skipped if no source tree).

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
leak guard clean (incl. no `SessionAnalyticsStore`/`IDatabaseService`/`ExerciseSessionRecorder`); no
persistence/DB/analytics; no clinical/domain or WPF behaviour change; no runtime platform implementation.

> The repository is private/proprietary; no open-source license assumed.
