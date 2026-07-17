# Avalonia — Android head bootstrap — Gate Results

Date: 2026-07-17 · Branch: `avalonia-android-head-bootstrap-slice` (off `main` @ `e69808f`) · Host: Linux (.NET 10 SDK `10.0.110`, `android` workload `36.1.2`; no Android SDK, JRE-only Java).

> 4th-platform Android head reusing the shared Avalonia UI, plus desktop-verified single-view enablers (lazy DI,
> `ISingleViewApplicationLifetime` branch, extracted shared `ShellView`). Readiness slice: APK build deferred on
> toolchain provisioning. No clinical/DSP/WPF/DB/Core change.

## Build (desktop head)
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Error(s).**

## Smokes (39 — all OK, all exit 0)
38 prior + **`--android-readiness-smoke` (new, 39th)** → **39/39 OK.**
- `--android-readiness-smoke`: `diOk=True scanned=True headOk=True sharedOk=True gateIsolated=True`.
- Shell extraction did not regress any UI/theme smoke — `--visual-baseline-smoke`, `--packaged-theme-smoke`,
  `--visual-layout-polish-smoke`, `--shell-smoke`, and the exercise/settings/localization/audio smokes all remain green.
- Real GUI boot check: the desktop shell booted on the display for 5 s and stayed alive with **no XAML load exception**
  (verifies the `MainWindow` → `ShellView` extraction loads at runtime, which the reflection-binding smokes don't exercise).

## Android head
- **`dotnet restore FemVoice.Android`** → success (`Avalonia.Android` 11.2.1 + shared project graph + Android SQLite
  RID variant resolve).
- **`dotnet build FemVoice.Android`** → reaches the Android SDK stage and stops on **provisioning only**:
  `error XA5300` (no Android SDK) + JRE missing `jar` (needs a full JDK). Both need machine-level setup (root / large
  downloads) unavailable here. Not a project defect. Build/run steps documented in `FemVoice.Android/README.md`.
- **Kept out of `FemVoiceStudio.slnx`** and `scripts/linux-portable-gate.sh` → the cross-platform Linux gate builds
  and tests unchanged without the Android SDK. `FemVoice.Android/bin,obj` are gitignored (not committed).

## Reference / leak guard
- `FemVoice.Avalonia` still references **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- `FemVoice.Android` references `Avalonia.Android` + `FemVoice.Avalonia` (thin platform host; no domain logic).
- No clinical/DSP/SmartCoach/recovery/Core/WPF change; no DB; no real capture wired (runtime still synthetic).
- Diff scope: `FemVoice.Avalonia/` (Program.cs, App.axaml.cs, MainWindow.axaml, Views/ShellView.axaml[.cs]),
  new `FemVoice.Android/`, `docs/`.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** — documented baseline (10 pre-existing localization-data
failures). **0 regressions.**

## Behaviour change
None to existing behaviour. Adds an Android head (reusing the shared shell) that restores + is structured to build
once the Android SDK + full JDK are provisioned, and makes the shared `App`/shell single-view-capable (desktop path
unchanged). No clinical/domain/WPF change.
