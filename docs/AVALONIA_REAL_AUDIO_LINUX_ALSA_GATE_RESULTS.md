# Avalonia — Real cross-platform microphone capture (Linux/ALSA) — Gate Results

Date: 2026-07-17 · Branch: `avalonia-real-audio-linux-alsa-slice` (off `main` @ `af3af06`) · Host: Linux (.NET 10 SDK `10.0.110`, `libasound.so.2`, real capture device + PulseAudio).

> Real ALSA microphone capture behind `IAudioCaptureService`; `CrossPlatformAudioCaptureService` becomes an OS
> dispatcher (Linux real, macOS/Windows unavailable). Runtime DI unchanged (synthetic stays the runtime backend);
> no clinical/DSP/WPF/DB/Core change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Error(s).**
(`FemVoice.Audio.Abstractions` builds with **0 Warning(s)**; the only warning in the wider build is the pre-existing
`NU1903` on `SQLitePCLRaw.lib.e_sqlite3` transitively via Core — untouched by this slice.)

## Smokes (38 — all OK, all exit 0)
37 prior + **`--real-audio-capture-smoke` (new, 38th)** → **38/38 OK.**
- `--real-audio-capture-smoke` (REAL path on this box): `frames=21 samples=21504 badSample=False devices=1 realAvailable=True` → captured real microphone frames, all finite in `[-1, 1]`, clean stop. On a device-less host it asserts the graceful path (`frames=0`, `realAvailable=false`, no throw).
- `--avalonia-audio-backend-smoke` (reworked env-agnostic): `enumerationSafe=True available=True consistent=True noAutoCapture=True probeSafe=True readinessTruthful=True syntheticUnaffected=True scanned=True noForbidden=True`; `backend=AlsaAudioCaptureService status="Mikrofon: enheter funnet: 1" devices=1`.
- `--avalonia-audio-readiness-smoke` / `--smoke` and all UI/packaging/localization smokes remain green.

## Vulnerable packages
No new dependency added (pure P/Invoke; no NuGet). `Tmds.DBus.Protocol` still pinned `0.21.3`. Pre-existing `NU1903` on `SQLitePCLRaw` (via Core) unchanged.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions` (unchanged).
- New code lives in `FemVoice.Audio.Abstractions` (dependency-free managed P/Invoke). Source scan over
  `CrossPlatformAudioCaptureService.cs` + `Linux/AlsaAudioCaptureService.cs` + `Linux/AlsaInterop.cs` → **no**
  `Audio.Windows`/`NAudio`/`WaveIn`/`Wasapi`/`DatabaseService`/`System.Windows`/`ThemeManager` refs.
- No DI default change (synthetic stays active). No clinical/DSP/SmartCoach/recovery/Core/WPF change. Diff scope: `FemVoice.Audio.Abstractions/`, `FemVoice.Avalonia/Audio/AudioReadiness.cs`, `FemVoice.Avalonia/Program.cs`, `docs/`, `docs/AVALONIA_MIGRATION_TRACKER.md`.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** — identical to the documented baseline. The 10 failures are the
known pre-existing localization-data quirks (1× `ExerciseGuideEncodingTests.ResourceFiles_NoMojibake_All12Resx`
+ 9× `NewLanguageResourcesTests.NewFile_PreservesPlaceholdersPipesAndGlobs`); **0 regressions** from this slice.

## Behaviour change
Real ALSA microphone capture is now available on Linux behind the abstraction, and readiness reports it truthfully
("Mikrofon: enheter funnet: N" when a device opens). No change to the display-only clinical runtime (still synthetic
via DI). macOS/Windows real capture remain deferred to their own slices.
