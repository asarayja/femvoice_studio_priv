# FemVoice Funksjonsoversikt

Status: 2026-06-16 (oppdatert mot faktisk WPF-kode under repo-audit; se `docs/`)

Formål: praktisk oversikt over hvilke hovedfunksjoner FemVoice Studio har nå, hva de gjør, og hvor de bor i koden. Dette er en produkt- og arkitekturoversikt basert på aktiv WPF-kode, DI-wiring og tester. Det er ikke en klinisk validering, cleanup-plan eller Avalonia-plan — det er en nåtilstand-oversikt.

> Oppdatering 2026-06-16: Filreferanser er verifisert mot koden under repo-auditen. Utdaterte referanser er rettet (se «Rettelser etter audit» nederst). Detaljert audit ligger i `docs/CURRENT_*`-filene og `docs/AUDIT_SUMMARY_FOR_AVALONIA_PLANNING.md`.

## Kort status

FemVoice Studio er en lokal WPF/.NET-app for stemmetrening med sanntids lydanalyse, øvelsesguide, progressiv trening, SmartCoach, helse-/recovery-gating, rapportering og profesjonelle verktøy. Appen bruker NAudio til mikrofon/lyd, OxyPlot til grafer, SQLite til lokal persistens, RESX til lokalisering og QuestPDF til PDF-eksport.

Aktiv oppstart og DI ligger primært i `FemVoiceStudio/App.xaml.cs`. `Subsystems/*` og `Infra/DependencyInjection.cs` finnes fortsatt, men ser ut som eldre/parallelle arkitekturlag som må merge-audites før cleanup.

Sprint G Addendum-status per 2026-06-14:

- Theme-hardening er utvidet for ComboBox/dropdown, knapper og OxyPlot-baserte UI-grafer.
- `Analyse`/`Dybdeanalyse` og `Resonansanalyse` har delt chart-theme pipeline og realistiske zoom/pan-bounds for UI-grafer.
- `Settings`-vinduet og flere sekundærvinduer er auditert for layout og modal/modeless oppførsel.
- Forsidekort for progresjon/nivå er koblet til faktisk dataflyt eller forklarer datatilstand der nok data mangler.
- Forbrukerrettet engelsk dokumentasjon ligger nå i `README_CONSUMER.md` og `USER_GUIDE.md`.

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
| Øvelsessammendrag | Oppsummering av pitch, resonans, stabilitet og anbefalt neste steg etter stopp. **Rettet 2026-06-16:** det finnes IKKE noen `ExerciseSummaryView`/`ExerciseSummaryViewModel`/`SmartCoachExerciseAdapter` — sammendraget rendres inline i `ExerciseWindow.xaml.cs` og hentes fra `SessionAnalyticsStore`. | `Views/ExerciseWindow.xaml.cs`, `Services/SessionAnalyticsStore.cs` |

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
| Inline coach | Gir korte, kontekstuelle coach-meldinger mens brukeren øver. **Rettet 2026-06-16:** `InlineCoachFeedbackMapper` er en klasse inne i `Services/FeedbackPipeline.cs`, ikke en egen fil. | `Models/InlineCoachMessage.cs`, `ExerciseIntelligenceCoordinator.cs`, `Services/FeedbackPipeline.cs` |
| FeedbackPipeline | Felles port for meldinger før UI viser dem. | `Services/FeedbackPipeline.cs` |
| FeedbackConsistencyGuard | Prioriterer Safety > Health > Recovery > Comfort > Voice Development > Reporting og undertrykker motstridende meldinger. Den maskinlesbare rekkefølgen er `FeedbackPriority`-enumen. | `Services/FeedbackConsistencyGuard.cs`, `FeedbackPriorityMatrixTests.cs`, `FeedbackConsistencyGuardTests.cs` |
| Feedback mappers | Oversetter SmartCoach, inline coach, progression, hydration og vocal health til felles feedbackformat. **Rettet 2026-06-16:** ALLE mapper-klassene (`SmartCoachFeedbackMapper`, `InlineCoachFeedbackMapper`, `ProgressionFeedbackMapper`, `HydrationFeedbackMapper`, `VocalHealthFeedbackMapper`, `MainScreenFeedbackMapper`) ligger inne i `Services/FeedbackPipeline.cs` — det finnes ingen egne mapper-filer. | `Services/FeedbackPipeline.cs` |
| Legacy feedback service | Brukes særlig på hoveddashboardet for session feedback og sanntidstekst. **Rettet 2026-06-16:** `CoachMessageGenerator.cs`/`CoachMessageFormatter.cs` finnes IKKE; logikken bor i `FeedbackService.cs`. | `Services/FeedbackService.cs` |

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
| Exercise effectiveness | Måler per-øvelse-effektivitet og lar SmartCoach rangere øvelser som faktisk fungerer for brukeren. **Rettet 2026-06-16:** det finnes ingen `ExerciseEffectivenessProvider.cs` — motoren heter `ExerciseEffectivenessEngine`. | `Services/ExerciseEffectivenessEngine.cs`, `Models/ExerciseEffectivenessProfile.cs` |
| Trend engine | Bygger trendvinduer, utviklingsprofil og longitudinelle innsikter. | `Services/TrendEngineService.cs`, `Services/LongitudinalInsightEngine.cs`, `Models/TrendWindow.cs`, `Models/LongitudinalInsight.cs` |
| Pattern detector | Oppdager plateau, breakthrough, regression og andre mønstre i stemmeutviklingen. | `Services/VoicePatternDetector.cs`, `Models/VoicePatternEvents.cs` |
| Progression-dashboard | Viser progresjonsrelaterte data, kompleksitet og anbefalinger. | `Views/ProgressionWindow.xaml`, `Views/ProgressionDashboard.xaml`, `ViewModels/ProgressionDashboardViewModel.cs` |
| Calendar/statistics | Viser historikk, dagdetaljer, streak, totaler, score og progresjonsstatistikk. | `Views/CalendarWindow.xaml`, `Views/DayDetailsWindow.xaml`, `Views/StatisticsWindow.xaml` |
| Voice Goal Profile | Lagrer brukerens stemmemål, stilpreferanse og fokusdimensjon. | `Models/VoiceGoalProfile.cs`, `Models/UserVoiceProfile.cs`, `Services/LocalVoiceGoalProfileStore.cs` |
| Førstegangsoppsett | Lar bruker velge grunninnstillinger, mål og tilgjengelighetsvalg ved første oppstart. | `Views/FirstTimeSetupWindow.xaml`, `Services/FirstTimeSetupService.cs` |
| Settings | Håndterer tema, språk, målprofil, tilgjengelighet, mic calibration, hear-own-voice og database-reset. | `Views/SettingsWindow.xaml`, `Services/ThemeManager.cs`, `Services/LocalizationService.cs` |
| Tema | Støtter lys, mørk og systemstyrt visning. | `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml`, `ThemeManager.cs` |
| Språk/RESX | Bruker språkfiler for norsk, engelsk og flere andre språk. | `Resources/Strings*.resx`, `LocalizationService.cs`, `LocConverter.cs` |

