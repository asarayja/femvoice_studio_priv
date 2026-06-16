# FemVoice Studio — Current Feature Matrix (WPF Baseline)

Audit date: 2026-06-16 · Read-only. Status reflects what is wired and live in the WPF app today.

Status legend: **CONFIRMED** (live + wired), **PARTIAL** (present but limited/approximate/partial wiring), **LEGACY/DEAD** (present but not wired into the active app), **OUTDATED** (prior docs misdescribed it).

## 1. App shell & navigation

| Feature | Status | Key files |
| --- | --- | --- |
| Splash + startup + first-time setup | CONFIRMED | `App.xaml.cs`, `Views/FirstTimeSetupWindow.xaml`, `Services/FirstTimeSetupService.cs`, `ThemeManager.cs` |
| Main dashboard (start/stop session, pitch graph, comfort zone, stability, health indicator, feedback, exercise text, shortcuts) | CONFIRMED | `Views/MainWindow.xaml(.cs)`, `ViewModels/MainViewModel.cs` |
| Navigation to calendar/statistics/exercise guide/analyzer/SmartCoach/resonance/progression/analysis/settings | CONFIRMED | `MainWindow.xaml(.cs)` |
| Professional Tools row (Clinician, Coach, Report Export, Manual Override, Case Review) | CONFIRMED | `MainWindow`, the respective Windows/VMs |
| Difficulty level (beginner/intermediate/advanced) affecting text, progression, pitch target zone | CONFIRMED | `MainViewModel.cs`, `PitchTargetZonePolicy.cs`, `ProgressionService.cs` |
| Modeless helper windows / modal destructive confirmations | CONFIRMED | `MainWindow.xaml.cs`, `WindowModalBehaviorTests.cs` |

## 2. Exercise guide

| Feature | Status | Key files |
| --- | --- | --- |
| Exercise catalog: **15 exercises**, hardcoded (seeded into SQLite by `ExerciseDataService`; a parallel hardcoded list in `VoiceFeminizationExerciseService`) | CONFIRMED | `Data/ExerciseDataService.cs`, `Services/VoiceFeminizationExerciseService.cs`, `Models/Exercise.cs`, `ExerciseDefinition.cs` |
| Exercise guide window (filter/category: pitch, resonance, intonation, breathing, practice/combined) | CONFIRMED | `Views/ExerciseWindow.xaml(.cs)`, `ViewModels/ExerciseListViewModel.cs` |
| Exercise detail (goals, instructions, guidance, live feedback, timer, hold progress, subjective report) | CONFIRMED | `ExerciseWindow`, `ViewModels/ExerciseDetailViewModel.cs` |
| Localized exercise/guide text bank | CONFIRMED | `ExerciseTextService.cs`, `ExerciseGuideTextLocalizer.cs`, `Models/ExerciseText.cs`, RESX |
| Guidance system (purpose, physical focus, common errors, safety, threshold strategy, indicator package) | CONFIRMED | `ExerciseDetailViewModel.cs`, `Models/ExerciseTargetProfile.cs`, `IndicatorPackage.cs` |
| Exercise live feedback (resonance, pitch, stability, intensity, hold progress, safety, inline coach) | CONFIRMED | `Services/ExerciseIntelligenceCoordinator.cs`, `Models/ExerciseLiveState.cs` |
| Hold-progress + safety freeze/stop | CONFIRMED | `ExerciseIntelligenceCoordinator.cs`, `ExerciseSessionTimerState.cs` |
| Subjective report (comfort/fatigue/pressure/motivation) → adaptive progression | CONFIRMED | `ExerciseWindow.xaml.cs`, `Models/SubjectiveReport.cs`, `ProgressionOrchestrator.cs` |
| ~~ExerciseSummaryView / LiveFeedbackView~~ | OUTDATED — **these files do not exist**; summary/live feedback are rendered inline in `ExerciseWindow.xaml.cs` + `SessionAnalyticsStore` | — |

## 3. Audio / biofeedback (see [`CURRENT_AUDIO_PIPELINE.md`](CURRENT_AUDIO_PIPELINE.md))

