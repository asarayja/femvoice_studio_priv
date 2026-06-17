# Avalonia 20-Language Scaffold Localization — Gate Results

Date: 2026-06-17 · Branch: `avalonia-20-language-scaffold-localization-slice` (off `main` @ `84e839b`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Localization structure + coverage only — behavior-neutral. Avalonia-owned resources; Core `Strings.*.resx`
> untouched; reuse-only (no machine translations); no runtime language switching/persistence; no WPF/clinical change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (29 — all OK, all exit 0)
`--smoke` … `--localization-text-polish-smoke` (28 prior) + **`--avalonia-localization-coverage-smoke` (new, 29th)** → **29/29 OK.**
- `--avalonia-localization-coverage-smoke` (source): `cultures=20(20=True) trusted=1 documentedFallback=105 broken=0 trustedResolves=True registeredSane=True overlayClean=True noBrokenKeys=True`.
- From the published DLL: source cross-check `skipped` (no source tree) → still passes (overlay/culture/structure checks run).
- `--localization-text-polish-smoke` + `--theme-loc-smoke` remain green.

## Core resx untouched
`git status --porcelain FemVoice.Core/` → **clean** (no Core/Resources edits). Portable **1570/1580** unchanged — the
known localization/mojibake baseline tests are NOT perturbed by this slice.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- No WPF localization dependency (`LocExtension`/`LocConverter`); forbidden WPF/audio/database/clinical tokens (non-comment): **clean**.

## Packaging verification (readiness intact)
- `publish-linux.sh linux-x64` → OK; **15/15 published smokes exit 0** (incl. `--avalonia-localization-coverage`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK; `package-app.sh osx-x64` → unsigned `.app`; `package-dmg.sh osx-x64` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures; **unchanged** —
this slice does not touch Core resx); 1569 acceptable with the intermittent `ComfortZone` flake. No new failures.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Added an Avalonia-owned scaffold-string overlay (20 cultures,
trusted product name + documented 105-key native-translation backlog) + a coverage smoke. No runtime language
switching/persistence, no Core resx change.
