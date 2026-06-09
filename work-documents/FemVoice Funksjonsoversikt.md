# FemVoice Funksjonsoversikt

Status: 2026-06-09

Formål: praktisk oversikt over hvilke hovedfunksjoner FemVoice Studio har nå, hva de gjør, og hvor de bor i koden. Dette er en produkt- og arkitekturoversikt basert på aktiv WPF-kode, DI-wiring og tester. Det er ikke en klinisk validering eller cleanup-plan.

## Kort status

FemVoice Studio er en lokal WPF/.NET-app for stemmetrening med sanntids lydanalyse, øvelsesguide, progressiv trening, SmartCoach, helse-/recovery-gating, rapportering og profesjonelle verktøy. Appen bruker NAudio til mikrofon/lyd, OxyPlot til pitch-graf, SQLite til lokal persistens, RESX til lokalisering og QuestPDF til PDF-eksport.

Aktiv oppstart og DI ligger primært i `FemVoiceStudio/App.xaml.cs`. `Subsystems/*` og `Infra/DependencyInjection.cs` finnes fortsatt, men ser ut som eldre/parallelle arkitekturlag som må merge-audites før cleanup.

## Hovedapp og navigasjon

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Splash og oppstart | Viser splash, kjører førstegangsoppsett ved behov, initialiserer tema/debug og åpner hovedvinduet. | `App.xaml.cs`, `Views/FirstTimeSetupWindow.xaml`, `Services/FirstTimeSetupService.cs`, `Services/ThemeManager.cs` |
| Hoveddashboard | Start/stopp vanlig økt, viser pitch-graf, comfort-zone, stabilitet, health-indikator, feedback, øvelsestekst, session-status og snarveier. | `Views/MainWindow.xaml`, `Views/MainWindow.xaml.cs`, `ViewModels/MainViewModel.cs` |
| Hovednavigasjon | Åpner kalender, statistikk, øvelsesguide, analyzer, SmartCoach, resonansvindu, progresjon, analyse og innstillinger. | `MainWindow.xaml`, `MainWindow.xaml.cs` |
| Professional Tools-rad | Åpner kliniker-dashboard, coach-dashboard, rapporteksport, manual override og case review fra egen navigasjonsrad. | `MainWindow.xaml`, `MainWindow.xaml.cs`, `Views/ClinicianDashboardWindow.xaml`, `Views/CoachDashboardWindow.xaml`, `Views/ReportExportWindow.xaml`, `Views/ManualOverrideWindow.xaml`, `Views/CaseReviewWindow.xaml` |
| Start/stopp session | Starter mikrofonanalyse, live pitch/resonansmåling, timer og feedback. Stopper analyse, lagrer resultat og oppdaterer brukerdata. | `MainViewModel.cs`, `AudioAnalyzerService.cs`, `AudioAnalysisEngine.cs`, `FeedbackService.cs`, `ProgressionService.cs` |
| Vanskelighetsnivå | Lar brukeren velge nybegynner, middels eller avansert. Påvirker øvelsestekst, progresjonsvisning og pitch target-zone. | `MainViewModel.cs`, `PitchTargetZonePolicy.cs`, `ProgressionService.cs` |
| Pitch-graf | Viser stabilisert pitch-trace og comfort-zone i OxyPlot. UI skal vise komfortsone, ikke talljag mot høyest mulig pitch. | `MainWindow.xaml.cs`, `PitchTraceStabilizer.cs`, `PitchTargetZonePolicy.cs`, `PitchChartAxisRangeCalculator.cs` |
| Sanntidsfeedback | Gir korte meldinger om signal, pitch, resonans, stabilitet, comfort og helse/recovery. | `FeedbackService.cs`, `FeedbackPipeline.cs`, `FeedbackConsistencyGuard.cs`, `Resources/Strings*.resx` |

