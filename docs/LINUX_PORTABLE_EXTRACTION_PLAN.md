# Linux Portable Extraction — Plan & As-Executed (Phases L1–L2)

Date: 2026-06-16 · Status: **EXECUTED and build/test-verified on Linux** (.NET 10.0.301). Branch: `linux-portable-core`.

This supersedes the Windows-gated `SHARED_PROJECT_EXTRACTION_PLAN.md` for the Linux-first slice: here the moves were actually performed and compiled. Namespaces were kept stable (`FemVoiceStudio.*`) so no call sites changed.

## Projects created
| Project | TFM | Role | Build |
| --- | --- | --- | --- |
| `FemVoice.Core` | net10.0, `RootNamespace=FemVoiceStudio` | All UI-free domain + data + DSP + localization + diagnostics + RESX | ✅ 0 warn / 0 err |
| `FemVoice.Audio.Abstractions` | net10.0 | `IAudioCaptureService` + DTOs + Synthetic/Noop capture | ✅ |
| `FemVoice.Tests.Portable` | net10.0 | Portable xUnit tests (refs Core + Abstractions) | ✅ |
| `FemVoice.Avalonia` | net10.0 | Minimal Avalonia head (refs Core + Abstractions) | ✅ |

> Pragmatic deviation from the prompt's suggested per-concern split: `Reports`, `Localization`, `Diagnostics`, `Audio.Dsp` were **folded into `FemVoice.Core`** (all net10.0, UI-free) rather than created as separate assemblies. This avoided inter-project circular references (e.g. Core services consume `ILocalizationService`; LocalizationService consumes `ISettingsService`/`AppSettings`) and is the lowest-risk path to a building+tested core. Splitting them out later is a mechanical follow-up. The gate script reflects the actual layout.

## What moved into FemVoice.Core (compiler-verified closure)
- `Models/**` → `FemVoice.Core/Models/` (52 files) + relocated `ResonanceCategory` enum (see below).
- `Data/**` → `FemVoice.Core/Data/` (DatabaseService, ExerciseDataService, repository interfaces, `migrations/`).
- `Services/**` → `FemVoice.Core/Services/` (**112 files**) — scoring, SmartCoach, VocalHealth/recovery/safety gates, progression, analytics, feedback pipeline + all 6 mappers, ExportWriter/ReportAssembler/ReportTextSanitizer, LocalizationService, Rc0RuntimeLog/Rc0WriteFailureSink/Rc0EvidenceExporter/DiagnosticsNaming, ResearchAnonymizer/Aggregator, ParticipantTokenProvider, PilotReadinessChecker, the SQLite stores, settings family, etc.
- Audio DSP → `FemVoice.Core/Audio/` (13 files): PitchDetectionService, AdaptivePitchDetector, FormantDetectionService, VoiceActivityDetector, VocalWeightAnalyzer, VoiceStrainDetector, SpeechRateAnalyzer, VoiceMetricsCalculator, ResonansScoringService, ResonanceProxyEngine, MicrophoneCalibrationService, MicrophoneCalibrationProfile, AudioCaptureDiagnostics.
- RESX → `FemVoice.Core/Resources/` (`Strings.resx` + 18 culture satellites + the two mis-named files). **`RootNamespace=FemVoiceStudio` preserves the `FemVoiceStudio.Resources.Strings` manifest base name** — verified at runtime (smoke prints `Common_Yes → "Ja"`).

## Two small behaviour-neutral extractions required (done)
1. **`AppTheme` / `AppSettings` / `DebugSettings` / `AppSettingsJson`** moved from `ThemeManager.cs` (WPF) → `FemVoice.Core/Services/SettingsModels.cs` (same namespace `FemVoiceStudio.Services`). Unblocks `ISettingsService`, `LocalizationService`, the settings family. `ThemeManager.cs` (stays in WPF) consumes them via the Core reference. Pure DTO move — no logic change.
2. **`ResonanceCategory` enum** moved from the dead `Subsystems/Analysis/IAnalysisSubsystem.cs` → `FemVoice.Core/Models/ResonanceCategory.cs` (same namespace `FemVoiceStudio.Subsystems.Analysis`), because live `Models` (TrainingSession, Feedback) depend on it. The dead subsystem file stays in WPF and consumes it via Core.

Also added `FemVoice.Core/AssemblyInfo.cs` with `[InternalsVisibleTo("FemVoice.Tests.Portable")]` + `[InternalsVisibleTo("FemVoiceStudio.Tests")]` (the original grant lived in `AudioCaptureService.cs`, which stayed in WPF). Test-visibility only.

## What stayed in the WPF project (`FemVoiceStudio`, net10.0-windows — NOT built on Linux)
- **Services (8, WPF/Windows-coupled):** `ThemeManager.cs` (System.Windows + Registry), `AnalysisChartTheme.cs` (System.Windows.Media + OxyPlot), `IconProvider.cs` (System.Windows), `FeedbackService.cs` (WPF `Loc` markup), `LocalBackupService.cs`, `PrivacyConsentPolicy.cs`, `Rc0StartupBootstrap.cs`, `SupportPackageService.cs` (last four reference `ThemeManager.SettingsPath`).
- **Audio (6, capture/orchestration/dead):** `AudioCaptureService.cs`, `AudioAnalysisEngine.cs`, `AudioAnalysisEngine_new.cs`, `AudioAnalyzerService.cs`, `RealtimeAnalysisEngine.cs`, `AsyncAudioPipeline.cs`.
- All `Views/**`, `ViewModels/**`, `Converters/**`, `Themes/**`, `Resources/Icons.xaml`, `App.xaml(.cs)`, `Infra/**` (dead), `Subsystems/**` (dead), `FemVoiceStudio/Tests/**`.
- `FemVoiceStudio.csproj` now `<ProjectReference>`s `FemVoice.Core` + `FemVoice.Audio.Abstractions`, and its `Resources\Strings.resx` `EmbeddedResource` block was removed (RESX moved). **These WPF-side edits are unverified on Linux** (cannot build WPF here) — re-verify on Windows.

## Refuted risk
The much-feared `Rc0RuntimeLog → ThemeManager.SettingsPath` WPF leak **does not exist**: `Rc0RuntimeLog` imports only `System`/`System.IO` and depends only on the clean `DiagnosticsNaming` + `Rc0WriteFailureSink`. The diagnostics logger moved cleanly into Core. (The "blockers" from the earlier minimal-slice analysis — `ExerciseSessionOutcome`, `SmartCoachMessage`, the `[Obsolete]` ctor — dissolved because the full portable set, including their host files, moved together.)

## Verification
`dotnet build` of all four portable projects: **0 errors**. `dotnet test FemVoice.Tests.Portable`: **1570/1580 pass** (10 pre-existing localization-data failures, documented). See `LINUX_PORTABLE_GATE_RESULTS.md`.
