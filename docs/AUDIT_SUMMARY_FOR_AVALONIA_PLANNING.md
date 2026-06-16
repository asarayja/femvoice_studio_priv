# FemVoice Studio — Audit Summary for Avalonia Planning

Audit date: 2026-06-16 · Read-only audit; documentation only. This is the executive summary of the full audit set in `docs/`.

## 1. Current WPF baseline summary

FemVoice Studio is a finished, local, single-user **Windows WPF / .NET 10** voice-feminization training app. It provides real-time microphone analysis (pitch, resonance/formants, stability, intonation, vocal-weight/strain proxies), a 15-exercise guided catalog, SmartCoach recommendations, health/recovery/safety gating, progression & analytics, professional dashboards (clinician/coach), 4 report types in 3 formats, research-grade anonymized export, a privacy-filtered diagnostics/support-package pipeline (RC-0), and localization across ~19 effective languages with a Norwegian neutral base. The entire product is governed by the clinical priority hierarchy **Safety > Health > Recovery > Comfort > Voice Development > Reporting**, machine-enforced by the `FeedbackPriority` enum + `FeedbackConsistencyGuard` and the progression safety gates. Reporting/coaching/overrides are descriptive and can never weaken a safety/health/recovery decision.

## 2. Current project & package overview

- **Solution:** `FemVoiceStudio.slnx` → `FemVoiceStudio` (WinExe, `net10.0-windows`, `UseWPF`) + `FemVoiceStudio.Tests` (xUnit). ~263 app `.cs`, 27 `.xaml`, ~130 tests.
- **Packages:** CommunityToolkit.Mvvm 8.2.2, QuestPDF 2026.5.0, Microsoft.Data.Sqlite 8.0.0, Microsoft.Extensions.DependencyInjection 8.0.0, NAudio 2.2.1, OxyPlot.Wpf 2.1.2, xUnit 2.6.2 (+ runner) and Microsoft.NET.Test.Sdk 17.8.0. (Test packages are erroneously also referenced by the app project.)
- **DI:** entirely in `App.xaml.cs.ConfigureServices`. Two MVVM stacks coexist (CommunityToolkit + a dead hand-rolled base).
- **Domain core is overwhelmingly UI-free** — only 3 service files import `System.Windows`; no `Dispatcher` in Services/Models/Subsystems.

See [`CURRENT_PROJECT_STRUCTURE.md`](CURRENT_PROJECT_STRUCTURE.md) and [`CURRENT_PACKAGE_INVENTORY.md`](CURRENT_PACKAGE_INVENTORY.md).

## 3. Biggest Avalonia risks

1. **Audio capture (NAudio, Windows-only WASAPI/WaveIn/MMDevice)** — highest risk; needs an `IAudioCaptureService` abstraction now and a cross-platform backend later. DSP itself is portable.
2. **Theme system** — runtime `Application.Current.Resources` swap, `pack://` URIs, and a Windows Registry theme read. Full rewrite.
3. **Charts** — `OxyPlot.Wpf` → `OxyPlot.Avalonia`, plus the WPF-brush-reading half of `AnalysisChartTheme`.
4. **Real-time UI marshalling** — `Application.Current.Dispatcher` embedded in `MainViewModel`/`ExerciseDetailViewModel`/`SmartCoachViewModel`.
5. **Platform services** — file dialogs (`Microsoft.Win32`), `MessageBox` (95+ call sites), system-theme detection.
6. **Startup/splash** — `DispatcherFrame.PushFrame` + programmatic WPF splash window have no Avalonia equivalent.

See [`WPF_DEPENDENCY_MAP.md`](WPF_DEPENDENCY_MAP.md).

## 4. Systems that MUST NOT be changed (behaviour-frozen)

- **Clinical scoring** (`FemVoiceScore*`, `ClinicalSessionScore`, `VoiceIntelligenceScorer`, `LevelClassificationSystem`).
- **SmartCoach** (`SmartCoachEngine` and collaborators).
- **Voice Health / safety / recovery gates** (`VocalHealthSupervisor`, `RecoveryScorer`, `RecoveryIntelligenceService`, `ProgressionSafetyGate`, `RecoveryActivationPolicy`, `FeedbackConsistencyGuard`/`FeedbackPriority`).
- **Progression** (`ProgressionOrchestrator`, `MasteryEvaluator`, `ComfortZoneController`, `AdaptiveDifficultyService`).
- **Persistence** (SQLite schema + shared `femvoice.db` + the 8 stores).
- **Analytics** (`TrendEngineService`, `LongitudinalInsightEngine`, `VoicePatternDetector`, `ExerciseEffectivenessEngine`).
- **Diagnostics/evidence** (RC-0 pipeline, support package, audit trail) — must not be removed or weakened.
- **Reports** content (4 types × 3 formats) and **localization resources** (edit only the documented RESX naming fixes).
- **Exercise definitions** (the 15-exercise catalog + target profiles).

The safety invariant tests (`SafetyOverrideInvariantTests`, `SafetyPriorityEngineTests`, `ManualOverrideClampTests`, `FeedbackPriorityMatrixTests`) are the guardrails that prove these are intact — keep them green through the port.

