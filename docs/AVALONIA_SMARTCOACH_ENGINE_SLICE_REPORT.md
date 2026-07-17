# Avalonia — SmartCoach engine-backed (real engine, real DB) — Slice Report

Date: 2026-07-18 · Branch: `avalonia-smartcoach-engine-slice` · Host: Linux (.NET 10 `10.0.110`).

## Goal
Per user direction (real data like WPF, no demo data, one thing at a time): make the **SmartCoach** screen truly
engine-backed — run the real Core `SmartCoachEngine` (read-only) on the real `DatabaseService` to produce the daily
recommendation / weekly target / status. First engine-backed screen; sets the pattern for Progression/Analysis/Reports.

## What changed
- `ViewModels/SmartCoachViewModel.cs` (new): constructs `new SmartCoachEngine(db, loc)` and surfaces
  `GenerateDailyRecommendation` (focus + text + duration + health warning), `GetWeeklySessionTarget`,
  `GetStatusSummary`. Fails safe on no-DB/engine-error → truthful "unavailable" state (no crash, no DB opened).
- `Views/SmartCoachView.axaml`(+.cs) (new): recommendation card (focus/duration chips + text), weekly-target/status
  card, health-warning card (safety first), or the unavailable note.
- `ShellView.axaml`: DataTemplate for `SmartCoachViewModel`.
- `ShellViewModel`: gains an optional `IDatabaseService` (injected by DI in production; null in headless/tests →
  safe fallback). **SmartCoach nav is now `IsImplemented=true`**; `ShowSmartCoach` opens a fresh engine-backed VM.
- Smokes: new `--smartcoach-engine-smoke` (empty DB → new-user rec; with sessions → real focus/text/duration);
  nav-count assertions updated (implemented 6→7, deferred 3→2); `--shell-smoke` +
  `--smartcoach-progression-ui-scaffold-smoke` updated (SmartCoach engine-backed + safe when DB-less; Progression
  still a deferred scaffold).

## Verification
`--smartcoach-engine-smoke`: `emptyOk=True dataOk=True focus='resonance' dur=8min weekly=3`. Offscreen snapshot
(DI shell → real DB) shows the real recommendation. Gate: build 0 err, **44/44 smokes**, portable **1570/1580**.
No clinical logic changed — the engine is used exactly as WPF uses it.
