# Avalonia Exercise Guide — Placeholders & Deferred Wiring

Date: 2026-06-16. What in this slice is real vs. a documented placeholder.

## Real (shared services, read-only — no behaviour change)
- **Exercise catalog** — `VoiceFeminizationExerciseService.GetAllEnhancedExercises()` (pure, no DB, no WPF) → all **15** exercises.
- **Per-exercise metadata** (from `EnhancedExercise`): name, description, steps/instructions, difficulty, category, goal/focus, metrics, target pitch range, duration, frequency, scientific rationale.
- **Navigation**: lightweight shell (`ShellViewModel` + `ContentControl`/`DataTemplate`) — dashboard ↔ guide ↔ detail. No nav framework.

## Placeholders / deferred (by design)
| Item | Status | Why |
| --- | --- | --- |
| **"Start øvelse"** | Placeholder — sets a status string, does nothing clinical. | The real exercise runtime (live-state stream, `ExerciseIntelligenceCoordinator`, hold-progress, safety freeze, progression, subjective report) is a later slice and needs the audio/session pipeline. Not wired to avoid partial clinical behaviour. |
| **Per-exercise safety/health warnings** | Generic, non-clinical reminder on the detail screen. | `EnhancedExercise` has no per-exercise safety field. Exercise-specific safety/target profiles live in `ExerciseTargetProfile`/`IndicatorPackage` (via `ExerciseProfileFactory`) and are a later wiring step. The note explicitly is **not** a Voice-Health gate decision. |
| **Resonance target** | Not shown (only target pitch range is in `EnhancedExercise`). | Resonance targets come from `ExerciseTargetProfile`/`ResonanceStyleTarget`; deferred. |
| **Localized exercise text** | Catalog text shown as-is (Norwegian literals from the catalog). | The catalog strings are already Norwegian; `ExerciseTextService`/`ExerciseGuideTextLocalizer` (key-based localized text) is a later localization step. UI chrome labels are Norwegian literals (consistent with the dashboard slice). |
| **Category grouping UI** | Flat list; category shown per card + a distinct `Categories` list exposed. | A grouped-by-category layout is a minor UI enhancement; flat list satisfies "category/group display". |
| **Theme/localization markup** | FluentTheme skeleton; literals. | Full theme palette + `{loc:}`-equivalent markup is a later slice. |
| **Compiled bindings** | Off (reflection bindings). | Tighten with `x:DataType` later. |

## Safety note
Nothing here changes clinical scoring, SmartCoach, Voice Health gates, recovery, safety priority, reports, persistence, localization semantics, or the exercise definitions/target profiles. The guide/detail are read-only views over the shared catalog.
