# Avalonia Settings Persistence Readiness — Gate Results

Date: 2026-06-17 · Branch: `avalonia-settings-persistence-readiness-slice` (off `main` @ `639e9a2`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Audit/readiness only — NO persistence, NO controls enabled, NO behavior change. Only production change is the
> guardrail smoke; `SettingsViewModel`/`SettingsView` + Core + WPF untouched.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (30 — all OK, all exit 0)
`--smoke` … `--avalonia-localization-coverage-smoke` (29 prior) + **`--settings-persistence-readiness-smoke` (new, 30th)** → **30/30 OK.**
- `--settings-persistence-readiness-smoke` (source): `notDisposable=True noCommands=True noServiceDeps=True noServiceFields=True allDeferred=True noPersistenceRefs=True`. From the published DLL: source check `skipped` → still passes (reflection checks run).

## Production code surface
Only `FemVoice.Avalonia/Program.cs` changed (the new guardrail smoke). `SettingsViewModel.cs` / `SettingsView.axaml`
unchanged; `FemVoice.Core/` untouched (git-clean). No controls enabled, no services/commands added, VM not IDisposable.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- No WPF settings/localization dependency; no `IDatabaseService`/`ThemeManager`/`MicrophoneCalibration`/persistence
  references; forbidden tokens (non-comment) **clean** (the readiness smoke detects WPF hooks via non-forbidden
  substrings/invocation patterns, so it does not trip the guard itself).

## Packaging verification (readiness intact)
- `publish-linux.sh linux-x64` → OK; **16/16 published smokes exit 0** (incl. `--settings-persistence-readiness`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb`; `publish-macos.sh osx-x64` → OK; `package-app.sh` → unsigned `.app`; `package-dmg.sh` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures; unchanged). No new failures.

## Behaviour change
**None.** Added a docs audit + a behavior-neutral guardrail smoke. No settings persistence, no runtime
language/theme/audio behaviour, no DB/privacy actions, no clinical/domain or WPF change.
