# Avalonia Main Dashboard — Slice Report

Date: 2026-06-16 · Branch: `avalonia-main-dashboard-slice` (synced with `main` incl. PR #1 + PR #2).

## 1. PR #2 merge status
**MERGED.** PR #2 (NU1903 / Tmds.DBus.Protocol 0.21.3 pin) merged into `main` via merge commit `aeb7ae4`. (PR #1 — portable core + Avalonia shell + Audio.Windows — previously merged at `a95b569`.) `main` is the integration line; both merges used merge commits (batch history preserved).

## 2. Dashboard branch sync status
**SYNCED.** `git merge origin/main` into `avalonia-main-dashboard-slice` → merge commit `eee3dea`, **clean auto-merge** (the `FemVoice.Avalonia.csproj` edits from each side were in non-overlapping regions). The branch now contains both the dashboard slice and the NU1903 fix.

## 3. Final commit
`eee3dea` (merge of `origin/main` into `avalonia-main-dashboard-slice`) + this docs refresh.

## 4. Dashboard features implemented
`MainDashboardViewModel` + `MainWindow.axaml`: Start/Stop commands; current pitch (Hz); signal status (voiced + confidence); pitch stability; health status; comfort-zone (low/high); feedback message area; difficulty selector (`DifficultyLevel`); synthetic-audio mode selector; live pitch-trace area; navigation + professional-tools placeholders (disabled); FluentTheme skeleton; Norwegian labels.

## 5. Audio mode used
Synthetic (`SyntheticAudioCaptureService`) behind `IAudioCaptureService`; modes StablePitch / UnstablePitch / PitchRampUp / PitchRampDown / Silence. No real mic, no NAudio capture in Avalonia.

## 6. Chart status
Converter-free placeholder (bottom-aligned bar trace over recent stabilized pitch). OxyPlot.Avalonia port deferred — `AVALONIA_CHART_PORT_NOTES.md`.

## 7. Feedback wiring status
Placeholder descriptive derivation from live pitch/stability/health states. Full `FeedbackPipeline`/`FeedbackConsistencyGuard` routing deferred (frozen engine untouched) — `AVALONIA_DASHBOARD_FEEDBACK_WIRING.md`.

## 8. Theme/localization status
FluentTheme skeleton; `LocalizationService` available via DI (`Common_Yes → "Ja"`). Full theme palette/system-theme port and `{loc:}`-style markup deferred (Norwegian literals in AXAML for now).

## 9. Dependency security status
**NU1903 / GHSA-xrw6-gwf8-vvr9 resolved.** `Tmds.DBus.Protocol` pinned to **0.21.3** (direct ref on the Avalonia head). `dotnet list --vulnerable --include-transitive` → **no vulnerable packages**; build emits **0 warnings**. See `DEPENDENCY_SECURITY_NOTES.md`.

## 10. Linux build result
✅ GREEN — `FemVoice.Audio.Abstractions`, `FemVoice.Core`, `FemVoice.Avalonia` build (0 warnings, 0 errors).

## 11. Avalonia smoke result
✅ `--smoke` OK; `--dashboard-smoke` OK — VM drives real pitch/stability/health from synthetic audio (StablePitch → 200.0 Hz / "Veldig stabil"; Silence → "Ingen stemme"; start/stop clean).

## 12. Vulnerability scan result
✅ `dotnet list FemVoice.Avalonia package --vulnerable --include-transitive` → "has no vulnerable packages".

## 13. Portable test result
**1570/1580** (10 pre-existing localization-data failures; no regression from this slice or the dependency fix).

## 14. Windows build/test result, if run
Not run on this Linux host in this step. WPF + shared projects were green on Windows CI for PR #1 (run 27618290291) and PR #2 (run 27620976282). The `Windows WPF Verification` workflow re-runs on any PR for this branch.

## 15. Known gaps
Chart placeholder (no OxyPlot.Avalonia/axes/shaded band); feedback placeholder (no FeedbackPipeline routing); health uses `strainLevel=0` (no VocalHealthSupervisor); no FemVoiceScore/HydrationAdvisor display; no real Linux mic capture; nav/pro-tools/settings disabled placeholders; theme/localization skeleton/literals; compiled bindings off; the 14 pre-existing test failures (10 portable + 4 Windows theme-style) remain. All documented in `AVALONIA_MAIN_DASHBOARD_PLACEHOLDERS.md`.

## 16. Behaviour changes: **NO**
No clinical scoring, SmartCoach, Voice Health, recovery, safety gates, reports, localization semantics, diagnostics, analytics, persistence, or exercise definitions changed. New code: synthetic-audio modes, the Avalonia VM/view, DI wiring, and the NU1903 dependency pin (build-only).

## 17. Recommended next phase
**Avalonia Exercise Guide + Exercise Detail slice** (per the work order — not started in this prompt). Supporting slices that pair well: OxyPlot.Avalonia chart, and the real FeedbackPipeline wiring (both have notes docs).

## Acceptance checkpoint — met
PR #2 merged to main ✓ · dashboard branch synced + includes NU1903 fix ✓ · no NU1903 warning ✓ · dashboard layout real ✓ · start/stop works with synthetic audio ✓ · pitch/signal updates ✓ · feedback/status updates (placeholder documented) ✓ · theme/localization basics work ✓ · Linux build green ✓ · smoke OK ✓ · WPF unaffected (no Avalonia/Tmds ref) ✓ · no Windows-only leak ✓ · no clinical/domain behaviour changed ✓.
