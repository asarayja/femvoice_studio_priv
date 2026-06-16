# Avalonia Desktop Shell + Navigation/Layout Parity — Gate Results

Date: 2026-06-16 · Branch: `avalonia-shell-navigation-layout-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

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
| `--shell-smoke` | **OK** (exit 0) |

### `--shell-smoke` output
```
[shell] Nav items: 9 (implemented=2, deferred=7)
[shell] Lands on: Dashbord
[shell] Deferred nav 'Innstillinger — senere' -> static placeholder (inert=True)
[shell] Runtime lifecycle: running=True disposed-on-nav=True no-orphan-frames=True fresh-instance=True second-running=True no-orphan=True
[shell] Shell smoke OK
```
Verifies: shell constructs; lands on dashboard; 2 implemented + 7 deferred nav items; implemented nav switches
pages; deferred nav opens a static, non-`IDisposable` placeholder (inert); runtime is disposed on nav-away
(`IsRunning=false`); the disposed runtime's pitch trace does **not** keep growing after nav-away
(`no-orphan-frames` — direct proof the synthetic `FrameAvailable` handler was unsubscribed); re-open yields a
fresh distinct running instance with the first still stopped (no orphan, no duplicate runtime).

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`** (no FemVoiceStudio/WPF, no Audio.Windows).

## Leak guard (forbidden tokens in `*.cs` / `*.axaml` / `*.csproj`)
Searched: `System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot*`, `FemVoice.Audio.Windows`,
`NAudioCaptureService`, `WaveInEvent`, `WasapiCapture`, `ThemeManager`, `LocExtension`, `LocConverter`,
`FeedbackConsistencyGuard`, `ComfortZoneController`.
**Result: CLEAN — zero real references** (a non-comment grep returned empty; only the two pre-existing
documentary comments — a future MessageBox library and the Windows-only NAudioCaptureService — match, both negations).
No value converters were introduced.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline: 9
`NewLanguageResourcesTests` placeholder mismatches + `ExerciseGuideEncodingTests.All12Resx`; the intermittent
`ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake may appear → 1569/1580). **No new
failures; this slice changed no test-covered code.**

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only changes; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour.** WPF untouched; all additions display-only shell/layout.