## UI, tema og release-hardening

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Global mørk/lys theme | Delt ResourceDictionary-oppsett for tekst, flater, knapper, ComboBox/dropdown og appens hovedflater. | `Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml`, `Services/ThemeManager.cs`, `App.xaml` |
| ComboBox/dropdown-lesbarhet | Globale ComboBox-, ComboBoxItem- og ListBoxItem-stater er herdet for normal, hover, valgt, valgt+hover, keyboard highlight, fokus og disabled i dark/light mode. | `Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml`, `FemVoiceStudio.Tests/ThemeResourceCoverageTests.cs`, `FemVoiceStudio/Docs/ComboBoxButtonThemeVisualChecklist.md` |
| Knappe-stater | Delte button-varianter har lesbare normal/hover/pressed/focus/disabled-stater, inkludert Settings og Manuelle justeringer. | `Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml`, `Views/SettingsWindow.xaml`, `Views/ManualOverrideWindow.xaml` |
| Settings layout | Innstillingsvinduet er gjort større og mer responsivt slik at backup/restore/database-kontroller og lange norske labels ikke klippes. | `Views/SettingsWindow.xaml`, `FemVoiceStudio.Tests/WindowModalBehaviorTests.cs` |
| Modeless hjelpevinduer | Ikke-kritiske hjelpe-/guide-/analysevinduer åpnes modeless med fokus på eksisterende instans der det er relevant; destruktive bekreftelser forblir modal. | `Views/MainWindow.xaml.cs`, `FemVoiceStudio/Docs/WindowModalBehaviorChecklist.md`, `FemVoiceStudio.Tests/WindowModalBehaviorTests.cs` |
| Chart theme | UI-grafer mapper WPF-theme brushes til OxyPlot-farger for bakgrunn, plot area, akser, grid, legend og tomtilstand. PDF/rapportgrafer holdes adskilt. | `Services/AnalysisChartTheme.cs`, `Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml`, `FemVoiceStudio.Tests/AnalysisChartThemeTests.cs` |
| Chart zoom/pan-bounds | Analyse/Dybdeanalyse- og Resonansanalyse-grafer har realistiske `AbsoluteMinimum`, `AbsoluteMaximum`, `MinimumRange` og `MaximumRange` slik at zoom/pan ikke havner langt utenfor datadomenet. | `Services/AnalysisChartTheme.cs`, `ViewModels/AnalysisPageViewModel.cs`, `Views/AnalysisWindow.xaml.cs`, `Views/ResonanceChartViewModel.cs` |
| Manuell visuell sjekkliste | Release-sjekklister dekker dark/light theme, chart zoom/pan/reset, knapper, ComboBox og vindusoppførsel. | `FemVoiceStudio/Docs/AnalysisChartThemeManualChecklist.md`, `FemVoiceStudio/Docs/ComboBoxButtonThemeVisualChecklist.md`, `FemVoiceStudio/Docs/WindowModalBehaviorChecklist.md` |

