# Avalonia — Analysis view WPF structural parity — Slice Report

Date: 2026-07-17 · Branch: `avalonia-analysis-wpf-parity-slice` (off `main` @ `<post-#42>`) · Host: Linux (.NET 10 `10.0.110`).

## Goal

Bring the Avalonia Analysis page's **layout structure** in line with the WPF `AnalysisWindow` design (a core goal:
"design the same as the WPF version"), while staying **display-only** over synthetic data. View-only change.

## WPF source-of-truth (structure)

`FemVoiceStudio/Views/AnalysisWindow.xaml`: header band (heading + subtitle) → a **top stats row of 4 stat cards**
(Total Score / Average Pitch / Pitch Stability / Session Duration — big value + small label) → a **2-column grid of
chart cards** (Resonance / Pitch / Prosody / Health, each a titled 200px plot). Uses OxyPlot + real session data.

## What changed (files)

- **`FemVoice.Avalonia/Views/AnalysisView.axaml`** rebuilt from a single-column scaffold to the WPF structure:
  - Header **card band**: heading (`Title`) + subtitle.
  - **4-card stats row** (`UniformGrid Rows=1`) bound to the VM's existing `SummaryMetrics` (big value + label),
    mirroring WPF's top stats row.
  - **2-column chart-card grid** (`UniformGrid Columns=2`) bound to the VM's `Series`, each a titled card with the
    converter-free mini bar-chart + summary — mirroring WPF's 2×2 chart layout.
  - Kept the sample-data banner, deferred import/export buttons, and `HorizontalAlignment="Center"`.

No view-model / data change: `AnalysisViewModel` is untouched (still static, synthetic, inert). No OxyPlot, no
history/database, no export, no clinical scoring.

## Verification

- Offscreen Analysis screenshot confirms the WPF-style layout (header · 4 stat cards · 2-col chart grid).
- Gate: build 0 err, **41/41 smokes** (`--analysis-scaffold-smoke` + `--visual-layout-polish-smoke` green — Analysis
  stays inert + centered), portable **1570/1580** (baseline).

## Deferred (Phase 10)

Real analysis-engine data (session history), OxyPlot.Avalonia (or richer native charts), resonance scatter/timeline,
and the health/prosody indicators remain deferred — this slice is **structural design parity only**.