## Øvelsesguide

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Exercise Guide | Viser øvelser filtrert/kategorisert etter pitch, resonans, intonasjon, pust og praksis/combined. | `Views/ExerciseWindow.xaml`, `Views/ExerciseWindow.xaml.cs`, `Services/VoiceFeminizationExerciseService.cs` |
| Øvelseskatalog | Har 15 kjerneøvelser med måltype, kategori, vanskelighet, varighet og treningsinstruksjoner. | `VoiceFeminizationExerciseService.cs`, `Models/Exercise.cs`, `Models/ExerciseDefinition.cs` |
| Øvelsestekster | Har større tekstbank for basic/intermediate/advanced practice-tekster med lokalisering. | `Services/ExerciseTextService.cs`, `Models/ExerciseText.cs`, `Resources/Strings*.resx` |
| Øvelsesdetalj | Viser mål, instruksjoner, guidance, live feedback, timer, status, hold progress og subjektiv rapport etter stopp. | `ExerciseWindow.xaml`, `ExerciseWindow.xaml.cs`, `ViewModels/ExerciseDetailViewModel.cs` |
| Guidance-system | Viser hensikt, fysisk fokus, vanlige feil, sikkerhetsinfo, terskelstrategi og indikatorpakke for valgt øvelse. | `ExerciseDetailViewModel.cs`, `Models/ExerciseTargetProfile.cs`, `Models/IndicatorPackage.cs`, `Resources/Strings*.resx` |
| Exercise live feedback | Samler resonans, pitch, stabilitet, intensitet, hold progress, safety og inline coach under en øvelse. | `Services/ExerciseIntelligenceCoordinator.cs`, `Models/ExerciseLiveState.cs`, `ViewModels/ExerciseDetailViewModel.cs` |
| Hold progress | Måler om brukeren holder riktig måltilstand lenge nok, og fryser/stopper ved safety lock eller feil måltilstand. | `ExerciseIntelligenceCoordinator.cs`, `ExerciseSessionTimerState.cs`, `ExerciseDetailViewModel.cs` |
| Subjektiv rapport | Etter stopp kan brukeren rapportere comfort, fatigue, pressure og motivasjon før adaptiv progresjon vurderes. | `ExerciseWindow.xaml.cs`, `Models/SubjectiveReport.cs`, `Services/ProgressionOrchestrator.cs` |
| Øvelsessammendrag | UserControl/VM for oppsummering av pitch, resonans, stabilitet og anbefalt neste steg. Ser ut som aktiv/nyere UX-kandidat, men bør wiring-audites. | `Views/ExerciseSummaryView.xaml`, `Views/ExerciseSummaryViewModel.cs`, `Services/SmartCoachExerciseAdapter.cs` |

## Lydanalyse og biofeedback

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Audio capture | Leser mikrofoninput, bruker WASAPI når mulig og WaveIn fallback der det trengs. | `Audio/AudioCaptureService.cs`, `Audio/AudioAnalysisEngine.cs` |
| Realtime analysis | Prosesserer lydframes til pitch, volum, spektrum og live metrics på bakgrunnstråd. | `Audio/RealtimeAnalysisEngine.cs`, `Audio/AudioAnalyzerService.cs`, `Audio/AsyncAudioPipeline.cs` |
| Pitch detection | Beregner tonehøyde og filtrerer ugyldige/ustabile frames. | `Audio/PitchDetectionService.cs`, `Audio/AdaptivePitchDetector.cs`, `Audio/VoiceActivityDetector.cs` |
| Pitch trace stabilisering | Korrigerer sannsynlige harmoniske hopp og avviser ekstreme spikes før graf/feedback. | `Services/PitchTraceStabilizer.cs` |
| Pitch target-zone | Beregner trygg/komfortabel pitch-zone ut fra nivå og profil. | `Services/PitchTargetZonePolicy.cs`, `Services/ZoneConfiguration.cs` |
| Resonansanalyse | Bruker formanter, spectral features og proxy-score for å anslå resonansplassering/styrke. | `Audio/ResonanceProxyEngine.cs`, `Audio/FormantDetectionService.cs`, `Audio/ResonansScoringService.cs` |
| Spectrogram intelligence | Viser resonans/formant-overlay, tonekategori og klinisk resonansscore i analyzer. | `Views/AnalyzerWindow.xaml.cs`, `Services/SpectrogramResonanceMapper.cs` |
| FemVoice score | Lager normaliserte score-signaler for pitch, resonans, stabilitet, intonasjon og comfort. | `Services/FemVoiceScoreEngine.cs`, `Services/FemVoiceScore.cs`, `Models/VoiceMetrics.cs` |
| Comfort zone | Vurderer stabilitet, scorehistorikk og ekspansjon/regresjon av trygge treningsgrenser. | `Services/ComfortZoneController.cs`, `Services/AdaptiveComfortZoneService.cs`, `Services/ComfortZoneState.cs` |
| Vocal weight / strain proxy | Har egne analysatorer for vocal weight, strain, speech rate og health-signaler som kan brukes i safety/feedback. | `Audio/VocalWeightAnalyzer.cs`, `Audio/VoiceStrainDetector.cs`, `Audio/SpeechRateAnalyzer.cs`, `Services/VocalHealthSupervisor.cs` |