## 5. Systems that MUST be abstracted (behind interfaces)

- UI dispatch → `IUiDispatcher`.
- Theme resource/brush lookup → `IThemeResourceProvider` (keys are already pure strings).
- File save/open → `IFileDialogService` (`ReportExportViewModel.FileSavePathOverride` is the model seam).
- Message dialogs → `IDialogService`.
- System theme detection → `ISystemThemeProvider`.
- Audio capture → `IAudioCaptureService` (NAudio impl on Windows).
- Chart color source for `AnalysisChartTheme` (inject colors instead of reading WPF resources).
- Diagnostics settings-path source (so it doesn't pull in WPF `ThemeManager`).

## 6. Systems likely reusable as-is

- All domain `Services/**` and `Models/**` (scoring, coach, health, recovery, progression, analytics, feedback).
- The entire `Data/**` + SQLite stores (no UI coupling).
- Reports (`ReportAssembler`/`ExportWriter`/QuestPDF — no charts in PDF, cross-platform).
- Diagnostics/evidence/research (file I/O only; mind Windows path shapes).
- Localization core (`LocalizationService` + RESX).
- All audio DSP analyzers (pure math; NAudio only for FFT `Complex`).
- The OxyPlot `PlotModel`-building VMs (once the package is swapped).

See [`AVALONIA_PORT_READINESS_NOTES.md`](AVALONIA_PORT_READINESS_NOTES.md) for the file-by-file extraction plan.

## 7. Documentation files created/updated by this audit

Created under `docs/`:
- `CURRENT_REPOSITORY_AUDIT.md`
- `CURRENT_PROJECT_STRUCTURE.md`
- `CURRENT_PACKAGE_INVENTORY.md`
- `WPF_DEPENDENCY_MAP.md`
- `CURRENT_FEATURE_MATRIX.md`
- `CURRENT_RUNTIME_WORKFLOWS.md`
- `CURRENT_REPORTS_AND_LOCALIZATION.md`
- `CURRENT_AUDIO_PIPELINE.md`
- `CURRENT_DIAGNOSTICS_AND_EVIDENCE.md`
- `AVALONIA_PORT_READINESS_NOTES.md`
- `AUDIT_SUMMARY_FOR_AVALONIA_PLANNING.md` (this file)

Updated:
- `work-documents/FemVoice Funksjonsoversikt.md` (corrected stale references; remains a current-state feature overview, not an Avalonia plan).

No source code, scoring, coaching, health, persistence, report, or localization resource was modified.

## 8. Open questions (NEEDS REVIEW)

1. Is `AudioAnalysisEngine`'s capture ever started in production, or is `AudioAnalyzerService` the sole live pitch engine? Are `RealtimeAnalysisEngine`/`AsyncAudioPipeline`/`AudioAnalysisEngine_new.cs` truly dead?
2. Are `Resources/DatabaseSchema.sql` and `Data/migrations/001_*.sql` executed at runtime, or dormant? (Migration 001 contains invalid SQLite syntax.)
3. Are `VoiceHealthService`/`HealthStatus` live anywhere, or orphaned from the gate flow?
4. Confirm `.cs.old`/`_new` exclusion from compilation, and that `AudioAnalysisEngine_new.cs` (empty) is harmless.
5. Cross-platform DB/diagnostics paths: `MyDocuments`/`LocalApplicationData` resolve differently off Windows — what is the desired location?
6. Backups omit SQLite WAL/SHM sidecars — acceptable, or fix before port?
7. Should the dead `Subsystems/**` + `Infra/DependencyInjection.cs` be deleted before or after the port?
8. Which languages are officially shipped? (pt-BR is currently mis-wired and effectively absent.)

## 9. Recommended next prompt for Avalonia planning

> "Using the `docs/` audit set as the source of truth (especially `WPF_DEPENDENCY_MAP.md` and `AVALONIA_PORT_READINESS_NOTES.md`), produce a phased Avalonia migration plan that: (1) first creates the shared UI-free projects (`FemVoice.Core`, `FemVoice.Audio.Abstractions`, `FemVoice.Reports`, `FemVoice.Localization`, `FemVoice.Diagnostics`) and moves the confirmed-portable code into them with zero behavioural change, keeping all safety-invariant tests green; (2) defines the abstraction interfaces (`IAudioCaptureService`, `IUiDispatcher`, `IDialogService`, `IFileDialogService`, `ISystemThemeProvider`, `IThemeResourceProvider`) and a Windows NAudio implementation; (3) stands up the Avalonia head with theme system, OxyPlot.Avalonia charts, and re-implemented converters/localization markup; (4) sequences view-by-view porting with the safety hierarchy and RC-0 diagnostics preserved. Do not modify clinical scoring, SmartCoach, Voice Health, persistence, report content, localization resources (beyond the documented RESX naming fixes), or the 15-exercise catalog. Resolve the open questions in `AUDIT_SUMMARY_FOR_AVALONIA_PLANNING.md` §8 first."
