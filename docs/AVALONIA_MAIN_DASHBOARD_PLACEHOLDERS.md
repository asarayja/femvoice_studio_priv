# Avalonia Main Dashboard — Placeholders & Deferred Wiring

Date: 2026-06-16. What in this slice is real vs. a documented placeholder.

## Real (shared services, read-only — no behaviour change)
- **Pitch** — `PitchDetectionService` (YIN) on synthetic frames → `CurrentPitch`.
- **Stabilized trace** — `PitchTraceStabilizer.Filter` → `PitchSamples`.
- **Smoothing/stability/health states** — `LiveMetricsService.CalculateSmoothedPitch` / `CalculateStability` / `CalculateHealth` → `PitchStability`, `HealthStatusDisplay`.
- **Comfort zone** — `PitchTargetZonePolicy.ForDifficulty(DifficultyLevel)` → `ComfortZoneLow/High`.
- **Signal status** — derived from `PitchAnalysisResult.IsVoiced` + confidence.

## Placeholders / deferred (NOT wired in this slice — by design)
| Item | Status | Why deferred |
| --- | --- | --- |
| **FeedbackPipeline / FeedbackConsistencyGuard** | Placeholder: `CurrentFeedbackMessage` is a simple descriptive derivation of live pitch/stability/health states. | The real pipeline needs SmartCoach/health/hydration message sources + the priority/suppression context; wiring it safely is a later slice. **No change to the FeedbackConsistencyGuard contract.** |
| **VocalHealthSupervisor** | Placeholder: `CalculateHealth` is called with `strainLevel = 0`. | The supervisor consumes an `ExerciseLiveState` stream + options; full wiring belongs to the exercise/session slices. Health display is therefore indicative, not the gated safety state. |
| **FemVoiceScoreEngine** | Not shown. | Score display is a later slice (needs user/session context + async baseline). |
| **HydrationAdvisor** | Not shown. | Soft-signal feedback; later slice. |
| **OxyPlot.Avalonia chart** | Placeholder: converter-free `ItemsControl` bar trace. | See `AVALONIA_CHART_PORT_NOTES.md`. |
| **Navigation + Professional tools buttons** | Disabled placeholders. | Exercise Guide / SmartCoach / Progression / Reports / Settings are later slices. |
| **Real Linux microphone capture** | Not implemented (synthetic/noop only). | Out of scope; Windows uses `NAudioCaptureService` (not referenced by Avalonia). |
| **Theme parity** | FluentTheme skeleton only. | Full Light/Dark palette + system-theme port is a later slice. |
| **Localization** | Norwegian literals in AXAML for now. | Full RESX/`{loc:}`-equivalent markup wiring is a later slice; `LocalizationService` is available via DI. |
| **Compiled bindings** | Off for this slice (reflection bindings). | Tighten with `x:DataType` later. |
| **Shared/unit-testable VM** | VM lives in `FemVoice.Avalonia` (verified via `--dashboard-smoke`). | Extracting it to a shared, portable-testable project is a possible later refinement. |

## Safety note
Nothing here changes clinical scoring, SmartCoach, Voice Health gates, recovery, safety priority, reports, persistence, or localization semantics. The dashboard reads shared analysis services and renders their output.