## Mikrofon og hardware

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Mikrofonkalibrering | Måler stille rom, tale/humming, noise floor, gain, clipping og anbefalte terskler. | `Views/MicrophoneCalibrationWindow.xaml`, `Audio/MicrophoneCalibrationService.cs` |
| Per-device profiler | Lagrer kalibreringsprofil per mikrofon/device-type med kvalitet, SNR og RMS-terskler. | `Audio/MicrophoneCalibrationProfile.cs`, `MicrophoneCalibrationService.cs` |
| Signalråd | Skiller tekniske mikrofonproblemer fra stemmefeedback, for eksempel lav output, støy, clipping eller noise gate. | `MicrophoneCalibrationService.cs`, `AudioAnalyzerService.cs`, `Views/MicrophoneCalibrationWindow.xaml.cs` |
| Hear own voice | Innstilling for mikrofon-monitorering. Skal ikke være aktiv når funksjonen er av. | `Views/SettingsWindow.xaml`, `ViewModels/MainViewModel.cs`, `AudioAnalyzerService.cs` |

## SmartCoach, læring og feedback

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| SmartCoach | Gir daglig anbefaling, fokusområde, målstatus, ukentlig historikk, confidence og meldinger basert på historikk, målprofil, recovery og safety. | `Services/SmartCoachEngine.cs`, `ViewModels/SmartCoachViewModel.cs`, `Views/SmartCoachDetailView.xaml` |
| Anbefalt øvelse | SmartCoach kan løfte en konkret anbefalt katalogøvelse og foreslå treningsvolum, men health/recovery kan alltid stramme inn. | `SmartCoachEngine.cs`, `SmartCoachViewModel.cs`, `ExerciseRecommendationEngine.cs`, `ExerciseEffectivenessEngine.cs` |
| Learning path | Bygger personlig læringsfase/stage ut fra mål, historikk og kompleksitet. | `Services/LearningPathProfileBuilder.cs`, `Models/LearningPathProfile.cs`, `Services/Progression/ComplexityEngine.cs` |
| SmartCoach memory | Persisterer coach-råd og utfall for å unngå kortsiktig/glemsk coaching. | `Services/SmartCoachMemoryStore.cs`, `Models/SmartCoachAdviceEntry.cs` |
| Voice knowledge graph | Modell for sammenhenger mellom stemmedimensjoner, innsikter og anbefalinger. | `Services/VoiceKnowledgeGraphBuilder.cs`, `Models/VoiceKnowledgeGraph.cs` |
| Inline coach | Gir korte, kontekstuelle coach-meldinger mens brukeren øver. | `Models/InlineCoachMessage.cs`, `ExerciseIntelligenceCoordinator.cs`, `InlineCoachFeedbackMapper.cs` |
| FeedbackPipeline | Felles port for meldinger før UI viser dem. | `Services/FeedbackPipeline.cs` |
| FeedbackConsistencyGuard | Prioriterer Safety > Health > Recovery > Comfort > Voice Development > Reporting og undertrykker motstridende meldinger. | `Services/FeedbackConsistencyGuard.cs`, `FeedbackPriorityMatrixTests.cs`, `FeedbackConsistencyGuardTests.cs` |
| Feedback mappers | Oversetter SmartCoach, inline coach, progression, hydration og vocal health til felles feedbackformat. | `FeedbackPipeline.cs`, `SmartCoachFeedbackMapper.cs`, `ProgressionFeedbackMapper.cs`, `HydrationFeedbackMapper.cs`, `VocalHealthFeedbackMapper.cs` |
| Legacy feedback service | Brukes særlig på hoveddashboardet for session feedback og sanntidstekst. | `Services/FeedbackService.cs`, `Services/CoachMessageGenerator.cs`, `Services/CoachMessageFormatter.cs` |

