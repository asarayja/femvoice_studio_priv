# Avalonia Runtime Chart + Live Feedback — Gate Results

Date: 2026-06-16 · Branch: `avalonia-runtime-chart-feedback-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

All commands from the repo root with `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (`dotnet run --project FemVoice.Avalonia --no-build -- --<smoke>`)
| Smoke | Result |
| --- | --- |
| `--smoke` | **OK** (exit 0) |
| `--dashboard-smoke` | **OK** (exit 0) |
| `--exercise-smoke` | **OK** (exit 0) |
| `--exercise-runtime-smoke` | **OK** (exit 0) |
| `--exercise-runtime-integration-smoke` | **OK** (exit 0) |
| `--exercise-coordinator-smoke` | **OK** (exit 0) |
| `--runtime-chart-feedback-smoke` | **OK** (exit 0) |

### `--runtime-chart-feedback-smoke` output
```
[chart] Exercises: 15
[chart] Exercise: Grunnleggende humming
[chart] Samples: 29 (cap respected: True)
[chart] Axis: 115-205 Hz, height 200px
[chart] Target band: bottom=56px top=144px (OK)
[chart] Current marker: 160,0 Hz @ 100px (OK)
[chart] Feedback: Innenfor målområdet [I mål]
[chart] Derived hold: 24%  Coordinator hold: 0%
[chart] Hold comparison: -0,7 s (koordinator − avledet)
[chart] Navigation: OK (runtime=True back-to-detail=True)
[chart] Runtime chart feedback smoke OK
```
Notes: fixed axis 115–205 Hz (from the 140–180 target band via `PitchChartAxisRangeCalculator`); band and
marker px are consistent with the trace's px space; trace cap (120) respected; feedback "Innenfor målområdet";
derived hold accumulates (24 %) while the coordinator hold stays 0 % under the neutral resonance placeholder
(documented in the coordinator slice). Headless note: the smoke verifies VM/chart state and navigation; AXAML
rendering (Canvas attached-property bindings) is not exercised headlessly.

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**. No OxyPlot package
of any kind. Packages: Avalonia 11.2.1 (+ Desktop, Fluent), Microsoft.Extensions.DependencyInjection 8.0.0,
Tmds.DBus.Protocol 0.21.3.

## Leak guard (forbidden tokens in `*.cs` / `*.axaml` / `*.csproj`)
Searched (extended for this slice): `System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot.Wpf`,
`OxyPlot.Avalonia`, `OxyPlot`, `FemVoice.Audio.Windows`, `NAudioCaptureService`, `WaveInEvent`,
`WasapiCapture`, `ThemeManager`, `LocExtension`, `LocConverter`, `FeedbackConsistencyGuard`,
`ComfortZoneController`.
**Result: CLEAN — zero real references.** Every match is a comment/docstring mentioning a token in negation
(e.g. "no OxyPlot", "NOT FeedbackConsistencyGuard / SmartCoach", "OxyPlot.Avalonia port deferred", plus the two
pre-existing documentary comments about a future MessageBox library and the Windows-only NAudioCaptureService).
A non-comment grep returned empty.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline: 9
`NewLanguageResourcesTests` placeholder mismatches + `ExerciseGuideEncodingTests.All12Resx`; the intermittent
`ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake may appear → 1569/11). **No new
failures; this slice changed no test-covered Core code.**

## Windows CI
Pending PR (`windows-wpf-verification.yml`). This slice is Avalonia-only code; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour.** WPF untouched; all additions display-only.
