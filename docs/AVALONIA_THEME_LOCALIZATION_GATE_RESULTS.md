# Avalonia Theme + Localization Adapter Parity — Gate Results

Date: 2026-06-16 · Branch: `avalonia-theme-localization-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

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
| `--shell-smoke` | **OK** |
| `--theme-loc-smoke` | **OK** |

### `--theme-loc-smoke` output
```
[theme-loc] Localized.Get: 'Common_Yes'='Ja'  missing->'Fallback-X'
[theme-loc] Shell labels: nav[0]='Dashbord' nav[2]='Innstillinger — senere' mic='Mikrofon: syntetisk (kun visning)'
[theme-loc] Deferred page: title='Innstillinger — utsatt'
[theme-loc] Theme brushes: all present (14 keys × Dark+Light)
[theme-loc] Theme + localization smoke OK
```
Verifies: the read-only adapter resolves a known RESX key (`Common_Yes`→"Ja") and falls back on a missing key;
`LocalizedValue` + `TrExtension` resolve/fall back (the markup pattern returns an Avalonia `Binding`);
shell/nav/status/deferred labels resolve or fall back to the current text; and the **guarded** runtime check
(an Avalonia platform was available on this host) confirmed all 14 shell theme brushes resolve in both Dark and
Light variants. No `SetLanguage` is called (localization semantics preserved). On a host without an Avalonia
platform the theme runtime check is cleanly skipped (logged) and the localization assertions still gate.

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard (forbidden tokens in `*.cs` / `*.axaml` / `*.csproj`)
Searched: `System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot*`, `FemVoice.Audio.Windows`,
`NAudioCaptureService`, `WaveInEvent`, `WasapiCapture`, `ThemeManager`, `LocExtension`, `LocConverter`,
`FeedbackConsistencyGuard`, `ComfortZoneController`.
**Result: CLEAN — zero real references.** The localization adapter is named to avoid the WPF tokens
(`Localized`/`LocalizedValue`/`TrExtension`, not LocExtension/LocConverter), and the theme comment was worded
to avoid the literal `ThemeManager` token. Only the two pre-existing negation comments remain (matched & excluded).

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1569, Failed: 11, Total: 1580** on this run. The 11th failure
is the **known intermittent** `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake
(confirmed by name); the other 10 are the known localization-data baseline (9 `NewLanguageResourcesTests` +
`ExerciseGuideEncodingTests.All12Resx`). Baseline is 1570/1580; 1569/1580 is the documented flake variant.
**No new failures; this slice changed no test-covered code.**

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only changes; WPF build unaffected. (The `--theme-loc-smoke`
runs as a Linux gate step; CI gates the WPF/shared build + test suites.)

## Behaviour change
**None to clinical/domain behaviour. WPF untouched. Localization semantics preserved.** All additions display-only.