## Helse, sikkerhet og recovery

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| VocalHealthSupervisor | Vurderer strain, fatigue, pausebehov, recovery og safety state fra live-metrikker. | `Services/VocalHealthSupervisor.cs`, `Services/VocalHealthBaselineProvider.cs` |
| Health safety states | Kan eskalere til caution/restrict/lock og stoppe eller begrense øvelse når risiko øker. | `VocalHealthSupervisor.cs`, `Models/ExerciseLiveState.cs`, `SafetyLockTests.cs` |
| HydrationAdvisor | Gir støttende hydrering-/pauseforslag basert på belastning, resonansdrift og stabilitet. | `Services/HydrationAdvisor.cs`, `HydrationAdvisorTests.cs` |
| RecoveryScorer | Beregner reaktiv recovery-status basert på belastning, strain, fatigue og historikk. | `Services/RecoveryScorer.cs`, `RecoveryScorerTests.cs` |
| RecoveryIntelligenceService | Lager prediktiv recovery forecast med recovery debt, acute/chronic workload ratio, severity og anbefaling. | `Services/RecoveryIntelligenceService.cs`, `RecoveryIntelligenceServiceTests.cs` |
| ProgressionSafetyGate | Hindrer progresjon når helse, fatigue, recovery eller safety-historikk tilsier pause/forsiktighet. | `Services/ProgressionSafetyGate.cs`, `ProgressionSafetyGateTests.cs` |
| Recovery-aware target zones | Sørger for at targets og progresjon ikke åpnes for aggressivt ved lav recovery eller rask scoreøkning. | `ComfortZoneController.cs`, `ProgressionOrchestrator.cs`, `RecoveryAwareTargetZoneTests.cs` |
| StressSensitiveMode | Reduserer visuell/coachende belastning for brukere som trenger roligere presentasjon, uten å skjule safety/health. | `Services/StressSensitiveExperience.cs`, `Models/UserVoiceProfile.cs`, `SettingsWindow.xaml` |
| Safety-copy policy | Tester språkfiler for å unngå pressende, skamfull eller pitch-jagende tekst. | `FemVoiceStudio.Tests/ResourceTextPolicyTests.cs`, `ProfessionalResxPolicyTests.cs`, `Resources/Strings*.resx` |

## Progresjon, analytics og personalisering

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| SessionAnalyticsStore | Lagrer øktdata, exercise summaries, health events, hydration events og progresjonssignaler uten rå audio. | `Services/SessionAnalyticsStore.cs`, `Models/SessionInsight.cs` |
| ExerciseSessionRecorder | Journalfører fullførte øvelser, lytter på live-state og sender relevante data til analytics/health. | `Services/ExerciseSessionRecorder.cs` |
| MasteryEvaluator | Vurderer mastery over tid basert på stabil, trygg og gjentatt gjennomføring. | `Services/MasteryEvaluator.cs`, `Models/MasteryLevel.cs` |
| ProgressionOrchestrator | Vurderer om øvelsesprofil bør beholdes, tilpasses, pauses eller regresseres etter økt. | `Services/ProgressionOrchestrator.cs`, `Models/ProgressionSessionData.cs` |
| ExerciseProfileStore | Lagrer personlige øvelsesprofil-tilpasninger i SQLite. | `Services/ExerciseProfileStore.cs`, `Models/ExerciseTargetProfile.cs` |
| Exercise effectiveness | Måler per-øvelse-effektivitet og lar SmartCoach rangere øvelser som faktisk fungerer for brukeren. | `Services/ExerciseEffectivenessEngine.cs`, `Services/ExerciseEffectivenessProvider.cs`, `Models/ExerciseEffectivenessProfile.cs` |
| Trend engine | Bygger trendvinduer, utviklingsprofil og longitudinelle innsikter. | `Services/TrendEngineService.cs`, `Services/LongitudinalInsightEngine.cs`, `Models/TrendWindow.cs`, `Models/LongitudinalInsight.cs` |
| Pattern detector | Oppdager plateau, breakthrough, regression og andre mønstre i stemmeutviklingen. | `Services/VoicePatternDetector.cs`, `Models/VoicePatternEvents.cs` |
| Progression-dashboard | Viser progresjonsrelaterte data, kompleksitet og anbefalinger. | `Views/ProgressionWindow.xaml`, `Views/ProgressionDashboard.xaml`, `ViewModels/ProgressionDashboardViewModel.cs` |
| Calendar/statistics | Viser historikk, dagdetaljer, streak, totaler, score og progresjonsstatistikk. | `Views/CalendarWindow.xaml`, `Views/DayDetailsWindow.xaml`, `Views/StatisticsWindow.xaml` |
| Voice Goal Profile | Lagrer brukerens stemmemål, stilpreferanse og fokusdimensjon. | `Models/VoiceGoalProfile.cs`, `Models/UserVoiceProfile.cs`, `Services/LocalVoiceGoalProfileStore.cs` |
| Førstegangsoppsett | Lar bruker velge grunninnstillinger, mål og tilgjengelighetsvalg ved første oppstart. | `Views/FirstTimeSetupWindow.xaml`, `Services/FirstTimeSetupService.cs` |
| Settings | Håndterer tema, språk, målprofil, tilgjengelighet, mic calibration, hear-own-voice og database-reset. | `Views/SettingsWindow.xaml`, `Services/ThemeManager.cs`, `Services/LocalizationService.cs` |
| Tema | Støtter lys, mørk og systemstyrt visning. | `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml`, `ThemeManager.cs` |
| Språk/RESX | Bruker språkfiler for norsk, engelsk og flere andre språk. | `Resources/Strings*.resx`, `LocalizationService.cs`, `LocConverter.cs` |

