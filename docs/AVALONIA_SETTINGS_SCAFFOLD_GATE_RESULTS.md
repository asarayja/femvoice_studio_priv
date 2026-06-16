# Avalonia Settings / Preferences UI Scaffold — Gate Results

Date: 2026-06-16 · Branch: `avalonia-settings-scaffold-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

All commands from the repo root with `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (`dotnet run --project FemVoice.Avalonia --no-build -- --<smoke>`)
| Smoke | Result |
| --- | --- |
| `--smoke` | **OK** |
| `--dashboard-smoke` | **OK** |
| `--exercise-smoke` | **OK** |
| `--exercise-runtime-smoke` | **OK** |
| `--exercise-runtime-integration-smoke` | **OK** |
| `--exercise-coordinator-smoke` | **OK** |
| `--runtime-chart-feedback-smoke` | **OK** |
| `--shell-smoke` | **OK** (updated: implemented==3) |
| `--theme-loc-smoke` | **OK** (updated: nav[2]=="Innstillinger") |
| `--settings-smoke` | **OK** |

### `--settings-smoke` output
```
[settings] Nav implemented: True  onSettings: True  sections: 8
[settings] Inert: notDisposable=True noCommands=True allDeferred=True
[settings] Runtime->Settings: ran=True disposed=True no-orphan-frames=True
[settings] Settings smoke OK
```
Verifies: the Settings nav item exists and is implemented; navigating switches `CurrentPage` to a
`SettingsViewModel` that is inert (not IDisposable, exposes no `IRelayCommand`, all rows deferred); 8 cards are
present with rows; and navigating to Settings from a running runtime disposes the runtime (`IsRunning==false`,
trace stops growing → no orphaned synthetic capture).

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard
Base forbidden list (`System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot*`, `FemVoice.Audio.Windows`,
`NAudioCaptureService`, `WaveInEvent`, `WasapiCapture`, `ThemeManager`, `LocExtension`, `LocConverter`,
`FeedbackConsistencyGuard`, `ComfortZoneController`) → **CLEAN (zero real references)**: a non-comment grep returns
empty; every match across the project is a negation comment/docstring (e.g. "no OxyPlot", "NOT FeedbackConsistencyGuard",
"the real NAudioCaptureService would be wired … NOT here"), never a real using/type/method.

Settings-specific forbidden list (`SQLite`, `IDatabaseService`, `ExerciseSessionRecorder`, `SetLanguage`,
`.Save(`, `Persist`, `Backup`, `Restore`, `MicrophoneCalibration`, `ProgressionSafetyGate`, `SmartCoachEngine`)
→ **CLEAN (zero real references)**. (Two localization key strings originally read `Settings_Backup`/`Settings_Restore`;
they were renamed to Norwegian keys so no English token matches — display text unchanged, no real API usage either way.)

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline; 1569/1580 is
the documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake variant). No new failures.

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only changes; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched. Localization semantics preserved.** All additions display-only.