| Feature | Status | Key files |
| --- | --- | --- |
| Audio capture (WaveIn primary; WASAPI→WaveIn fallback in `AudioAnalysisEngine`) | CONFIRMED | `Audio/AudioCaptureService.cs`, `AudioAnalysisEngine.cs`, `AudioAnalyzerService.cs` |
| Realtime analysis (pitch, volume, spectrum, live metrics on background thread) | CONFIRMED | `RealtimeAnalysisEngine.cs` (note: appears LEGACY), `AudioAnalyzerService.cs`, `AsyncAudioPipeline.cs` (appears unused) |
| Pitch detection (YIN) + stabilization | CONFIRMED | `PitchDetectionService.cs`, `AdaptivePitchDetector.cs`, `PitchTraceStabilizer.cs`, `VoiceActivityDetector.cs` |
| Pitch target zone (level/profile-derived, 150–240 Hz clamp) | CONFIRMED | `PitchTargetZonePolicy.cs`, `ZoneConfiguration.cs` |
| Resonance analysis (LPC formants + FFT proxy score) | CONFIRMED | `ResonanceProxyEngine.cs`, `FormantDetectionService.cs`, `ResonansScoringService.cs` |
| Spectrogram intelligence (resonance/formant overlay, tone category, clinical resonance score) | CONFIRMED | `AnalyzerWindow.xaml.cs`, `SpectrogramResonanceMapper.cs` |
| FemVoice score signals (pitch/resonance/stability/intonation/comfort) | CONFIRMED | `FemVoiceScoreEngine.cs`, `FemVoiceScore.cs`, `Models/VoiceMetrics.cs` |
| Comfort zone (stability/score-history-driven expansion/regression) | CONFIRMED | `ComfortZoneController.cs`, `AdaptiveComfortZoneService.cs`, `ComfortZoneState.cs` |
| Vocal weight / strain / speech-rate analyzers | PARTIAL (some stubbed metrics: jitter/shimmer=0, approximate HNR/stddev) | `VocalWeightAnalyzer.cs`, `VoiceStrainDetector.cs`, `SpeechRateAnalyzer.cs` |
| Microphone calibration + per-device profiles + signal advice + hear-own-voice | CONFIRMED | `MicrophoneCalibrationService.cs`, `MicrophoneCalibrationProfile.cs`, `MicrophoneCalibrationWindow` |

## 4. SmartCoach, learning & feedback

| Feature | Status | Key files |
| --- | --- | --- |
| SmartCoach (daily recommendation, focus area, goal status, weekly history, confidence) | CONFIRMED | `SmartCoachEngine.cs`, `ViewModels/SmartCoachViewModel.cs`, `Views/SmartCoachDetailView.xaml` |
| Recommended exercise (effectiveness-ranked; health/recovery can always tighten) | CONFIRMED | `SmartCoachEngine.cs`, `ExerciseRecommendationEngine.cs`, `ExerciseEffectivenessEngine.cs` |
| Learning path (personal stage from goals/history/complexity) | CONFIRMED | `LearningPathProfileBuilder.cs`, `Progression/ComplexityEngine.cs` |
| SmartCoach memory (persisted advice + outcomes) | CONFIRMED | `SmartCoachMemoryStore.cs`, `Models/SmartCoachAdviceEntry.cs` |
| Voice knowledge graph | CONFIRMED | `VoiceKnowledgeGraphBuilder.cs`, `Models/VoiceKnowledgeGraph.cs` |
| Inline coach (short contextual messages) | CONFIRMED | `Models/InlineCoachMessage.cs`, `ExerciseIntelligenceCoordinator.cs`, `InlineCoachFeedbackMapper` (in `FeedbackPipeline.cs`) |
| FeedbackPipeline + 6 mappers (SmartCoach/InlineCoach/Progression/Hydration/VocalHealth/MainScreen) | CONFIRMED — **all mapper classes live inside `Services/FeedbackPipeline.cs`** | `FeedbackPipeline.cs` |
| FeedbackConsistencyGuard (priority + suppression matrix + rate limiting) | CONFIRMED | `FeedbackConsistencyGuard.cs` (`FeedbackPriority` enum) |
| Legacy feedback service (main dashboard session text) | CONFIRMED | `FeedbackService.cs` |
| ~~CoachMessageGenerator / CoachMessageFormatter / SmartCoachExerciseAdapter / ExerciseEffectivenessProvider~~ | OUTDATED — **do not exist** (the real engine is `ExerciseEffectivenessEngine`) | — |

## 5. Health, safety & recovery (priority hierarchy enforced here)

| Feature | Status | Key files |
| --- | --- | --- |
| VocalHealthSupervisor (strain/fatigue/pause/recovery/safety state machine Normal→Caution→Restrict→Lock) | CONFIRMED | `VocalHealthSupervisor.cs`, `VocalHealthBaselineProvider.cs` |
| Health safety states (caution/restrict/lock can stop/limit exercise) | CONFIRMED | `VocalHealthSupervisor.cs`, `Models/ExerciseLiveState.cs`, `SafetyLockTests.cs` |
| HydrationAdvisor (soft signal only; never a gate; 120 s min, max 3/session) | CONFIRMED | `HydrationAdvisor.cs` |
| RecoveryScorer (reactive 0–100) | CONFIRMED | `RecoveryScorer.cs` |
| RecoveryIntelligenceService (predictive forecast: debt, ACWR, severity; never weakens safety) | CONFIRMED | `RecoveryIntelligenceService.cs` |
| ProgressionSafetyGate (5 blocking rules over 14-day history) | CONFIRMED | `ProgressionSafetyGate.cs` |
| Recovery-aware target zones (recovery can only shrink, never expand) | CONFIRMED | `ComfortZoneController.cs`, `ProgressionOrchestrator.cs`, `RecoveryActivationPolicy.cs` |
| StressSensitiveMode / ReducedVisualFeedback (dampens presentation only; never hides safety) | CONFIRMED | `StressSensitiveExperience.cs`, `Models/UserVoiceProfile.cs` |
| Safety-copy policy (RESX language guards) | CONFIRMED | `ResourceTextPolicyTests.cs`, `ProfessionalResxPolicyTests.cs`, `ClinicalLanguagePolicy.cs` |
| `VoiceHealthService` / `HealthStatus` | LEGACY/NEEDS REVIEW — appear orphaned from the active gate flow | `VoiceHealthService.cs`, `HealthStatus.cs` |

