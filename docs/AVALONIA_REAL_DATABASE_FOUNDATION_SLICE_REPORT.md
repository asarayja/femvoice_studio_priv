# Avalonia — Real SQLite database foundation — Slice Report

Date: 2026-07-18 · Branch: `avalonia-real-database-foundation-slice` · Host: Linux (.NET 10 `10.0.110`).

## Goal
Per user direction (real data like WPF, **no demo data**, one thing at a time): wire Core's real
`DatabaseService` (SQLite) into the Avalonia head so engines can run on REAL data — the foundation for
SmartCoach/Progression/Analysis/Reports parity.

## What changed
- `FemVoice.Avalonia.UI/AppServices.cs`: registers `DatabaseService` + `IDatabaseService` (lazy singleton). Uses
  Core's default path `<MyDocuments>/FemVoiceStudio/femvoice.db` — the SAME store the WPF app uses (shared real data
  on Windows; a fresh SQLite DB on Linux/macOS via SQLitePCLRaw). Schema is created idempotently
  (`CREATE TABLE IF NOT EXISTS`); no clinical logic changed.
- `FemVoice.Avalonia/Program.cs`: new `--database-service-smoke` (43rd) — on Linux it creates the schema, reads
  seeded `UserSettings`, and round-trips a real `TrainingSession` (save → `GetRecentSessions`), using a unique test
  DB file it deletes afterward (incl. WAL/SHM).

## Verification
`--database-service-smoke`: `created=True settingsOk=True readOk=True saveOk=True(id=1) roundTrip=True` on
`~/Documents/FemVoiceStudio/…`. Gate: build 0 err, **43/43 smokes**, portable **1570/1580**. The DB is a lazy
singleton, so the headless smokes that don't need it never touch the real file.

## Next (one thing at a time)
Switch dashboard/exercise session persistence to real `TrainingSession`s in this DB (replacing the interim local
JSON), then wire `SmartCoachEngine` (needs `IDatabaseService`) → real daily recommendation, then Progression /
Analysis-with-data / Reports-generation.