## Analysevinduer

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Analyzer | Viser detaljert audio-/spectrogramanalyse, resonansstatus, clinical score og debugpanel ved behov. | `Views/AnalyzerWindow.xaml`, `Views/AnalyzerWindow.xaml.cs`, `Services/SpectrogramResonanceMapper.cs` |
| Resonance window | Eget vindu for resonansanalyse med start, stopp, reset og chart view model. | `Views/ResonanceWindow.xaml`, `Views/ResonanceWindow.xaml.cs`, `Views/ResonanceChartViewModel.cs` |
| Analysis window | Viser analyse-side/rapportering knyttet til stemmedata og dimensjoner. | `Views/AnalysisWindow.xaml`, `ViewModels/AnalysisPageViewModel.cs` |
| LiveFeedbackView | UserControl for live feedback med egen VM. Finnes i repoet, men aktiv navigasjon/wiring bør verifiseres før den behandles som hovedflate. | `Views/LiveFeedbackView.xaml`, `Views/LiveFeedbackViewModel.cs` |

## Profesjonelle verktøy og rapportering

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Clinician Dashboard | Profesjonell oversikt over outcome, kliniske signaler, risiko, mål og historikk. | `Views/ClinicianDashboardWindow.xaml`, `Views/ClinicianDashboard.xaml`, `ViewModels/ClinicianDashboardViewModel.cs` |
| Coach Dashboard | Coach-orientert oversikt over anbefalinger, effektivitet, mål og treningsstatus. | `Views/CoachDashboardWindow.xaml`, `Views/CoachDashboard.xaml`, `ViewModels/CoachDashboardViewModel.cs` |
| OutcomeProfile | Samler målprogresjon, recovery, øvelseseffektivitet og long-term development til ett rapporteringssnapshot. | `Services/OutcomeProfileBuilder.cs`, `Services/OutcomeProfileStore.cs`, `Models/OutcomeProfile.cs` |
| Report Export | Lar bruker velge rapporttype og format, genererer rapport og lagrer fil. | `Views/ReportExportWindow.xaml`, `ViewModels/ReportExportViewModel.cs`, `Services/ReportAssembler.cs`, `Services/ExportWriter.cs` |
| Rapporttyper | Støtter Clinical, Coach, Outcome og Timeline report DTO-er. | `Models/ProfessionalReports.cs`, `Services/ReportAssembler.cs` |
| Eksportformater | Skriver PDF, CSV og JSON. PDF bygges med QuestPDF; CSV følger RFC 4180-escaping. | `Services/ExportWriter.cs`, `ExportWriterTests.cs`, `ReportAssemblerTests.cs` |
| Clinical notes | Lagrer kliniske notater separat fra treningsmotoren. | `Services/ClinicalNotesStore.cs`, `Models/ClinicalNote.cs` |
| Audit trail | Append-only audit-historikk for profesjonelle handlinger, spesielt overrides. | `Services/AuditTrailStore.cs`, `Models/AuditEvent.cs`, `AuditTrailStoreTests.cs` |
| Manual Override | Lar profesjonell be om override, men clampes av recovery/safety slik at override aldri blir mindre konservativ enn gate-floors. | `Views/ManualOverrideWindow.xaml`, `ViewModels/ManualOverrideViewModel.cs`, `Services/ManualOverrideEngine.cs`, `Models/ManualOverrideRequest.cs` |
| Case Review | Monterer og lagrer case reviews fra outcome snapshots for klinisk/coach review. | `Views/CaseReviewWindow.xaml`, `ViewModels/CaseReviewViewModel.cs`, `Services/CaseReviewAssembler.cs`, `Services/CaseReviewsStore.cs`, `Models/CaseReview.cs` |
| Pilot readiness | Sjekker om app/data er klare nok for pilot-/releasebruk. | `Services/PilotReadinessChecker.cs`, `PilotReadinessCheckerTests.cs`, `ReleaseReadinessSmokeTests.cs` |

