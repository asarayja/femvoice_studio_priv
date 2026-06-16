# Avalonia Analysis / Resonance Charts Scaffold — Gate Results

Date: 2026-06-17 · Branch: `avalonia-analysis-resonance-scaffold-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

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
| `--shell-smoke` | **OK** (updated: 4 implemented / 5 deferred) |
| `--theme-loc-smoke` | **OK** |
| `--settings-smoke` | **OK** |
| `--runtime-lifecycle-smoke` | **OK** |
| `--analysis-scaffold-smoke` | **OK** |

### `--analysis-scaffold-smoke` output
```
[analysis] nav-implemented=True onAnalysis=True series=4 summary=4
[analysis] inert: notDisposable=True noCommands=True seriesOk=True summaryOk=True
[analysis] Runtime->Analysis: ran=True disposed=True no-orphan-frames=True
[analysis] Analysis scaffold smoke OK
```
Verifies: the Analysis nav item exists and is implemented; navigating switches `CurrentPage` to an
`AnalysisViewModel` that is inert (not IDisposable, no `IRelayCommand`); 4 synthetic series (each with bars) +
4 summary placeholders present; and navigating to Analysis from a running runtime disposes the runtime
(`IsRunning==false`, trace stops growing → no orphaned synthetic capture).

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard
Base forbidden list → **CLEAN (zero real references)** (only the two pre-existing negation comments match & are excluded).

Analysis-specific list (`SQLite`, `IDatabaseService`, `ExerciseSessionRecorder`, `SessionAnalyticsStore`, `.Save(`,
`Persist`, `ReportExport`, `SmartCoachEngine`, `ProgressionSafetyGate`, `VoiceHealth`, `VocalHealthSupervisor`,
`RecoveryScorer`, `RecoveryIntelligenceService`, `MicrophoneCalibration`, `NAudio`, `Wasapi`, `WaveIn`, `OxyPlot`)
→ **CLEAN (zero real references)**. (The new AnalysisView + AnalysisViewModel comments are worded to avoid the
literal `OxyPlot`/`Persist` tokens; no real charting or persistence dependency is introduced. Any base-list
matches elsewhere are pre-existing negation comments already tolerated by the guard.)

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline; 1569/1580 is
the documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake variant). No new failures.

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only changes; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** All additions display-only over synthetic sample data.
