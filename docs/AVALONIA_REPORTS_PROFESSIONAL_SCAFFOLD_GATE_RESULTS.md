# Avalonia Reports / Professional Workflow Scaffold — Gate Results

Date: 2026-06-17 · Branch: `avalonia-reports-professional-scaffold-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

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
| `--shell-smoke` | **OK** (updated: 5 implemented / 4 deferred) |
| `--theme-loc-smoke` | **OK** |
| `--settings-smoke` | **OK** |
| `--runtime-lifecycle-smoke` | **OK** |
| `--analysis-scaffold-smoke` | **OK** |
| `--reports-scaffold-smoke` | **OK** |

### `--reports-scaffold-smoke` output
```
[reports] nav-implemented=True onReports=True cards=8
[reports] inert: notDisposable=True noCommands=True cardsOk=True allDeferred=True
[reports] Runtime->Reports: ran=True disposed=True no-orphan-frames=True
[reports] Reports scaffold smoke OK
```
Verifies: the Reports nav item exists and is implemented; navigating switches `CurrentPage` to a `ReportsViewModel`
that is inert (not IDisposable, no `IRelayCommand`); 8 placeholder cards all deferred (`AllActionsDeferred`); and
navigating to Reports from a running runtime disposes the runtime (`IsRunning==false`, trace stops growing → no
orphaned synthetic capture).

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard
Base forbidden list → **CLEAN (zero real references)** (only the two pre-existing negation comments match & are excluded).

Reports-specific list (`SQLite`, `IDatabaseService`, `ExerciseSessionRecorder`, `SessionAnalyticsStore`, `.Save(`,
`Persist`, `ReportExport`, `ExportWriter`, `IFileDialogService`, `OpenFile`, `SaveFile`, `PDF`, `Docx`,
`ClinicianDashboard`, `CoachDashboard`, `CaseReview`, `SmartCoachEngine`, `ProgressionSafetyGate`, `VoiceHealth`,
`VocalHealthSupervisor`, `RecoveryScorer`, `RecoveryIntelligenceService`, `MicrophoneCalibration`, `NAudio`,
`Wasapi`, `WaveIn`, `OxyPlot`):
- **The Reports slice files introduce ZERO forbidden references.** (The professional case-review card uses the
  Norwegian localization key `Reports_Saksgjennomgang` — no `CaseReview` token; a repo-wide grep for `CaseReview`
  in `FemVoice.Avalonia` returns zero matches. Professional cards use Norwegian labels — Klinikerpanel /
  Veilederpanel / Saksgjennomgang / Eksporter — so no English forbidden token appears.)
- The only remaining matches across `FemVoice.Avalonia` are **pre-existing** platform-abstraction placeholders:
  `IFileDialogService` / `AvaloniaFileDialogService` with `PickOpenFileAsync` / `PickSaveFileAsync` (all return
  `null`) in `Platform/AvaloniaPlatformServices.cs`, plus the DI registration at `Program.cs:50`. These predate
  this slice (Phase-2 abstractions), were **not** modified by it (`git diff main` does not touch them), and are
  **not** used by the Reports scaffold (which opens no file dialogs).

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline; 1569/1580 is the
documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake variant). No new failures.

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only changes; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** All additions display-only over static placeholders.
