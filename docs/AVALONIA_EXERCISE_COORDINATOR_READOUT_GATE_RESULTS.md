# Avalonia Exercise Coordinator Readout — Gate Results

Date: 2026-06-16 · Branch: `avalonia-exercise-coordinator-readout-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

All commands run from the repo root with `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (`dotnet run --project FemVoice.Avalonia --no-build -- --<smoke>`)
| Smoke | Result |
| --- | --- |
| `--smoke` | **OK** (exit 0) — shared Core services resolve via Avalonia DI |
| `--dashboard-smoke` | **OK** (exit 0) — pitch/stability/health from synthetic audio |
| `--exercise-smoke` | **OK** (exit 0) — 15 exercises, detail, nav |
| `--exercise-runtime-smoke` | **OK** (exit 0) — pitch 160 in 140–180, hold accumulates, nav |
| `--exercise-runtime-integration-smoke` | **OK** (exit 0) — 15/15 profiles mapped, "Mål-profil" panel, nav |
| `--exercise-coordinator-smoke` | **OK** (exit 0) — coordinator active, readout produced, safety display-only, stop clears, nav |

### `--exercise-coordinator-smoke` output (key lines)
```
[coord] Exercises: 15
[coord] Exercise: Grunnleggende humming
[coord] Coordinator active: True
[coord] Coordinator hold: 0,0s (0%)
[coord] Derived hold: 0,7s (24%)
[coord] Hold difference: -0,7 s (koordinator − avledet)
[coord] Coordinator state: I komfortsone
[coord] Raw: hold=0,00 inZone=True holding=False locked=False elapsed=0s quality=Poor
[coord] Safety readout: display-only
[coord] Readout mode: Visning-bare koordinator-readout (ikke håndhevet)
[coord] After stop -> coordinator active: False
[coord] Navigation: OK (runtime=True back-to-detail=True)
[coord] Exercise coordinator smoke OK
```
Notes: coordinator hold reads 0 % because resonance is fed as a documented neutral placeholder (60) vs the
profile's **0.50–0.85** resonance target (a normalized 0–1 score; the "Mål-profil" panel rounds to "0–1" via
`:F0`); the derived (pitch-band) hold accumulates. Both are shown — display-only.
`locked=False` (health placeholder 100 → no lock); the readout labels the safety state non-enforced.
`After stop -> coordinator active: False` confirms stop/back clears the VM-local coordinator state.

## Vulnerability scan
`dotnet list FemVoice.Avalonia/FemVoice.Avalonia.csproj package --vulnerable --include-transitive`
→ **"has no vulnerable packages given the current sources."** `Tmds.DBus.Protocol` pinned to **0.21.3**.

## Project references (leak boundary)
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`** (no `FemVoice.Audio.Windows`).
Packages: Avalonia 11.2.1 (+ Desktop, Fluent), Microsoft.Extensions.DependencyInjection 8.0.0, Tmds.DBus.Protocol 0.21.3.

## Leak guard (forbidden tokens in FemVoice.Avalonia `*.cs` / `*.axaml` / `*.csproj`)
Searched: `System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot.Wpf`, `FemVoice.Audio.Windows`,
`NAudioCaptureService`, `WaveInEvent`, `WasapiCapture`, `ThemeManager`, `LocExtension`, `LocConverter`.
**Result: CLEAN — no real references.** Two pre-existing **documentary comments** match the regex and are NOT references:
- `Platform/AvaloniaPlatformServices.cs:15` — "… will use a **MessageBox** library …" (future-work prose).
- `Program.cs:47` — "the real **NAudioCaptureService** would be wired … **NOT here**." (explicitly excluded.)
No new code introduced any forbidden API.

## Portable tests
`dotnet test FemVoice.Tests.Portable/FemVoice.Tests.Portable.csproj`
→ **Passed: 1570, Failed: 10, Total: 1580** (known baseline). The 10 are pre-existing localization-data
failures: 9 × `NewLanguageResourcesTests.NewFile_PreservesPlaceholdersPipesAndGlobs` (placeholder mismatch
in `Report_RecommendationHighFatigueFormat`) + `ExerciseGuideEncodingTests.ResourceFiles_NoMojibake_All12Resx`.
The intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake may appear on some
runs (→ 1569/11). **No new failures; this slice changed no test-covered Core code.**

## Windows CI
Pending PR (GitHub Actions `windows-wpf-verification.yml`). The Avalonia head + this slice are Linux-only
code; WPF build is unaffected (no shared/WPF files changed).

## Behaviour change
**None to clinical/domain behaviour.** WPF untouched. Coordinator driven read-only; output rendered only.
