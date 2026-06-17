# Avalonia Diagnostics / Export / Backup Read-only Scaffold — Gate Results

Date: 2026-06-17 · Branch: `avalonia-diagnostics-export-scaffold-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

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
| `--shell-smoke` | **OK** (updated: 6 implemented / 3 deferred) |
| `--theme-loc-smoke` | **OK** |
| `--settings-smoke` | **OK** |
| `--runtime-lifecycle-smoke` | **OK** |
| `--analysis-scaffold-smoke` | **OK** |
| `--reports-scaffold-smoke` | **OK** |
| `--diagnostics-scaffold-smoke` | **OK** |

### `--diagnostics-scaffold-smoke` output
```
[diag] nav-implemented=True onDiagnostics=True cards=8
[diag] inert: notDisposable=True noCommands=True cardsOk=True allDeferred=True
[diag] Runtime->Diagnostics: ran=True disposed=True no-orphan-frames=True
[diag] Diagnostics scaffold smoke OK
```
Verifies: the Diagnostics nav item exists and is implemented; navigating switches `CurrentPage` to a
`DiagnosticsViewModel` that is inert (not IDisposable, no `IRelayCommand`); 8 placeholder cards all deferred
(`AllActionsDeferred`); and navigating to Diagnostics from a running runtime disposes the runtime
(`IsRunning==false`, trace stops growing → no orphaned synthetic capture).

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard
Base forbidden list → **CLEAN (zero real references)** (only the two pre-existing negation comments match & are excluded).

Diagnostics-specific list (`SQLite`, `IDatabaseService`, `ExerciseSessionRecorder`, `SessionAnalyticsStore`, `Save`,
`Persist`, `ReportExport`, `ExportWriter`, `IFileDialogService`, `OpenFile`, `SaveFile`, `PDF`, `Docx`, `Zip`,
`SupportPackageService`, `Backup`, `Restore`, `DiagnosticsService`, `RC0`, `Research`, `Anonymization`,
`SmartCoachEngine`, `ProgressionSafetyGate`, `VoiceHealth`, `VocalHealthSupervisor`, `RecoveryScorer`,
`RecoveryIntelligenceService`, `MicrophoneCalibration`, `NAudio`, `Wasapi`, `WaveIn`, `OxyPlot`):
- **The Diagnostics slice's new files (`DiagnosticsViewModel.cs`, `DiagnosticsView.axaml(.cs)`) introduce ZERO
  forbidden references** (verified incl. comments — Norwegian labels: Støttepakke / Sikkerhetskopi / Gjenoppretting /
  Dataeksport / Forskning / anonymisering avoid the English tokens).
- The only remaining match across `FemVoice.Avalonia` is the **pre-existing** `IFileDialogService` /
  `AvaloniaFileDialogService` placeholder (null-returning) and its DI registration in `Program.cs` — Phase-2
  abstraction code **not** added or modified by this slice (`git diff main` adds no file-dialog line) and **not**
  used by the Diagnostics scaffold.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline; 1569/1580 is the
documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake variant). No new failures.

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only changes; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** All additions display-only over static placeholders.