## Research og anonymisering

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Participant token | Lager og persisterer en opaque deltaker-ID for research-eksport, uten å bruke lokal UserId. | `Services/ParticipantTokenProvider.cs` |
| Research anonymizer | Fjerner PII: lokal UserId, device name, fritekst og time-of-day fjernes før eksport. | `Services/ResearchAnonymizer.cs`, `Models/ResearchDataset.cs`, `ResearchAnonymizerTests.cs` |
| Research aggregator | Bygger cohort-/gruppeaggregater som exercise effectiveness, plateau frequency og recovery distribution. | `Services/ResearchAggregator.cs`, `ResearchAggregatorTests.cs` |
| N=1 caveat | Research-datasettet er multi-participant i form, men flagger utilstrekkelig cohort når deltakerantall er under 5. | `ResearchAggregator.cs`, `Models/ResearchDataset.cs` |
| Research no-PII policy | Tester at research-output ikke lekker identifiserende felt. | `ResearchNoPiiTests.cs`, `ResearchAnonymizerTests.cs` |

## Data og persistens

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| DatabaseService | SQLite/database-lag for brukerdata, settings, økter, score, SmartCoach-data og historikk. | `Data/DatabaseService.cs`, `Data/DatabaseSchema.sql`, `Data/IDatabaseService.cs` |
| Shared femvoice.db | Flere stores bruker samme lokale SQLite-fil under brukerens dokumentmappe. | `App.xaml.cs`, `SessionAnalyticsStore.cs`, `ExerciseProfileStore.cs`, `SmartCoachMemoryStore.cs`, `OutcomeProfileStore.cs`, `ManualOverridesStore.cs`, `ClinicalNotesStore.cs`, `AuditTrailStore.cs`, `CaseReviewsStore.cs` |
| Repository interfaces | Skiller dataaksess fra app-logikk og gjør tester enklere. | `Data/IUserRepository.cs`, `Data/IScoreRepository.cs`, `Services/*Store.cs` |
| In-memory repositories | Brukes for testbarhet og fallback der persistent data ikke trengs. | `Services/InMemoryExerciseRepositories.cs`, `Services/*Store.cs` |
| Migreringer | Har SQL-migrering for exercise feedback-systemet i tillegg til hovedschema. | `Data/migrations/001_exercise_feedback_system.sql`, `Resources/DatabaseSchema.sql` |
| Debug/test services | Støtte for test, debug, lokal verifisering og analyzer-logging. | `Services/TestSettingsService.cs`, `Services/TestLocalizationService.cs`, `Services/DebugSettingsService.cs` |

