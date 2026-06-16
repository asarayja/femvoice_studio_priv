# Linux Portable Gate — Results (Phase L5)

Date: 2026-06-16 · Host: Ubuntu 26.04, linux-x64 · SDK: .NET 10.0.301 (user-local `~/.dotnet`).
Gate script: `scripts/linux-portable-gate.sh`.

## Build (net10.0 portable projects) — ✅ ALL GREEN
| Project | Result |
| --- | --- |
| FemVoice.Audio.Abstractions | Build succeeded — 0 warnings, 0 errors |
| FemVoice.Core | Build succeeded — 0 warnings, 0 errors (+ 19 culture satellite assemblies generated) |
| FemVoice.Tests.Portable | Build succeeded — 0 warnings, 0 errors |
| FemVoice.Avalonia | Build succeeded — 0 errors (2 warnings: `NU1903` transitive `Tmds.DBus.Protocol` 0.20.0 advisory pulled by Avalonia.Desktop on Linux — see Notes) |

## Test (FemVoice.Tests.Portable, net10.0) — 1570 / 1580 PASS
```
Failed: 10, Passed: 1570, Skipped: 0, Total: 1580, Duration: ~7 s
```
- **0 regressions caused by the port.** The 1570 passing include every prioritized safety/clinical suite (safety-invariant, feedback-priority, recovery, scoring, SmartCoach, progression-gate, report-assembly/export, research-anonymization, DSP, calibration).
- **10 failures are PRE-EXISTING** (proven via git `HEAD`: identical test code + byte-identical RESX; even `Strings.en.resx` exhibits the quirk). They are **not** caused by the port and are **not editable here** (hard rule: don't change localization resources/assertions):
  - 9× `NewLanguageResourcesTests.NewFile_PreservesPlaceholdersPipesAndGlobs` (ar, cs-CZ, el-GR, hu-HU, nl-NL, pl-PL, ro-RO, tr-TR, uk-UA): key `Report_RecommendationHighFatigueFormat` has placeholder set `{0} {0} {1:F1}` vs neutral `{0} {1:F1}`.
  - 1× `ExerciseGuideEncodingTests.ResourceFiles_NoMojibake_All12Resx`: asserts 12 resx; repo has 21 (stale expectation after the 9-language expansion).
- **+1 intermittent flake (≈1 run in 4): `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate`.** Repeated runs gave 10,10,11,10,10,11,10,10,10 failures. This is a **timing-sensitive event test** (not localization), failing occasionally under xUnit parallel CPU contention. `ComfortZoneController` was moved **verbatim** (unchanged), so this is pre-existing test flakiness, not a port regression. Mitigation (optional, later): assign it `DisableParallelization` or make the event assertion deterministic.

## Avalonia headless smoke — ✅ PASS
```
[smoke] ILocalizationService -> FemVoiceStudio.Services.LocalizationService
[smoke] IUiDispatcher        -> AvaloniaUiDispatcher
[smoke] capture backend       -> NoopAudioCaptureService (devices=0)
[smoke] Core scoring type     -> FemVoiceStudio.Services.FemVoiceScore
[smoke] localized 'Common_Yes' -> Ja
[smoke] OK: shared FemVoice.Core services resolve on Linux via the Avalonia head DI.
```
`Common_Yes → "Ja"` confirms the RESX manifest base name (`FemVoiceStudio.Resources.Strings`) survived the move into `FemVoice.Core` (RootNamespace fix worked) — localization is intact on Linux.

## Notes / follow-ups
- `NU1903` (Tmds.DBus.Protocol 0.20.0, high-severity advisory) is a **transitive** dependency of Avalonia.Desktop's Linux backend, not a direct reference. Track an Avalonia bump that pulls a patched version; not a blocker for the portable-core gate.
- The WPF app (`net10.0-windows`) is intentionally excluded from this gate and must be built/tested on Windows (it cannot build on Linux).
- To reproduce: `bash scripts/linux-portable-gate.sh` (sets `DOTNET_ROOT`/`PATH` to `~/.dotnet`).
