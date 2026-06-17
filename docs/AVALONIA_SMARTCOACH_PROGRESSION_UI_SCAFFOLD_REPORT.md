# SmartCoach & Progression UI Scaffold Parity — Slice Report

Date: 2026-06-17 · Branch: `avalonia-smartcoach-progression-ui-parity-scaffold-slice` (off `main` @ `77f3ed3`).

> **UI parity scaffold only — display-only, synthetic, clearly deferred.** No SmartCoach behavior, no progression
> logic, no engines/scoring/safety gates/analytics/persistence/microphone/clinical decisions. WPF untouched.

## WPF source-of-truth conclusion
Inspected `FemVoiceStudio/Views/SmartCoachDashboardView.xaml`, `ProgressionDashboard.xaml`, and their VMs:
- **SmartCoach (`SmartCoachDashboardView`):** header (icon + title) → a **"today's focus"** card (`TodayFocus`
  chip + `TodayRecommendation` text), an optional **health-warning** banner, and three **stat tiles** —
  `CurrentStreak` (days), `SessionsThisWeek` (sessions), `HealthScore` (%). Loading/"building baseline" states.
  All values are **engine/persistence-backed** (`SmartCoachSubsystem`), so they must NOT be ported as real data.
- **Progression (`ProgressionDashboard`):** a **level card** (level badge + `LevelName` + `LevelDescription` +
  progress bar `ProgressBarWidth` + `ProgressText`) with a **`FemVoiceScore`** tile, and a **parameters** card
  with `Resonance` / `Pitch` / `Intonation` rows (each a progress bar + value). All **engine/persistence-backed**
  (`ProgressionSubsystem`, level/score calculation) → reproduce only the **visual structure** with synthetic values.
- Controls implying real behavior (start coached session, compute level/score, safety gating) must stay
  **disabled/deferred**. Labels (titles, "dagens fokus", "FemVoice-score", parameter names) are safe display-only text.

## Avalonia changes (display-only)
Previously both surfaces used one bare generic `DeferredSurfaceViewModel`. Now:
- **`SmartCoachScaffoldViewModel`** (new, sealed, **no services, parameterless ctor, not IDisposable**): title +
  "Utsatt · kun visning" badge + intro; today's-focus card (focus = "Utsatt", synthetic recommendation text);
  three stat tiles all showing **"—"** (Dager på rad / Økter denne uken / Helsescore) with a "Syntetisk · ingen
  lagring" note; a read-only safety note; a **disabled** "Kommer senere" action.
- **`ProgressionScaffoldViewModel`** (new, sealed, **no services, parameterless ctor, not IDisposable**): title +
  deferred badge + intro; level card (badge "—", "Nivå — (utsatt)", description, **empty disabled** progress bar,
  "Kommer senere") + FemVoice-score tile "—"; parameters card with Resonans / Tonehøyde / Intonasjon rows (each
  "—", empty disabled bar); read-only safety note; **disabled** "Kommer senere" action.
- **`SmartCoachScaffoldView.axaml` / `ProgressionScaffoldView.axaml`** (new): cards/chips/tiles using the existing
  `Border.card` / `Border.chip` / `Button.primary` styles and `Shell*` dark-baseline brushes; compact, bounded
  width, modest scaffold (no overbuild). Disabled buttons via `IsEnabled="{Binding ActionEnabled}"` (false).
- **`ShellViewModel`**: Progresjon/SmartCoach nav entries (still `IsImplemented=false` — **deferred**) now route to
  the retained scaffold singletons via `ShowProgression`/`ShowSmartCoach`; nav-title + disposal-exclusion updated.
  Mikrofonkalibrering (and Settings/Analysis/Reports/Diagnostics) unchanged. Nav counts unchanged (9 / 6 impl / 3 deferred).
- **`MainWindow.axaml`**: two new `DataTemplate`s mapping the scaffold VMs to their views.

## Deferred/disabled behavior
Both pages are clearly labelled **utsatt / kun visning / kommer senere / syntetisk / ingen lagring / ingen klinisk
endring**; all numeric values are **"—"** placeholders; the only action buttons are **disabled**; progress bars are
empty and disabled. No real recommendations, scores, levels, or progression decisions are shown or computed.

## Smoke
New `--smartcoach-progression-ui-scaffold-smoke` (25th): navigation opens each scaffold VM (inert, not IDisposable);
both hold no injected services (parameterless ctor — reflection); both are deferred + disabled with synthetic "—"
placeholders (Progression has exactly 3 parameter rows, all "—", empty progress); shell sidebar (9 / 3 deferred) and
dashboard nav remain intact. `--shell-smoke` extended to assert the two scaffolds open inert (and the generic
Mikrofonkalibrering placeholder still works).

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
leak guard clean — **no `SmartCoachEngine` / `ProgressionSafetyGate` / `VoiceHealth` / `VocalHealthSupervisor` /
`RecoveryScorer` / `RecoveryIntelligenceService` / `SessionAnalyticsStore` / `ExerciseSessionRecorder` /
`IDatabaseService` reference** (the scaffold VMs name those types only in `///` doc comments, which the guard
excludes; a forbidden token accidentally placed in a runtime string was caught and reworded). No persistence/DB/
analytics; no clinical/domain or WPF behaviour change; no runtime platform implementation. Build 0/0 also proves the
engine types are not referenced (they are not in the referenced assemblies).

> The repository is private/proprietary; no open-source license assumed.
