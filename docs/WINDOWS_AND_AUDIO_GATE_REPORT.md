# Windows Baseline + Audio.Windows — Gate Report

Date: 2026-06-16 · Branch: `linux-portable-core` · Host: Linux (Ubuntu 26.04), .NET 10.0.301 (`~/.dotnet`). **Committed and pushed to `origin/linux-portable-core` for review — NOT merged; `main` untouched.**

This gate: committed the portable-core work in reviewable batches, added the Windows NAudio audio adapter behind the abstraction, verified Linux/Avalonia regression, and statically prepared the WPF/Windows verification. The real Windows build/test **cannot run on this Linux host** and is documented as pending (not invented).

## 1. Branch and commit list
Branch `linux-portable-core` (off `main` @ `e9e0091`), 6 reviewable commits:
- `bc89c8a` Extract portable FemVoice.Core + FemVoice.Audio.Abstractions from WPF app
- `92ca285` Add FemVoice.Tests.Portable (net10.0) and move portable tests to it
- `d391844` Add minimal Avalonia Linux shell + platform abstraction interfaces
- `949bd9b` Add audit + migration docs, Linux portable gate script; update solution and feature overview
- `7abf648` Add FemVoice.Audio.Windows: NAudio capture behind IAudioCaptureService
- (docs batch for this gate)

Working tree clean; `bin/obj` ignored. See `COMMIT_BATCH_REVIEW_NOTES.md`.

**Remote / review status:**
- Remote: `origin` = `https://github.com/asarayja/femvoice_studio_priv.git` (private).
- Pushed: `git push -u origin linux-portable-core` succeeded. Local `HEAD` == `origin/linux-portable-core` == `07a542c` (matched). `origin/main` unchanged at `e9e0091`.
- PR: not opened (review branch only). Suggested PR URL: `https://github.com/asarayja/femvoice_studio_priv/pull/new/linux-portable-core`.
- Final commit pushed/tested-on-Linux: `07a542c` (a subsequent docs-only commit may follow on the same review branch).

## 2. What was verified on Windows
**Nothing — no Windows host available.** This is documented honestly in `WINDOWS_BASELINE_TEST_RESULTS.md` with a runbook. What was verified **on Linux** instead: `FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Audio.Windows` (via `EnableWindowsTargeting`), and `FemVoice.Avalonia` all build (0 errors); `FemVoice.Tests.Portable` runs 1570/1580.

## 3. WPF build result
**PENDING (Windows).** Not buildable on Linux (`UseWPF`/`net10.0-windows`). Static review found no blockers: namespaces preserved, RESX RootNamespace preserved, ProjectReferences added, **no WPF use of Core internals** (verified). See `WPF_SHARED_CORE_COMPATIBILITY_NOTES.md`.

## 4. WPF test result
**PENDING (Windows).** The 30 Windows-only tests stay in `FemVoiceStudio.Tests`. The portable safety/clinical suites already pass on Linux against the same `FemVoice.Core`.

## 5. Safety invariant test result
**GREEN on Linux** (executed in `FemVoice.Tests.Portable` against `FemVoice.Core`): SafetyOverrideInvariant, SafetyPriorityEngine, ManualOverrideClamp, FeedbackPriorityMatrix, FeedbackConsistencyGuard, ProgressionSafetyGate, RecoveryAwareTargetZone — all pass. (Windows re-run pending but exercises the identical assembly.)

## 6. WPF compatibility fixes made
Mechanical only (see `WPF_SHARED_CORE_COMPATIBILITY_NOTES.md`): csproj ProjectReferences + RESX block removal; `AppTheme`/`AppSettings`/`DebugSettings`/`AppSettingsJson` and `ResonanceCategory` relocated into Core (same namespaces); `App.xaml.cs` audio DI registration + usings; `AudioCaptureService` moved to `FemVoice.Audio.Windows` (namespace unchanged). No behaviour change; no UI rewrite; no dead code revived.

## 7. Audio.Windows adapter status
**DONE + Linux-compile-verified (0/0).** `FemVoice.Audio.Windows` (`net10.0-windows`) holds the moved `AudioCaptureService` + `NAudioCaptureService : IAudioCaptureService`, a thin adapter delegating all capture behaviour (noise gate, high-pass, watchdog, device-loss, calibration, device enumeration) unchanged. WPF references it and registers `IAudioCaptureService → NAudioCaptureService` (additive). **Manual Windows mic smoke test is PENDING** (no hardware here) — see `AUDIO_WINDOWS_ADAPTER_NOTES.md`.

## 8. Linux Avalonia regression result
**GREEN.** Avalonia + portable core build; portable tests still 1570/1580; headless smoke OK. Avalonia references **only** Core + Abstractions — **no** `FemVoice.Audio.Windows`/NAudio-capture/`System.Windows`/`Microsoft.Win32`/`OxyPlot.Wpf` leak. See `AVALONIA_LINUX_GATE_RESULTS.md`.

## 9. Known issues
- WPF build/test + manual Windows mic test are **unverified** (no Windows host) — the only open verification gap.
- 10 pre-existing portable-test failures (localization-data: `Report_RecommendationHighFatigueFormat` placeholders; stale `All12Resx` count) — not port regressions; not fixed (hard rule).
- 1 intermittent timing flake (`ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate`, ~1 run in 4) — pre-existing.
- Transitive `NU1903` advisory (`Tmds.DBus.Protocol` via Avalonia Linux backend).
- The three referenced docs `AVALONIA_LINUX_FIRST_UI_SLICE_REPORT.md`, `AVALONIA_MAIN_DASHBOARD_SLICE_REPORT.md`, `AVALONIA_MAIN_DASHBOARD_GATE_RESULTS.md` **do not exist** — no main-dashboard slice was built (only a minimal shell). Reported, not invented.

## 10. Behaviour changes: **NO**
No clinical/scoring/SmartCoach/health/recovery/safety-gate/progression/report/localization-semantics/diagnostics/analytics/persistence/exercise-catalog behaviour changed. All changes were relocations, additive wiring, the audio adapter (pure delegation), commits, and docs.

## 11. Recommended next phase
1. **On Windows (closes the only gap):** run `dotnet build`/`dotnet test FemVoiceStudio.slnx`; fill `WINDOWS_BASELINE_TEST_RESULTS.md`; confirm WPF compiles against the shared core and the 30 Windows-only tests pass; do the manual mic smoke for `NAudioCaptureService`.
2. **Then** (per stop condition, after review): begin the **Avalonia Main Dashboard parity slice** (it is currently only a bootstrap shell — no dashboard exists yet), then the **Exercise Guide + Exercise Detail** slice.

## Recommendation: **DO NOT MERGE — continue review**
The review branch is pushed and Linux-verified, but merging is **blocked** until the Windows gap closes: (a) `dotnet build`/`test FemVoiceStudio.slnx` green on a Windows host (WPF compiles against the shared core; 30 Windows-only tests pass), and (b) the manual Windows mic smoke for `NAudioCaptureService` passes. Until both are recorded in `WINDOWS_BASELINE_TEST_RESULTS.md` / `AUDIO_WINDOWS_ADAPTER_NOTES.md`, keep the branch in review. No behaviour change has been introduced, so the risk is purely "does WPF still compile/run on Windows" — which only a Windows build can confirm.

## Stop condition
Stopping here per the work order. Do not proceed to full Avalonia UI parity until this gate is reviewed and the Windows baseline (step 1) is confirmed.
