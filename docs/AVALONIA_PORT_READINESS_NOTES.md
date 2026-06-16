# FemVoice Studio — Avalonia Port Readiness Notes (WPF Baseline)

Audit date: 2026-06-16 · Read-only documentation. **No projects are created and no code is moved by this document** — it records the extraction plan only.

This is the planning bridge between the current-state audit and a future Avalonia migration plan. It identifies code that should move into shared, UI-agnostic projects before/during the port. The suggested target structure (for discussion — **not yet created**):

```
FemVoice.Core                 (domain: scoring, smartcoach, health, recovery, progression, analytics, feedback, models)
FemVoice.Audio.Abstractions   (IAudioCaptureService + DSP-facing contracts + pure DSP analyzers)
FemVoice.Audio.Windows        (NAudio WASAPI/WaveIn implementation)
FemVoice.Audio.Desktop        (future cross-platform capture backend — Linux/macOS)
FemVoice.Analytics            (trend/longitudinal/pattern/effectiveness — could stay in Core)
FemVoice.Reports              (ReportAssembler + ExportWriter + QuestPDF)
FemVoice.Localization         (LocalizationService + RESX)
FemVoice.Diagnostics          (RC-0 evidence, support package, research anonymization)
FemVoice.Wpf                  (existing WPF head — reference baseline)
FemVoice.Avalonia             (new Avalonia head)
FemVoice.Tests                (portable tests; a Windows-only test head keeps WPF UI tests)
```

> Whether `FemVoice.Analytics`/`FemVoice.Localization`/`FemVoice.Diagnostics` are separate assemblies or namespaces inside `FemVoice.Core` is a later decision. The audit shows they are all already UI-free, so the split is low-risk either way.

## Readiness scorecard — CONFIRMED

| Layer | Avalonia readiness | Why |
| --- | --- | --- |
| Domain services (scoring/coach/health/recovery/progression/analytics/feedback) | ✅ Reusable as-is | No `System.Windows`/`Dispatcher` (verified by grep) |
| Models | ✅ Reusable as-is | Pure DTOs/records/enums |
| Data / SQLite stores | ✅ Reusable as-is | No UI coupling; Microsoft.Data.Sqlite is cross-platform |
| Reports (assembler/writer/QuestPDF) | ✅ Reusable as-is | QuestPDF cross-platform, no charts in PDF |
| Diagnostics / evidence / research | ✅ Reusable (mind Windows paths) | File I/O only |
| Localization core | ✅ Reusable | ResourceManager + CultureInfo |
| Audio DSP analyzers | ✅ Reusable as-is | Pure math (NAudio only for FFT `Complex`) |
| Audio capture (NAudio) | ⚠️ Windows-only | Behind abstraction now; cross-platform backend later |
| Chart VMs (PlotModel building) | ⚠️ Swap package | `OxyPlot.Wpf` → `OxyPlot.Avalonia` |
| Converters | ⚠️ Re-shell | Re-implement against Avalonia `IValueConverter` |
| Localization XAML extensions | ⚠️ Re-implement | `LocExtension`/`LocConverter` are WPF MarkupExtensions |
| Theme system | ⛔ Rewrite | ResourceDictionary swap + pack URIs + Registry theme read |
| Views + code-behind + animations + splash | ⛔ Rewrite | WPF XAML/animations/`DispatcherFrame` |
| Dispatcher / dialogs / file pickers / MessageBox / system-theme | 🔌 Abstract | `IUiDispatcher`, `IDialogService`, `IFileDialogService`, `ISystemThemeProvider`, `IThemeResourceProvider` |

## Extraction candidates

Each row: current location → proposed target, with dependencies, risk, and required tests. (Risk reflects how much the move could disturb behaviour, not difficulty.)

### To `FemVoice.Core` (domain — the bulk)

