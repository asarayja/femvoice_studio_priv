# Avalonia Visual Layout Polish — Gate Results

Date: 2026-06-17 · Branch: `avalonia-visual-layout-polish-slice` (off `main` @ `31aa0c9`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Visual/layout only — behavior-neutral. No persistence/DB/analytics; no settings/SmartCoach/progression behaviour;
> no clinical/domain or WPF change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (27 — all OK, all exit 0)
`--smoke` … `--settings-visual-parity-smoke` (26 prior) + **`--visual-layout-polish-smoke` (new, 27th)** → **27/27 OK.**
- `--visual-layout-polish-smoke` (source present): `settingsResponsive=True scaffoldsCentered=True guideCentered=True settingsInert=True scaffoldsDeferred=True guideFilterIntact=True&searchWorks=True dashboardChartIntact=True navIntact=True`.
- From the published DLL: `source=skipped` (XAML checks skip→pass) and the VM checks still run and pass.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Forbidden WPF/audio/database/clinical/engine tokens (non-comment): **clean**.

## Packaging verification (readiness intact)
- `publish-linux.sh linux-x64` → OK; **13/13 published smokes exit 0** (incl. `--visual-layout-polish`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK; `package-app.sh osx-x64` → unsigned `.app`; `package-dmg.sh osx-x64` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures); **1569/1580**
acceptable when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake fires. No new
failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** XAML container/alignment/width/`ItemsPanel` edits across
Settings + SmartCoach/Progression/Analysis/Reports/Diagnostics scaffolds + Exercise Guide (centered columns;
Settings 2-column WrapPanel). Dashboard/runtime/shell unchanged. No persistence, no behaviour enabled.
