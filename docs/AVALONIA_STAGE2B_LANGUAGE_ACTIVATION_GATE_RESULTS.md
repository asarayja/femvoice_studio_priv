# Avalonia Stage 2B — Language Activation — Gate Results

Date: 2026-06-17 · Branch: `avalonia-stage2b-language-activation-slice` (off `main` @ `aa35246`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Stage 2B: Avalonia-only **startup** language activation via an Avalonia-LOCAL culture + Avalonia-owned
> ResourceManager. Language applies at startup (truthful copy — applied live only on restart, not for already-
> rendered views). No Core SetLanguage, no global thread-culture change, no native translations, no reduce-motion
> activation. Stage-2A theme activation (live) intact.
>
> **PR #32 manual-fix:** root cause = VMs resolve once at construction (no live refresh, issue D) + Save status
> overpromised live activation (issue E). Fix = startup-only with truthful copy; the smoke now enforces it.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (33 — all OK, all exit 0)
32 prior + **`--settings-language-activation-smoke` (new, 33rd)** → **33/33 OK.**
- `--settings-language-activation-smoke`: `svApplied=True enApplied=True nbApplied=True scaffoldFallsBack=True startupRead=True missingSafe=True corruptSafe=True unknownSafe=True threadCultureUntouched=True capturedUnchanged=True saveDoesNotSwitchLive=True truthfulStatus=True` (the last three from the PR #32 manual-fix — prove live-needs-restart semantics, Save-persists-but-doesn't-switch-live, and truthful Save copy).
- **Startup-only confirmed:** en-US startup activation flips Core-backed strings (Settings/Theme/Language); nb-NO default works; sv-SE sparse → safe Norwegian fallback; Save persists language but does NOT switch the running resolver (applies on restart). No live-refresh claim.
- `--settings-persistence-readiness-smoke` (updated): `notDisposable=True sectionsInert=True scanned=True noWpfHooks=True noGlobalCulture=True`.
- `--settings-theme-activation-smoke` (Stage 2A) still OK; `--theme-loc-smoke` / `--localization-text-polish-smoke` / `--avalonia-localization-coverage-smoke` / `--settings-smoke` / `--settings-visual-parity-smoke` green.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Leak guard clean (no WPF/Windows/DB/clinical/engine refs; no WPF `ThemeManager`/`LocalizationService`/`LocExtension`/`LocConverter`).
- No global thread-culture change / Core `SetLanguage` (verified by the readiness smoke `noGlobalCulture=True` and the language smoke `threadCultureUntouched=True`). **Core `Strings.*.resx` git-clean.** Diff scope: only `FemVoice.Avalonia/` + `docs/`.

## Packaging verification
- `publish-linux.sh linux-x64` → OK; **19/19 published smokes exit 0** (incl. `--settings-language-activation-smoke`, which resolves the embedded shared resources from the published DLL).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb`; `publish-macos.sh osx-x64` → OK; `package-app.sh` → unsigned `.app`; `package-dmg.sh` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures; unchanged — Core untouched). No new failures.

## Behaviour change
Avalonia-only runtime language activation (startup + on Save) via an Avalonia-local culture; Core-backed Avalonia
text follows the selected language, scaffold-only keys fall back to Norwegian (no native parity). Reduce-motion
remains persisted-only; Stage-2A theme activation intact. No DB/UserSettings/WPF ThemeManager/LocalizationService/
SetLanguage; no global thread-culture change; no audio/clinical/domain/Core/WPF change. Behaviour-heavy Settings
sections remain inert.