| Current file/path | Current project | Responsibility | Proposed target | Dependencies | Risk | Required tests | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Services/FemVoiceScore*.cs`, `ClinicalSessionScore.cs`, `VoiceIntelligenceScorer.cs`, `LevelClassificationSystem.cs` | FemVoiceStudio | Clinical scoring | FemVoice.Core | Models | Low | `FemVoiceScore*Tests`, `ClinicalSessionScoreTests`, `VoiceIntelligenceScorerTests` (already pass) | Move unchanged; scoring is a hard "do not modify" |
| `Services/SmartCoach*.cs`, `SmartCoachModule/*`, `LearningPathProfileBuilder.cs`, `ExerciseRecommendationEngine.cs`, `RecommendationExplanationEngine.cs`, `VoiceKnowledgeGraphBuilder.cs`, `ExerciseEffectivenessEngine.cs` | FemVoiceStudio | SmartCoach | FemVoice.Core | Models, ILocalizationService, SessionAnalyticsStore | Low | `SmartCoach*Tests`, `ExerciseRecommendationEngineTests`, `ExerciseEffectivenessEngineTests` | Mappers stay with FeedbackPipeline |
| `Services/VocalHealthSupervisor.cs`, `VocalHealthBaselineProvider.cs`, `HydrationAdvisor.cs`, `RecoveryScorer.cs`, `RecoveryIntelligenceService.cs`, `RecoveryActivationPolicy.cs`, `ProgressionSafetyGate.cs`, `SafeFailureMessages.cs`, `ClinicalLanguagePolicy.cs` | FemVoiceStudio | Health/safety/recovery gates | FemVoice.Core | Models | Low | `VocalHealthSupervisorTests`, `HydrationAdvisorTests`, `RecoveryScorerTests`, `RecoveryIntelligenceServiceTests`, `ProgressionSafetyGateTests`, `SafetyOverrideInvariantTests` | **Do not modify behaviour** |
| `Services/ProgressionOrchestrator.cs`, `ProgressionService.cs`, `Progression/*`, `MasteryEvaluator.cs`, `ComfortZoneController.cs`, `AdaptiveComfortZoneService.cs`, `PeriodizationService.cs`, `AdaptiveDifficultyService.cs` | FemVoiceStudio | Progression | FemVoice.Core | Models, DatabaseService | Low | `ProgressionOrchestratorTests`, `MasteryEvaluatorTests`, `ProgressionAuthorityTests`, `RecoveryAwareTargetZoneTests` | — |
| `Services/FeedbackPipeline.cs` (+ 6 mappers), `FeedbackConsistencyGuard.cs`, `FeedbackService.cs`, `FeedbackRuleEngine/*` | FemVoiceStudio | Feedback prioritization/suppression | FemVoice.Core | Models, ILocalizationService | Low | `FeedbackConsistencyGuardTests`, `FeedbackPriorityMatrixTests`, `InlineCoachPolicyTests`, `MainScreenFeedbackMapperTests` | All mappers are in this one file |
| `Services/VoiceFeminizationExerciseService.cs`, `ExerciseProfileFactory.cs`, `ExerciseTextService.cs`, `ExerciseIntelligenceCoordinator.cs` | FemVoiceStudio | Exercise catalog + coordination | FemVoice.Core | Models, ResonanceProxyEngine, FemVoiceScoreEngine, ComfortZoneController | Low–Med | `ExerciseCatalogCoverageTests`, `ExerciseProfileFactoryTests`, `GuidanceCompletenessTests`, `ExerciseDetailViewModelTests` | Coordinator wires DSP engines — keep DSP portable |
| `Models/**` (incl. `VoiceLoad/`) | FemVoiceStudio | Domain DTOs | FemVoice.Core | — | Low | (covered indirectly) | One soft note: `Models/ScoreSnapshot.cs` has an OxyPlot **comment** only |

### To `FemVoice.Audio.Abstractions` / `FemVoice.Audio.Windows`

| Current file/path | Responsibility | Proposed target | Dependencies | Risk | Required tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Audio/PitchDetectionService.cs`, `AdaptivePitchDetector.cs`, `FormantDetectionService.cs`, `VoiceActivityDetector.cs`, `VocalWeightAnalyzer.cs`, `VoiceStrainDetector.cs`, `SpeechRateAnalyzer.cs`, `VoiceMetricsCalculator.cs`, `ResonansScoringService.cs`, `ResonanceProxyEngine.cs` (DSP), `Services/PitchTraceStabilizer.cs`, `PitchTargetZonePolicy.cs`, `ZoneConfiguration.cs`, `LiveMetricsService.cs`, `SpectrogramResonanceMapper.cs`, `MicrophoneCalibrationService.cs`, `MicrophoneCalibrationProfile.cs`, `AudioCaptureDiagnostics.cs` | Pure DSP/analysis | FemVoice.Audio.Abstractions (or Core) | NAudio FFT `Complex` only | Low | `PitchDetectionServiceTests`, `FormantDetectionServiceTests`, `ResonanceProxyEngineTests`, `VocalWeightAnalyzerTests`, `PitchTraceStabilizerTests`, `PitchTargetZonePolicyTests`, `MicrophoneCalibrationServiceTests`, `SpectrogramResonanceMapperTests` | Consider a portable FFT to drop the NAudio dependency entirely |
| `Audio/AudioCaptureService.cs`, `AudioAnalysisEngine.cs` (capture half), `AudioAnalyzerService.cs` | Mic capture | FemVoice.Audio.Windows behind `IAudioCaptureService` | NAudio (WASAPI/WaveIn/MMDevice) | **High** | `AudioCaptureServiceTests`, `AudioSafetyTests` | Define `IAudioCaptureService` (float-frame events + device enum); this is the cross-platform boundary |

### To `FemVoice.Reports`, `FemVoice.Localization`, `FemVoice.Diagnostics`

| Current file/path | Responsibility | Proposed target | Risk | Required tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `Services/ReportAssembler.cs`, `ExportWriter.cs`, `ReportTextSanitizer.cs`, `ReportVerificationTracker.cs`, `Models/ProfessionalReports.cs` | Report assembly + PDF/CSV/JSON | FemVoice.Reports | Low | `ReportAssemblerTests`, `ExportWriterTests`, `ReportLocalizationTests`, `ReportVerificationTrackerTests` | QuestPDF stays; SaveFileDialog stays in the UI head behind `IFileDialogService` |
| `Services/LocalizationService.cs`, `Interfaces/ILocalizationService.cs`, `Resources/Strings*.resx`, `ExerciseGuideTextLocalizer.cs` | Localization core + resources | FemVoice.Localization | Low–Med | `ReportLocalizationTests`, `NewLanguageResourcesTests`, `LocalizationAccessibilityRobustnessTests`, `ProfessionalResxPolicyTests` | Fix RESX naming (`String.pt-BR`, `Strings_en`) before/while moving; XAML `LocExtension`/`LocConverter` re-implemented in the Avalonia head |
| `Services/Rc0*.cs`, `DiagnosticsNaming.cs`, `SupportPackageService.cs`, `PrivacyConsentPolicy.cs`, `ParticipantTokenProvider.cs`, `ResearchAnonymizer.cs`, `ResearchAggregator.cs`, `PilotReadinessChecker.cs`, `AuditTrailStore.cs`, `Models/ResearchDataset.cs`, `AuditEvent.cs` | Evidence/diagnostics/research | FemVoice.Diagnostics | Low | `Rc0EvidenceExporterSignalClassificationTests`, `DiagnosticsNamingTests`, `ResearchAnonymizerTests`, `ResearchNoPiiTests`, `PilotReadinessCheckerTests`, `AuditTrailStoreTests` | Abstract the settings-path source so it doesn't pull in WPF `ThemeManager` |

### Stays in the UI head (per-platform; not extracted)

`Views/**`, `Themes/**`, `Resources/Icons.xaml`, `App.xaml(.cs)`, `ThemeManager.cs`, `Services/AnalysisChartTheme.cs` (brush-reading half), `Services/IconProvider.cs`, `Converters/**` (re-shelled), `RelayCommand.cs`, and the WPF-coupled portions of `MainViewModel`/`ExerciseDetailViewModel`/`SmartCoachViewModel`. The `OxyPlot`-building VMs are shared once the package is swapped.

## Pre-port cleanup checklist (recommended, not yet done)

1. Decide fate of dead `Subsystems/**`, `Infra/DependencyInjection.cs`, `ViewModelBase`/`SubsystemViewModelBase` (do **not** port).
2. Remove test packages + `Tests/**` from the main app project; consolidate `FemVoiceScoreTests.cs` duplication.
3. Fix RESX naming so pt-BR loads (`String.pt-BR.resx` → `Strings.pt-BR.resx`); remove/merge `Strings_en.resx`; resolve the missing `Strings.Designer.cs` declaration.
4. Confirm whether `RealtimeAnalysisEngine`/`AsyncAudioPipeline`/`AudioAnalysisEngine_new.cs` are truly unused; consolidate the pitch engines.
5. Verify whether `DatabaseSchema.sql`/migration 001 run; fix the invalid `ADD COLUMN IF NOT EXISTS` or mark dormant.
6. Standardize on CommunityToolkit.Mvvm; retire the hand-rolled `ViewModelBase`/`RelayCommand`.
7. Decide cross-platform DB path strategy (`MyDocuments` resolves differently off Windows).

Each item is a **recommendation**; none were applied in this audit.

## Highest-risk items for the port (ranked)

1. **Audio capture (NAudio, Windows-only)** — define `IAudioCaptureService`; the cross-platform backend is the biggest unknown.
2. **Theme system** (ResourceDictionary swap + pack URIs + Registry read) — full rewrite.
3. **OxyPlot.Wpf → OxyPlot.Avalonia** + the `AnalysisChartTheme` brush bridge.
4. **Real-time UI marshalling** embedded as `Application.Current.Dispatcher` in three VMs — abstract behind `IUiDispatcher`.
5. **File dialogs / MessageBox / system-theme detection** — abstract behind services.
6. **Startup/splash** (`DispatcherFrame.PushFrame`) — rework.