**Priority hierarchy — CONFIRMED.** "Safety > Health > Recovery > Comfort > Voice Development > Reporting" is documented across the codebase and machine-enforced primarily by the `FeedbackPriority` enum (`ProgressionUpdate < PerformancePraise < TechniqueCorrection < HydrationSuggestion < PauseRecommendation < ActiveStrainAlert < HealthWarning < SafetyFreeze`) + `FeedbackConsistencyGuard`'s suppression matrix, and by `ProgressionSafetyGate`/`ProgressionOrchestrator`/`RecoveryIntelligenceService`/`MasteryEvaluator`. Descriptive/reporting engines explicitly never override gates.

## 6. Progression, analytics & personalization

| Feature | Status | Key files |
| --- | --- | --- |
| SessionAnalyticsStore (session data, exercise summaries, health/hydration events; no raw audio) | CONFIRMED | `SessionAnalyticsStore.cs`, `Models/SessionInsight.cs` |
| ExerciseSessionRecorder (records completed exercises → analytics/health) | CONFIRMED | `ExerciseSessionRecorder.cs` |
| MasteryEvaluator (mastery over time; safety lock in 7 days forces demotion) | CONFIRMED | `MasteryEvaluator.cs`, `Models/MasteryLevel.cs` |
| ProgressionOrchestrator (keep/adapt/pause/regress after session) | CONFIRMED | `ProgressionOrchestrator.cs`, `Models/ProgressionSessionData.cs` |
| ExerciseProfileStore (personal profile tweaks in SQLite) | CONFIRMED | `ExerciseProfileStore.cs` |
| Exercise effectiveness (per-exercise ranking for SmartCoach) | CONFIRMED | `ExerciseEffectivenessEngine.cs`, `Models/ExerciseEffectivenessProfile.cs` |
| Trend engine + longitudinal insight (7/30/90/180-day OLS windows) | CONFIRMED | `TrendEngineService.cs`, `LongitudinalInsightEngine.cs`, `Models/TrendWindow.cs` |
| Pattern detector (plateau/breakthrough/regression) | CONFIRMED | `VoicePatternDetector.cs`, `Models/VoicePatternEvents.cs` |
| Progression dashboard | CONFIRMED | `Views/ProgressionWindow.xaml`, `ProgressionDashboard.xaml`, `ViewModels/ProgressionDashboardViewModel.cs` |
| Calendar / statistics (history, day details, streak, totals, score) | CONFIRMED | `CalendarWindow`, `DayDetailsWindow`, `StatisticsWindow` |
| Voice goal profile (goals, style preference, focus dimension) | CONFIRMED | `Models/VoiceGoalProfile.cs`, `UserVoiceProfile.cs`, `LocalVoiceGoalProfileStore.cs` |
| Periodization / adaptive difficulty | CONFIRMED | `PeriodizationService.cs`, `AdaptiveDifficultyService.cs` |
| Settings (theme, language, goal profile, accessibility, mic calibration, hear-own-voice, DB reset) | CONFIRMED | `SettingsWindow`, `ThemeManager.cs`, `LocalizationService.cs` |
| Theme (light/dark/system) | CONFIRMED | `Themes/*.xaml`, `ThemeManager.cs` |

## 7. Analysis windows

| Feature | Status | Key files |
| --- | --- | --- |
| Analyzer (audio/spectrogram, resonance status, clinical score, debug panel) | CONFIRMED | `AnalyzerWindow.xaml(.cs)`, `SpectrogramResonanceMapper.cs` |
| Resonance window (start/stop/reset, F1/F2 position, formant timeline, themed bounded zoom/pan) | CONFIRMED | `ResonanceWindow.xaml(.cs)`, `ResonanceChartViewModel.cs`, `AnalysisChartTheme.cs` |
| Analysis window (Dybdeanalyse charts, ~14 PlotModels, shared chart theme) | CONFIRMED | `AnalysisWindow.xaml(.cs)`, `AnalysisPageViewModel.cs` |

