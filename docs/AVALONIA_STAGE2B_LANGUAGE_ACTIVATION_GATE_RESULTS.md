# Avalonia Stage 2B — Language Activation (LIVE, 20 languages) — Gate Results

Date: 2026-06-18 · Branch: `avalonia-stage2b-language-activation-slice` (off `main` @ `aa35246`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Stage 2B: Avalonia-only runtime language activation with **LIVE in-session switching**; high-visibility UI
> translated for **all 20 languages** (machine-generated, not native-reviewed); **English is the global fallback**.
> No Core SetLanguage, no global thread-culture change, no Core resx edits, no WPF change. Stage-2A theme intact.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (33 — all OK, all exit 0)
- `--settings-language-activation-smoke`: `svApplied=True enApplied=True nbApplied=True allCulturesSwitch=True englishFallback=True startupRead=True missingSafe=True corruptSafe=True unknownSafe=True threadCultureUntouched=True englishOverlay=True norwegianFallback=True liveRefreshSignal=True saveAppliesLive=True`.
- `--settings-theme-activation-smoke` (Stage 2A) OK; `--theme-loc-smoke`, `--localization-text-polish-smoke`, `--avalonia-localization-coverage-smoke`, `--settings-smoke`, `--settings-visual-parity-smoke`, `--settings-persistence-readiness-smoke`, `--shell-smoke` green.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Leak guard clean (no WPF/Windows/DB/clinical/engine refs; no WPF `ThemeManager`/`LocalizationService`/`LocExtension`/`LocConverter`). No global thread-culture change. **Core `Strings.*.resx` git-clean.** Diff scope: only `FemVoice.Avalonia/` + `docs/`.

## Packaging verification
- `publish-linux.sh linux-x64` → OK; published smokes incl. `--settings-language-activation-smoke` OK.
- `package-deb.sh linux-x64` → `.deb` built; `publish-macos.sh osx-x64` → OK; `package-app.sh` → unsigned `.app`; `package-dmg.sh` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures; unchanged — Core untouched). No new failures.

## Manual verification (user-accepted)
Live language switching confirmed in the running Linux GUI: changing the language in Settings re-renders the nav
rail + Settings page immediately (Norwegian → Swedish/German/French/Arabic/…/English and back). Theme (Stage 2A)
still applies live; reduce-motion has no runtime effect; behaviour-heavy sections remain inert.

## Caveat
The 18 non-English / non-Norwegian languages are **machine-generated (model)** translations, **not native-reviewed**
(`ScaffoldTranslations.cs`). They should be reviewed by native speakers before production/clinical release. Deep
deferred-scaffold strings fall back to English until individually translated.
