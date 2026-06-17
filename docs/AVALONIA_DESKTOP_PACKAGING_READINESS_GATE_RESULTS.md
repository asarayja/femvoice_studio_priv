# Avalonia macOS/Linux Packaging Readiness — Gate Results

Date: 2026-06-17 · Branch: `avalonia-desktop-packaging-readiness-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

All commands from the repo root with `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` (default, no `-r`) → **Build succeeded. 0 Warning(s) 0 Error(s).**
(Adding plural `RuntimeIdentifiers` did not disturb the default portable build/run.)

## Smokes (`dotnet run --project FemVoice.Avalonia --no-build -- --<smoke>`)
| Smoke | Result |
| --- | --- |
| `--smoke` … `--diagnostics-scaffold-smoke` (14 prior) | **all OK** |
| `--packaging-smoke` | **OK** |

### `--packaging-smoke` output
```
[pkg] csproj: found=True RIDs(linux-x64;linux-arm64;osx-x64;osx-arm64)=True Tmds-pin-0.21.3=True no-trim=True
[pkg] project refs: count=2 core+abstractions-only=True
[pkg] templates: macos/Info.plist=True linux/.desktop=True
[pkg] runtime refs: Core=True Abstractions=True no-other-FemVoice.Audio=True
[pkg] Packaging readiness smoke OK
```
Read-only verification: the csproj declares the 4 Linux/macOS RIDs + `RuntimeIdentifiers`, the Tmds 0.21.3 pin,
`PublishTrimmed=false`, and exactly 2 project refs (Core + Audio.Abstractions); the inert packaging templates
exist; and the running head references no `FemVoice.Audio.*` assembly other than Abstractions.

## Optional publish verification (run, Linux host)
```
dotnet publish … -r linux-x64 --self-contained false   -> succeeded (apphost 78 KB + managed DLLs)
dotnet publish … -r osx-x64   --self-contained false   -> succeeded (apphost 89 KB + managed DLLs)
dotnet /tmp/femvoice-publish-linux-x64/FemVoice.Avalonia.dll --smoke  -> [smoke] OK (exit 0)
```
Published `linux-x64` output contains `FemVoice.Avalonia.dll`, `FemVoice.Core.dll`,
`FemVoice.Audio.Abstractions.dll`, `Avalonia.dll`, `Tmds.DBus.Protocol.dll` — and NOT `FemVoice.Audio.Windows`.
(Standalone FDD apphost needs a system/registered .NET runtime; on this user-local-SDK box it is launched via
`dotnet <app>.dll`. No self-contained/trimmed publish and no full RID matrix were run.)

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard
Base forbidden list → **CLEAN (zero real references)** (only the two pre-existing negation comments match & are excluded).

Packaging-specific list (`Android`, `iOS`, `Xamarin`, `Maui`/`MAUI`, `UIKit`, `AVAudioEngine`, `CoreAudio`,
`PulseAudio`, `ALSA`, `PipeWire`, `NAudio`, `Wasapi`, `WaveIn`, `SQLite`, `IDatabaseService`,
`ExerciseSessionRecorder`, `Save`, `Persist`, `SmartCoachEngine`, `ProgressionSafetyGate`, `VoiceHealth`,
`VocalHealthSupervisor`, `RecoveryScorer`, `RecoveryIntelligenceService`, `MicrophoneCalibration`, `OxyPlot`):
- **The slice's new source/project/template files** (`FemVoice.Avalonia.csproj` packaging additions,
  `Packaging/macos/Info.plist`, `Packaging/linux/femvoice-studio.desktop`) and the new `--packaging-smoke`
  Program.cs additions → **introduce ZERO forbidden tokens** (verified via `git diff` of the additions).
- Acceptable, non-introduced matches: `Packaging/README.md` is **documentation** mentioning target platforms in
  negations ("no … Android/iOS work") — explicitly allowed. `Program.cs` `notSaved` (the `Save` substring) is the
  **pre-existing** runtime-lifecycle smoke variable, not added by this slice and not a real `Save` API.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline; 1569/1580 is the
documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake variant). No new failures.

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only csproj/metadata + smoke; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Packaging metadata + inert templates only; default build/run unchanged.
