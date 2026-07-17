# Avalonia — Progression engine-backed (real level/score/summary) — Slice Report

Date: 2026-07-18 · Branch: `avalonia-progression-engine-slice` · Host: Linux (.NET 10 `10.0.110`).

## Goal
Second engine-backed screen (real data, no demo): the **Progression** page now shows the REAL training level
(`UserSettings.CurrentDifficulty` + `LevelClassificationSystem`), the REAL recent-session FemVoice score / avg pitch,
and the REAL `ProgressionService.GetProgressionSummary()` from the real DB — the same sources WPF uses.

## What changed
- `ViewModels/ProgressionViewModel.cs` + `Views/ProgressionView.axaml`(+.cs) (new): level (name/emoji/focus),
  FemVoice score (avg `OverallScore` of recent sessions), session count + avg pitch, `ProgressionService` summary +
  recommended difficulty. Fails safe on no-DB/error → "unavailable" state.
- `ShellView.axaml` DataTemplate; `ShellViewModel.ShowProgression` opens the engine-backed VM; **Progression nav is
  now `IsImplemented=true`**.
- Smokes: new `--progression-engine-smoke` (empty + populated DB → level/score/summary, no throw); nav counts updated
  (implemented 7→8, deferred 2→1); `--shell-smoke` + `--smartcoach-progression-ui-scaffold-smoke` updated (both
  Progression + SmartCoach engine-backed + safe when DB-less; only Mikrofonkalibrering remains deferred).

## Note on per-dimension scores
WPF sources the per-dimension resonance/intonation rings from `SessionAnalyticsStore` (a separate live-trend service),
NOT from `TrainingSession`. The DB session round-trip doesn't carry `ResonanceScore`, so this slice shows the
reliably-persisted real values (level, FemVoice score = avg OverallScore, avg pitch, summary). Wiring the
`SessionAnalyticsStore` per-dimension trend is a follow-up.

## Verification
`--progression-engine-smoke`: `levelOk=True('Nybegynner') emptyOk=True dataOk=True avgScore=64 avgPitch=170Hz`.
Offscreen snapshot shows the real level + ProgressionService summary. Gate: build 0 err, **45/45 smokes**, portable
**1570/1580**. No clinical logic changed.
