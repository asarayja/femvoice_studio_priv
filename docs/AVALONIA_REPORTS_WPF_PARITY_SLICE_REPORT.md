# Avalonia — Reports view WPF structural parity — Slice Report

Date: 2026-07-17 · Branch: `avalonia-reports-wpf-parity-slice` · Host: Linux (.NET 10 `10.0.110`).

## Goal
Bring the Avalonia Reports/professional-tools page's layout in line with a WPF-style hub (header band + 2-column
card grid with status chips), staying DISPLAY-ONLY. View-only change; `ReportsViewModel` untouched.

## What changed
- `FemVoice.Avalonia/Views/ReportsView.axaml` rebuilt from a single-column list to: header **card band**
  (heading + subtitle); deferred banner; disabled global actions; **2-column card grid** (`UniformGrid Columns=2`)
  bound to the VM's `Cards`, each with a title (ellipsis), a **status chip** (`ShellChipBackgroundBrush`),
  description, and a disabled per-card action. Kept `HorizontalAlignment="Center"`.

No report generation, no file dialogs, no database/history, no clinical calc (all deferred, Phase 11).

## Verification
Offscreen Reports screenshot confirms the WPF-hub layout. Gate: build 0 err, **41/41 smokes**
(`--reports-scaffold-smoke` + `--visual-layout-polish-smoke` + `--packaged-theme-smoke` green), portable 1570/1580.

## Deferred (Phase 11)
Real report generation/export (4 types × 3 formats), clinician/coach/case-review behaviour, calendar/history.
