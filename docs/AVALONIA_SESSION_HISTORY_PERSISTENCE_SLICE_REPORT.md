# Avalonia — Session-history persistence (local, display-only) — Slice Report

Date: 2026-07-18 · Branch: `avalonia-session-history-persistence-slice` · Host: Linux (.NET 10 `10.0.110`).

## Goal
First step of **functional parity**: real persistence so completed sessions save and show. Safe foundation only —
Avalonia-LOCAL JSON, display-only, **no WPF SQLite DB, no clinical scoring, no engine wiring** (mirrors the merged
Stage-8z UI-prefs pattern).

## What changed
- `FemVoice.Avalonia.UI/History/SessionRecord.cs` (new) — display-only record (when / source / duration / note);
  no clinical score.
- `FemVoice.Avalonia.UI/History/SessionHistoryStore.cs` (new) — JSON list at
  `<ApplicationData>/FemVoiceAvalonia/session-history.json` (distinct from WPF/DB). Load/Recent/Append/Clear;
  graceful on missing/empty/corrupt; capped at 200; never throws.
- `MainDashboardViewModel` — records a session on **Stop** (Source="Dashbord", duration, "kun visning" note; skips
  <2 s), exposes `RecentSessions` (newest-first) + `HasRecentSessions`; refresh marshalled to the UI thread. A
  test/opt-in ctor accepts an injected store so smokes never touch the real file.
- `DashboardView.axaml` — a read-only "Siste økter (lokalt)" card (shown when history exists).
- `Program.cs` — new `--session-history-persistence-smoke` (42nd): empty/round-trip/newest-first/display/corrupt-
  safe/clear/Avalonia-local-default-path.

## Verification
Gate: build 0 err, **42/42 smokes**, portable **1570/1580** (baseline). Store is deterministic (smoke uses a temp
path). No WPF DB / clinical / Core change.

## Next (functional parity)
Wire the read-only Core services into the scaffolds (SmartCoach/Progression/Analysis/Reports), and record exercise
sessions too — each careful, one screen at a time, presenting the frozen clinical logic without changing it.
