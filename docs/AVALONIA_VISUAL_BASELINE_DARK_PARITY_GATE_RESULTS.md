# Avalonia Visual Baseline / Dark Theme Parity — Gate Results

Date: 2026-06-17 · Branch: `avalonia-visual-baseline-dark-parity-slice` (off `main` @ `d04e823`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (17 — all OK, all exit 0)
`--smoke`, `--dashboard-smoke`, `--exercise-smoke`, `--exercise-runtime-smoke`, `--exercise-runtime-integration-smoke`,
`--exercise-coordinator-smoke`, `--runtime-chart-feedback-smoke`, `--shell-smoke`, `--theme-loc-smoke`,
`--settings-smoke`, `--runtime-lifecycle-smoke`, `--analysis-scaffold-smoke`, `--reports-scaffold-smoke`,
`--diagnostics-scaffold-smoke`, `--packaging-smoke`, `--packaged-theme-smoke`, **`--visual-baseline-smoke` (new)** → **17/17 OK.**

- `--visual-baseline-smoke`: `nav implemented=6 deferred=3 ok=True`; `Settings inert=True`; `runtime: dark-first(RequestedThemeVariant=Dark)=True palette=12-brushes-resolve=True actualVariant='Dark'`; `source theme-usage check: OK`.
- `--packaged-theme-smoke`: `shellKeys=28×(Dark+Light) keysResolve=True variant='Dark'`; AXAML cross-check OK.

## Crash robustness (NVIDIA GL atexit teardown)
The three platform-initializing smokes intermittently segfaulted on exit (~1/12, SIGSEGV/139) inside the NVIDIA GL
`atexit` teardown — *after* producing the correct result. Fixed via POSIX `_exit()` after the smoke result
(Linux-only; skips `atexit`). **Stress test: 90 runs (30× each of `--theme-loc-smoke`/`--packaged-theme-smoke`/
`--visual-baseline-smoke`) → 0 non-zero exits, 0 new coredumps.**

## Vulnerable packages
`dotnet list … --vulnerable --include-transitive` → **none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Leak guard
- Base/forbidden tokens (non-comment) in `FemVoice.Avalonia` .cs/.axaml/.csproj → **clean**.
- No theme/settings persistence introduced (`Save`/`Persist`/`WriteSettings`): only match is the **pre-existing**
  `NotSavedNote` display-label binding (the "not saved" disclaimer; a `Save` substring, not a persistence API).
- New brush/style/theme names (`Shell*`, `Button.primary`, `Border.card`, etc.) are visual resource names — allowed.

## Project constraints
Refs only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; no Windows-only deps; `Tmds.DBus.Protocol` 0.21.3.

## Publish / package verification (published output reflects the fix)
- `publish-linux.sh linux-x64` → OK; published `--theme-loc-smoke`/`--packaged-theme-smoke`/`--visual-baseline-smoke`/`--smoke` → OK (dark-first, 28 keys resolve).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` clean (root:root).
- `publish-macos.sh osx-x64` → OK.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed 1570, Failed 10, Total 1580** (known localization-data baseline;
1569/1580 acceptable when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake
fires). No new failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched. No UI redesign.** Visual theming/layout + a read-only smoke +
a Linux-only smoke-exit hardening + docs. Default build/run unchanged. Signing/notarization **not started**.

---

## Follow-up polish gate (same PR #19): Exercise Guide row-click + Dashboard chart parity

Date: 2026-06-17 · same branch.

- **Build**: 0 warnings / 0 errors.
- **Smokes**: **18/18 OK, all exit 0** (added `--visual-interaction-chart-smoke`): guide exercises=15, cardOpensDetail=True, detailMatches=True; chart heightPx=200, comfort band ~117px, axis ~135–255 Hz, geometryOk=True; no-charting-lib-dependency=True; chart brushes resolve (Dark); source check OK. No regression in `--dashboard-smoke`/`--exercise-smoke`/`--runtime-chart-feedback-smoke`/`--visual-baseline-smoke`.
- **Vulnerable packages**: none; `Tmds.DBus.Protocol` 0.21.3; refs Core + Audio.Abstractions only.
- **Leak guard**: clean. No OxyPlot/charting dependency — csproj has no charting `PackageReference`; the smoke's "no charting lib" check + the doc comment avoid embedding a forbidden literal.
- **Publish/package**: `publish-linux.sh` OK; published `--theme-loc`/`--packaged-theme`/`--visual-baseline`/`--visual-interaction-chart` smokes all exit 0; `.deb` builds; `publish-macos.sh osx-x64` OK.
- **Portable**: 1570/1580 (known baseline; no new failures — no test-compiled code changed).

### Interaction change
Exercise Guide rows are clickable `Button.guideCard` bound to the existing `OpenExerciseCommand` (same path as before); keyboard-focusable; hover/pressed/cursor states. Non-interactive "Åpne ›" chevron affordance retained.

### Chart change
Dashboard chart now uses the shared converter-free `RuntimeChartDisplay` geometry: windowed axis (comfort zone ± padding via portable `PitchChartAxisRangeCalculator`), green comfort band, subtle grid lines, current-pitch marker, y-axis Hz labels, scaled trace, centered empty-state. Display-only `DashboardChart` + `PitchTracePx` added to the dashboard VM; **data pipeline / pitch detection / target profiles unchanged**.

### Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Visual/UX + display-only chart geometry + a read-only smoke. Signing/notarization **not started**.