## 8. Professional tools & reporting (see [`CURRENT_REPORTS_AND_LOCALIZATION.md`](CURRENT_REPORTS_AND_LOCALIZATION.md))

| Feature | Status | Key files |
| --- | --- | --- |
| Clinician dashboard | CONFIRMED | `ClinicianDashboardWindow`, `ClinicianDashboard`, `ClinicianDashboardViewModel.cs` |
| Coach dashboard | CONFIRMED | `CoachDashboardWindow`, `CoachDashboard`, `CoachDashboardViewModel.cs` |
| OutcomeProfile (goal progress, recovery, effectiveness, long-term dev) | CONFIRMED | `OutcomeProfileBuilder.cs`, `OutcomeProfileStore.cs`, `Models/OutcomeProfile.cs` |
| Report export (4 types: Clinical/Coach/Outcome/Timeline; 3 formats: PDF/CSV/JSON) | CONFIRMED | `ReportExportWindow`, `ReportExportViewModel.cs`, `ReportAssembler.cs`, `ExportWriter.cs` |
| Clinical notes (stored separately) | CONFIRMED | `ClinicalNotesStore.cs`, `Models/ClinicalNote.cs` |
| Audit trail (append-only) | CONFIRMED | `AuditTrailStore.cs`, `Models/AuditEvent.cs` |
| Manual override (clamped by recovery/safety) | CONFIRMED | `ManualOverrideWindow`, `ManualOverrideViewModel.cs`, `ManualOverrideEngine.cs` |
| Case review (assemble/store from outcome snapshots) | CONFIRMED | `CaseReviewWindow`, `CaseReviewAssembler.cs`, `CaseReviewsStore.cs`, `Models/CaseReview.cs` |
| Pilot readiness checker | CONFIRMED | `PilotReadinessChecker.cs` |

## 9. Research & anonymization (see [`CURRENT_DIAGNOSTICS_AND_EVIDENCE.md`](CURRENT_DIAGNOSTICS_AND_EVIDENCE.md))

| Feature | Status | Key files |
| --- | --- | --- |
| Participant token (opaque per-install) | CONFIRMED | `ParticipantTokenProvider.cs` |
| Research anonymizer (drops UserId/device/free-text/time-of-day) | CONFIRMED | `ResearchAnonymizer.cs`, `Models/ResearchDataset.cs` |
| Research aggregator (cohort aggregates; N<5 caveat) | CONFIRMED | `ResearchAggregator.cs` |
| No-PII policy tests | CONFIRMED | `ResearchNoPiiTests.cs`, `ResearchAnonymizerTests.cs` |

## 10. Diagnostics / evidence

| Feature | Status | Key files |
| --- | --- | --- |
| RC-0 evidence pipeline (PASS/WARNING/FAIL/BLOCKED) | CONFIRMED | `Rc0EvidenceExporter.cs`, `Rc0StartupBootstrap.cs`, `Rc0RuntimeLog.cs`, `Rc0WriteFailureSink.cs`, `DiagnosticsNaming.cs` |
| Support package export (.zip, privacy-filtered) | CONFIRMED | `SupportPackageService.cs`, `PrivacyConsentPolicy.cs` |

## 11. Data & persistence

| Feature | Status | Key files |
| --- | --- | --- |
| SQLite data layer (shared `~/Documents/FemVoiceStudio/femvoice.db`) | CONFIRMED | `DatabaseService.cs`, `IDatabaseService.cs` |
| 8 SQLite-backed stores sharing the one DB (connection-string sharing) | CONFIRMED | `SessionAnalyticsStore`, `ExerciseProfileStore`, `SmartCoachMemoryStore`, `OutcomeProfileStore`, `ManualOverridesStore`, `ClinicalNotesStore`, `AuditTrailStore`, `CaseReviewsStore` |
| Local backup/restore (zip of settings + db + report-status + manifest) | CONFIRMED (WAL sidecars not included — NEEDS REVIEW) | `LocalBackupService.cs` |
| `DatabaseSchema.sql` / migration 001 | NEEDS REVIEW — appear dormant (not executed; migration has invalid SQLite) | `Resources/DatabaseSchema.sql`, `Data/migrations/001_*.sql` |

## 12. Tests / QA (see test overview in audit)

| Feature | Status |
| --- | --- |
| ~130 xUnit tests across scoring/safety/progression/recovery/health/calibration/feedback/RESX-policy/reports/research/exercise/theme/icon/localization/viewmodels/RC-0 | CONFIRMED |
| Tests are `net10.0-windows` (Windows-bound); ~10 files touch WPF types | CONFIRMED |
| 4 test files compiled into the main app (`FemVoiceStudio/Tests/`) | CONFIRMED (concern) |