## Tester og kvalitetssikring

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Unit tests | Tester scoring, progresjon, recovery, health, calibration, feedback, RESX-policy, rapportering, research og exercise-flyt. | `FemVoiceStudio.Tests/*.cs` |
| Release smoke tests | Verifiserer sentrale release-forutsetninger. | `FemVoiceStudio.Tests/ReleaseReadinessSmokeTests.cs` |
| Resource policy tests | Hindrer utrygg eller uprofesjonell brukerrettet språkbruk i språkfiler. | `ResourceTextPolicyTests.cs`, `ProfessionalResxPolicyTests.cs` |
| Safety invariant tests | Tester at safety/health/recovery ikke kan overstyres av progresjon, coach eller manual override. | `SafetyOverrideInvariantTests.cs`, `SafetyPriorityEngineTests.cs`, `ManualOverrideClampTests.cs` |
| Pitch/graph tests | Tester target-zoner, pitch trace og chart axis policy. | `PitchTargetZonePolicyTests.cs`, `PitchTraceStabilizerTests.cs`, `PitchChartAxisRangeCalculatorTests.cs` |
| Report/research tests | Tester rapportmontasje, PDF/CSV/JSON writer, anonymisering og aggregater. | `ReportAssemblerTests.cs`, `ExportWriterTests.cs`, `ResearchAnonymizerTests.cs`, `ResearchAggregatorTests.cs` |
| Exercise catalog tests | Tester katalogdekning og øvelsesdetaljer. | `ExerciseCatalogCoverageTests.cs`, `ExerciseDetailViewModelTests.cs`, `ExerciseFeedbackEngineTests.cs` |

## Legacy, merge- og cleanup-kandidater

Disse finnes i prosjektet, men bør behandles forsiktig fordi noen er parallelle eller eldre systemer:

| Område | Kort status |
| ------ | ----------- |
| `Subsystems/*` | Eldre subsystem-lag med audio/data/analysis/progression/smartcoach. Aktiv app bruker primært `App.ConfigureServices`, men sletting krever merge-audit. |
| `Infra/DependencyInjection.cs` | Eldre DI-oppsett. Aktiv oppstart registrerer tjenester i `App.xaml.cs`. |
| `ViewModels/ViewModelBase.cs` | Refererer subsystem-abstraksjoner og ser eldre ut sammenlignet med dagens direkte VM-er/DI. |
| `LiveFeedbackView` / `ExerciseSummaryView` | UserControls med egne VM-er. Kan være aktive/planlagte UX-flater, men er ikke hovednavigasjon alene. |
| `VoiceHealthService` / `VoiceHealthModule` / `VocalHealthLegacyBridge` | Eldre/parallel health-linje. Aktiv safety-linje er `VocalHealthSupervisor`, men health-kode bør merge-audites før sletting. |
| `AudioAnalysisEngine_new.cs`, `part2.cs`, `.old`, `.old2`, `.txt` kopier | Artefakter/stubs/backupfiler. Bør ryddes i egen cleanup-runde etter kompilering og referansesøk. |
| `promts/`, `work-documents/new*`, roadmap-filer | Plan-/prompt-/arbeidsdokumenter. Skal ikke brukes alene som sannhet om implementert funksjon. |

## Kort systemflyt

```text
Mikrofon
  -> audio capture / realtime analysis
  -> pitch, resonans, formanter, intensity, stability, score
  -> MainViewModel / ExerciseIntelligenceCoordinator
  -> live UI, pitch-graf, analyzer og exercise feedback
  -> VocalHealthSupervisor / HydrationAdvisor / RecoveryIntelligenceService
  -> SessionAnalyticsStore / ExerciseSessionRecorder
  -> ProgressionOrchestrator / ExerciseEffectivenessEngine / SmartCoachEngine
  -> FeedbackPipeline / FeedbackConsistencyGuard
  -> trygg brukerrettet feedback
  -> OutcomeProfile / rapporter / research-anonymisering
```

## Viktigste prinsipp

FemVoice skal hjelpe en trans jente med stemmefeminisering uten å gjøre pitch til eneste mål. Systemet skal støtte resonans, stabilitet, intonasjon, komfort, bærekraftig øving, helse og personlig progresjon. Pitch brukes som en del av bildet, men feedback og progresjon skal ikke belønne press, strain eller ekstremt høye verdier.

Den praktiske prioriteten i koden er:

```text
Safety > Health > Recovery > Comfort > Voice Development > Reporting
```

Rapportering, research, coach-anbefalinger og profesjonelle overrides skal derfor være beskrivende eller mer konservative. De skal ikke kunne overstyre safety-, health- eller recovery-gater.
