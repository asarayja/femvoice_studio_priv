# Avalonia — Diagnostics view WPF structural parity — Slice Report

Date: 2026-07-17 · Branch: `avalonia-diagnostics-wpf-parity-slice` · Host: Linux (.NET 10 `10.0.110`).

## Goal
Bring the Avalonia Diagnostics/export page's layout in line with a WPF-style hub (header band + 2-column card grid
+ status chips), DISPLAY-ONLY. View-only change; `DiagnosticsViewModel` untouched.

## What changed
- `FemVoice.Avalonia/Views/DiagnosticsView.axaml`: single-column list -> header card band (heading+subtitle) +
  deferred banner + disabled global-action WrapPanel + 2-column card grid (`UniformGrid Columns=2`) from `Cards`,
  each with title (ellipsis) + status chip (`ShellChipBackgroundBrush`) + description + disabled per-card action.
  Kept `HorizontalAlignment="Center"`.

No support-package/export/backup-restore, no database/history, no RC-0/research-anonymization change (deferred, Phase 12).

## Verification
Offscreen Diagnostics screenshot confirms the WPF-hub layout. Gate: build 0 err, 41/41 smokes
(`--diagnostics-scaffold-smoke` + `--visual-layout-polish-smoke` + `--packaged-theme-smoke` green), portable 1570/1580.