## Analysevinduer

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Analyzer | Viser detaljert audio-/spectrogramanalyse, resonansstatus, clinical score og debugpanel ved behov. | `Views/AnalyzerWindow.xaml`, `Views/AnalyzerWindow.xaml.cs`, `Services/SpectrogramResonanceMapper.cs` |
| Resonance window | Eget vindu for resonansanalyse med start, stopp, reset, F1/F2-posisjon, formant-tidslinje, dark/light chart theme og bounded zoom/pan. | `Views/ResonanceWindow.xaml`, `Views/ResonanceWindow.xaml.cs`, `Views/ResonanceChartViewModel.cs`, `Services/AnalysisChartTheme.cs` |
| Analysis window | Viser analyse-side/rapportering knyttet til stemmedata og dimensjoner, inkludert Dybdeanalyse-grafer med delt chart-theme og bounded zoom/pan. | `Views/AnalysisWindow.xaml`, `Views/AnalysisWindow.xaml.cs`, `ViewModels/AnalysisPageViewModel.cs`, `Services/AnalysisChartTheme.cs` |
| ~~LiveFeedbackView~~ | **Rettet 2026-06-16:** `Views/LiveFeedbackView.xaml`/`LiveFeedbackViewModel.cs` finnes IKKE i repoet. Live feedback er implementert i `ExerciseWindow` + `ExerciseDetailViewModel`. | `Views/ExerciseWindow.xaml(.cs)`, `ViewModels/ExerciseDetailViewModel.cs` |

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
| `LiveFeedbackView` / `ExerciseSummaryView` | **Rettet 2026-06-16:** finnes IKKE som filer. Tidligere dokumentert som UX-kandidater, men er aldri implementert som egne views/VM-er. |
| `VoiceHealthService` | Eldre/parallell health-linje. **Rettet 2026-06-16:** `VoiceHealthModule` og `VocalHealthLegacyBridge` finnes IKKE; kun `VoiceHealthService.cs` (+ `HealthStatus.cs`) eksisterer og ser løsrevet ut fra den aktive gate-flyten (`VocalHealthSupervisor`). Bør merge-audites før sletting. |
| `AudioAnalysisEngine_new.cs`, `.old`/`.old2`-kopier | Artefakter/stubs/backupfiler. **Rettet 2026-06-16:** `AudioAnalysisEngine_new.cs` kompileres, men inneholder bare `using System;` (tom stub); ingen `part2.cs` finnes. `.old`/`.old2` kompileres ikke. Bør ryddes i egen cleanup-runde. |
| Død parallell-arkitektur | `Subsystems/**`, `Infra/DependencyInjection.cs` (`AddFemVoiceStudio`), `ViewModels/ViewModelBase.cs`/`SubsystemViewModelBase` har ingen eksterne referanser (aktiv wiring er `App.ConfigureServices`). Skal IKKE porteres til Avalonia. |
| Test-kode i app-prosjektet | `FemVoiceStudio/Tests/` (4 xUnit-filer) kompileres inn i selve WinExe-en, og app-csproj refererer xUnit/Test.Sdk. Cleanup-kandidat. |
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

## Delvis implementerte funksjoner

Disse er aktive, men har begrensninger eller stubbede deler (bekreftet i kode 2026-06-16):

- **Vocal weight / strain / jitter / shimmer / HNR:** `VoiceStrainDetector` returnerer jitter/shimmer = 0 og en placeholder-stddev (`mean*0.1`); `VoiceMetricsCalculator` HNR er en approksimasjon (ikke ekte harmonics-to-noise); `FormantDetectionService` «spectral centroid» er en ZCR-approksimasjon; `VoiceActivityDetector` har en `SpectralCentroidThreshold` som er deklarert, men ubrukt.
- **Pitch-motorer:** `AudioAnalyzerService` er den aktive forsidemotoren. `AudioAnalysisEngine` (WASAPI→WaveIn fallback) konstrueres, men forsidens capture er bevisst undertrykt; `RealtimeAnalysisEngine` og `AsyncAudioPipeline` ser ut til å være ubrukt i produksjon (bør verifiseres).
- **Lokalisering:** `String.pt-BR.resx` (feilstavet basenavn) og `Strings_en.resx` (understrek) lastes ikke som satellitter — pt-BR er reelt sett ikke koblet inn. Ca. 19 språk er reelt lastbare.
- **DB-migrering:** `Resources/DatabaseSchema.sql` og `Data/migrations/001_*.sql` ser dormante ut (kjøres ikke i runtime; migreringen har ugyldig SQLite-syntaks).

