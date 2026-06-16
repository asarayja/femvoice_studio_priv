# Linux Test Classification (Phase L0)

Date: 2026-06-16 · Verified by actually building + running on Linux (.NET 10.0.301).

Total tests in `FemVoiceStudio.Tests/`: **131 files**. Split: **101 moved to `FemVoice.Tests.Portable` (net10.0, runs on Linux)**, **30 kept in `FemVoiceStudio.Tests` (net10.0-windows)**.

Live result on Linux (`FemVoice.Tests.Portable`): **1580 test cases, 1570 passed, 10 failed (all pre-existing — see §Needs review), 0 skipped.**

## Category: Portable — runs on Linux now (MOVED to FemVoice.Tests.Portable)
101 files. All build and run on Linux against `FemVoice.Core`. Includes the priority safety/clinical suites the prompt called out — **all green**:

| Test file | Can run on Linux now | Notes |
| --- | --- | --- |
| SafetyOverrideInvariantTests | ✅ yes | safety hierarchy invariant — PASS |
| SafetyPriorityEngineTests | ✅ yes | PASS |
| ManualOverrideClampTests | ✅ yes | override never weakens gates — PASS |
| FeedbackPriorityMatrixTests, FeedbackConsistencyGuardTests | ✅ yes | priority/suppression — PASS |
| ProgressionSafetyGateTests, ProgressionOrchestratorTests, ProgressionAuthorityTests | ✅ yes | PASS |
| RecoveryAwareTargetZoneTests, RecoveryScorerTests, RecoveryIntelligenceServiceTests, RecoveryFirstOrderingTests | ✅ yes | PASS |
| FemVoiceScoreTests, FemVoiceScoreEngineTests, ClinicalSessionScoreTests, VoiceIntelligenceScorerTests | ✅ yes | clinical scoring — PASS |
| SmartCoach*Tests (Decision, Frequency, Memory, Pipeline, GoalCoaching*, VoiceIntelligence*) | ✅ yes | PASS (2 SmartCoach resx-coverage cases were path-fixed) |
| ResearchAnonymizerTests, ResearchAggregatorTests, ResearchNoPiiTests | ✅ yes | PII-free — PASS |
| ReportAssemblerTests, ExportWriterTests, ReportVerificationTrackerTests | ✅ yes | 4×3 report output — PASS |
| MicrophoneCalibrationServiceTests, PitchTraceStabilizerTests, PitchTargetZonePolicyTests, PitchDetectionServiceTests, ResonanceProxyEngineTests, VocalWeightAnalyzerTests, SpectrogramResonanceMapperTests | ✅ yes | DSP — PASS |
| MasteryEvaluatorTests, ComfortZoneControllerTests, AdaptiveComfortZone*Tests, TrendEngineServiceTests, LongitudinalInsightEngineTests, VoicePatternDetectorTests | ✅ yes | PASS |
| Audit/Clinical/Hydration/VocalHealth/ExerciseEffectiveness/Periodization/etc. | ✅ yes | PASS |
| Localization/RESX coverage & policy tests (ClinicalLanguagePolicyTests, ProfessionalResxPolicyTests, ResourceTextPolicyTests, GuidanceCompletenessTests, ExerciseCatalogCoverageTests, SmartCoach*resx) | ✅ yes (path-fixed) | read RESX/source from FemVoice.Core now — PASS |

> The only test-code changes were **file-location path updates** (the moved code/RESX now live in `FemVoice.Core/`); **no assertions or logic were changed**.

## Category: Windows-only — requires WPF/Windows (KEPT in FemVoiceStudio.Tests, net10.0-windows)
30 files. Reference WPF types (Brush/Color/Application.Current/ResourceDictionary), ViewModels/Views, theme XAML files, or the WPF-stay services (ThemeManager, IconProvider, AnalysisChartTheme, FeedbackService, LocalBackupService, SupportPackageService, PrivacyConsentPolicy, Rc0StartupBootstrap) or NAudio capture:

`AdaptiveVolumeTests, AnalysisChartThemeTests, ClinicianDashboardViewModelTests, CoachDashboardViewModelTests, ExerciseDetailViewModelTests, ExerciseSessionRecorderTests, FrontPageProgressTests, IconRenderingTests, ManualOverrideIntegrationTests, ReleaseReadinessSmokeTests, ReportExportViewModelTests, SmartCoachStressSensitiveTests, StressSensitiveExperienceTests, VoiceIntelligenceVizTests, AudioCaptureServiceTests, AudioSafetyTests, FeedbackSignalPolicyTests, FormantDetectionServiceTests, LocalBackupServiceTests, PackagingReadinessTests, PrivacyConsentReadinessTests, SafeFailureHandlingTests, ProgressionIconThemeTests, ResonanceContrastDemoTests, SettingsWindowLayoutTests, ThemeButtonStyleTests, ThemeComboBoxStyleTests, ThemeNoteButtonStyleTests, ThemeResourceCoverageTests, WindowModalBehaviorTests`

Recommended action: keep as the Windows test head; run on Windows CI. Some (e.g. FormantDetectionServiceTests, FeedbackSignalPolicyTests, SafeFailureHandlingTests) test now-portable Core logic but reference a WPF-stay type — candidates to refactor onto Core abstractions later so they can move portable.

## Category: Audio-hardware — requires real mic / Windows capture
`AudioCaptureServiceTests`, `AudioSafetyTests` (in the Windows set). Exercise NAudio capture behaviour. Keep Windows-only; the future cross-platform capture backend needs its own harness + manual mic validation.

## Category: Avalonia-future — recreate after Avalonia UI exists
None yet. To create in later phases: Avalonia theme-variant tests, localization-markup tests, OxyPlot.Avalonia chart-hosting tests, dialog/file-dialog/dispatcher abstraction tests, navigation/view-parity tests. (Replaces the WPF theme/viewmodel tests above for the Avalonia head.)

## Category: Needs review — pre-existing failures (NOT caused by the port)
**10 failing cases**, proven pre-existing via git evidence (identical test code at `HEAD`, byte-identical RESX — I only `git mv`'d files):
- `NewLanguageResourcesTests.NewFile_PreservesPlaceholdersPipesAndGlobs` × 9 cultures (ar, cs-CZ, el-GR, hu-HU, nl-NL, pl-PL, ro-RO, tr-TR, uk-UA): key `Report_RecommendationHighFatigueFormat` has placeholders `{0} {0} {1:F1}` in the culture files (and in **English** `Strings.en.resx`) vs `{0} {1:F1}` in the neutral `Strings.resx`. Longstanding localization-data inconsistency. **Not editable here** (hard rule: do not change localization resources).
- `ExerciseGuideEncodingTests.ResourceFiles_NoMojibake_All12Resx`: asserts exactly 12 resx files, but the repo has 21 (18 cultures + neutral + the two mis-named `Strings_en.resx`/`String.pt-BR.resx`). The "12" expectation went stale when 9 languages were added. **Test-expectation staleness**, not a port regression.

Recommended action (separate, approved change): fix the `Report_RecommendationHighFatigueFormat` placeholders in the affected resx, and update the `All12Resx` expected count — both are pre-existing product/test issues, out of scope for the port.
