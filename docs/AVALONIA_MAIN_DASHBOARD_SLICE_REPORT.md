# Avalonia Main Dashboard — Slice Report

Date: 2026-06-16 · Branch: `avalonia-main-dashboard-slice` (off `main` after PR #1 merge).

## 1. Merge status of PR #1
**MERGED.** PR #1 (`linux-portable-core` → `main`) merged via a merge commit (`a95b569`, batch history preserved, not squashed). `main` now contains the portable core, Avalonia shell, and Audio.Windows adapter. Source branch retained.

## 2. Dashboard features implemented
`MainDashboardViewModel` + `MainWindow.axaml` dashboard: Start/Stop session commands; current pitch (Hz); signal status (voiced + confidence); pitch stability; health status; comfort-zone (low/high); feedback message area; difficulty selector (`DifficultyLevel`); synthetic-audio mode selector; live pitch-trace area; navigation + professional-tools placeholders (disabled); FluentTheme skeleton; Norwegian labels.

## 3. Audio mode used
Synthetic (`SyntheticAudioCaptureService`) behind `IAudioCaptureService` — modes StablePitch / UnstablePitch / PitchRampUp / PitchRampDown / Silence. No real mic; no NAudio capture in Avalonia. (Windows production would use `NAudioCaptureService`, not referenced here.)

## 4. Chart status
Converter-free placeholder (bottom-aligned bar trace over recent stabilized pitch). OxyPlot.Avalonia port deferred — see `AVALONIA_CHART_PORT_NOTES.md`.

## 5. Feedback wiring status
Placeholder descriptive derivation from live pitch/stability/health states. Full `FeedbackPipeline`/`FeedbackConsistencyGuard` routing deferred (frozen engine untouched) — see `AVALONIA_DASHBOARD_FEEDBACK_WIRING.md`.

## 6. Theme/localization status
FluentTheme skeleton; `LocalizationService` available via DI (smoke confirms `Common_Yes → "Ja"`). Full theme palette/system-theme port and `{loc:}`-style markup are deferred (Norwegian literals in AXAML for now).

## 7. Linux build result
✅ GREEN — `FemVoice.Audio.Abstractions`, `FemVoice.Core`, `FemVoice.Avalonia` all build (0 errors). `--smoke` and `--dashboard-smoke` pass. See `AVALONIA_MAIN_DASHBOARD_GATE_RESULTS.md`.

## 8. Windows build result (if run)
Not run on this Linux host. WPF + shared projects were green on the earlier Windows CI (PR #1, run 27618290291); the `Windows WPF Verification` workflow re-runs on any PR.

## 9. Tests run
`FemVoice.Tests.Portable`: **1570/1580** (10 pre-existing localization-data failures; no regression from this slice). The dashboard VM itself is verified by `--dashboard-smoke` (it lives in `FemVoice.Avalonia`, outside the portable test project).

## 10. Known gaps
- Chart is a placeholder (no OxyPlot.Avalonia, no time/Hz axes, no shaded comfort band).
- Feedback is a placeholder (no FeedbackPipeline routing).
- Health uses `strainLevel = 0` (no VocalHealthSupervisor wiring); no FemVoiceScore/HydrationAdvisor display.
- Real Linux mic capture not implemented (synthetic only).
- Navigation/professional-tools/settings are disabled placeholders.
- Theme/localization are skeleton/literals.
- Compiled bindings off (reflection bindings) for this slice.
- The 14 pre-existing test failures (10 portable + 4 Windows theme-style) remain — unrelated to this slice.

## 11. Behaviour changes: **NO**
No clinical scoring, SmartCoach, Voice Health, recovery, safety gates, reports, localization semantics, diagnostics, analytics, persistence, or exercise definitions changed. The dashboard reads shared analysis services and renders their output; the only new code is the synthetic-audio modes, the Avalonia VM/view, and DI wiring.

## 12. Recommended next phase
**Avalonia Exercise Guide + Exercise Detail slice** (per the work order). Suggested supporting steps first/alongside: the OxyPlot.Avalonia chart slice and the real FeedbackPipeline wiring slice (both have notes docs). Open a PR for this dashboard slice for review before continuing.