## Validert RC-0 baseline-oppførsel

RC-0 er release-candidate-zero valideringsprofilen. Evidence-pipelinen (`Rc0StartupBootstrap`, `Rc0EvidenceExporter`, `Rc0RuntimeLog`, `Rc0WriteFailureSink`, `DiagnosticsNaming`) er en developer-only, aldri-kastende diagnostikk som beviser at audio→pitch→resonans→graf→persistens→rapport-kjeden faktisk kjørte. `Rc0StartupBootstrap.Run()` kjøres FØR DI i `App.OnStartup` og legger igjen baseline-evidence selv ved oppstartskrasj. Økter klassifiseres PASS/WARNING/FAIL/BLOCKED. Evidence skrives til `%LOCALAPPDATA%\FemVoiceStudio\Diagnostics` (med Documents-speil og legacy `RC0_Evidence`-alias). Påvirker aldri safety/health/recovery. Se `docs/CURRENT_DIAGNOSTICS_AND_EVIDENCE.md`.

## Rapporttyper og -format

- **4 rapporttyper:** Clinical, Coach, Outcome, Timeline (`Models/ProfessionalReports.cs`, `ReportAssembler`).
- **3 format:** PDF (QuestPDF 2026.5.0, Community-lisens, kun tekst/tabell — ingen grafer i PDF), CSV (RFC 4180-escaping), JSON (`System.Text.Json`). Tekst lokaliseres og saneres (`ReportTextSanitizer`). Se `docs/CURRENT_REPORTS_AND_LOCALIZATION.md`.

## Lokaliseringsoppførsel

`LocalizationService` bruker `ResourceManager` + `CultureInfo` (nøytralt språk = norsk). Språkbytte settes via `SetLanguage` (tråd-culture + `PropertyChanged("Item[]")`) og oppdateres live i UI via `{loc:Loc Key}`/`LocConverter` (WPF MarkupExtensions). Preferanse lagres i `language.txt`.

## Kjente begrensninger

- Windows-bundet: WPF + NAudio (WASAPI/WaveIn) + Registry-temadeteksjon + `Microsoft.Win32`-fildialoger. Tester er `net10.0-windows`.
- Stubbede stemmemetrikker (se «Delvis implementerte funksjoner»).
- Død parallell-arkitektur (`Subsystems/**`, `Infra/DependencyInjection.cs`) og backup-artefakter (`*.cs.old`) ligger igjen.
- Test-kode kompileres inn i app-EXE-en.
- pt-BR-lokalisering er feilkoblet.

## Hva som MÅ bevares ved Avalonia-port

Klinisk scoring, SmartCoach, Voice Health / safety / recovery-gater, progresjon, persistens (SQLite-skjema + delt `femvoice.db`), analytics, diagnostikk/evidence (RC-0), rapportinnhold (4×3), lokaliseringsressurser (kun de dokumenterte navnerettingene), og 15-øvelseskatalogen. Safety-invariant-testene (`SafetyOverrideInvariantTests`, `SafetyPriorityEngineTests`, `ManualOverrideClampTests`, `FeedbackPriorityMatrixTests`) skal holdes grønne gjennom porten. Detaljert ekstraherings-/abstraksjonsplan: `docs/AVALONIA_PORT_READINESS_NOTES.md` og `docs/WPF_DEPENDENCY_MAP.md`.

## Rettelser etter audit (2026-06-16)

Følgende referanser i tidligere versjon var utdaterte og er rettet over:

- **Finnes ikke:** `Views/ExerciseSummaryView.xaml`/`ExerciseSummaryViewModel.cs`, `Views/LiveFeedbackView.xaml`/`LiveFeedbackViewModel.cs`, `Services/CoachMessageGenerator.cs`, `Services/CoachMessageFormatter.cs`, `Services/SmartCoachExerciseAdapter.cs`, `Services/ExerciseEffectivenessProvider.cs`, `VoiceHealthModule`, `VocalHealthLegacyBridge`, `AudioAnalysisEngine part2.cs`.
- **Ligger inne i `Services/FeedbackPipeline.cs` (ikke egne filer):** `SmartCoachFeedbackMapper`, `InlineCoachFeedbackMapper`, `ProgressionFeedbackMapper`, `HydrationFeedbackMapper`, `VocalHealthFeedbackMapper`, `MainScreenFeedbackMapper`.
- **Feil navn:** «ExerciseEffectivenessProvider» → faktisk `ExerciseEffectivenessEngine`.
- **Eksisterer som angitt:** `VoiceHealthService.cs` (men løsrevet fra aktiv gate-flyt).
