# FemVoice Funksjonsoversikt

Status: 2026-06-07

Formål: kort oversikt over hvilke hovedfunksjoner FemVoice Studio har, og hva de gjør. Dette er ikke en klinisk validering eller cleanup-plan, men en praktisk produkt- og arkitekturoversikt.

## Hovedapp

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Hoveddashboard | Start/stopp vanlig økt, viser øvelse, sanntidsfeedback, pitch-graf, session-tid og snarveier til andre moduler. | `Views/MainWindow.xaml`, `Views/MainWindow.xaml.cs`, `ViewModels/MainViewModel.cs` |
| Start/stopp session | Starter mikrofonanalyse, live pitch/resonansmåling, timer og feedback. Stopper analyse, lagrer resultat og genererer oppsummering. | `MainViewModel.cs`, `AudioAnalyzerService.cs`, `AudioAnalysisEngine.cs`, `FeedbackService.cs` |
| Vanskelighetsnivå | Lar brukeren velge nybegynner, middels eller avansert. Påvirker øvelsestekst og pitch target-zone. | `MainViewModel.cs`, `PitchTargetZonePolicy.cs` |
| Pitch-graf | Viser stabilisert pitch-trace og grønn målzone per vanskelighetsgrad. Skal vise komfortabel målzone, ikke belønne høyest mulig pitch. | `MainWindow.xaml.cs`, `PitchTraceStabilizer.cs`, `PitchTargetZonePolicy.cs`, `PitchChartAxisRangeCalculator.cs` |
| Sanntidsfeedback | Gir korte meldinger om signal, pitch under/innenfor/over målzone, resonansfokus og komfort. | `FeedbackService.cs`, `Resources/Strings*.resx` |

## Øvelsesguide

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Exercise Guide | Viser øvelser kategorisert etter pitch, resonans, intonasjon, pust og praksis. | `Views/ExerciseWindow.xaml`, `Views/ExerciseWindow.xaml.cs` |
| Øvelsesdetalj | Viser mål, instruksjoner, guidance, live feedback, timer, status og subjektiv rapport etter stopp. | `ExerciseWindow.xaml`, `ViewModels/ExerciseDetailViewModel.cs` |
| Guidance-system | Viser klinisk/pedagogisk veiledning for valgt øvelse: hensikt, fysisk fokus, vanlige feil og sikkerhetsinfo. | `ExerciseDetailViewModel.cs`, `Models/ExerciseTargetProfile.cs`, `Resources/Strings*.resx` |
| Exercise live feedback | Samler resonans, pitch, stabilitet, hold progress, safety og coach-meldinger under en øvelse. | `ExerciseIntelligenceCoordinator.cs`, `ExerciseLiveState.cs`, `ExerciseDetailViewModel.cs` |
| Hold progress | Måler om brukeren holder riktig teknikk lenge nok, men fryser ved safety lock eller feil måltilstand. | `ExerciseIntelligenceCoordinator.cs`, `ExerciseSessionTimerState.cs` |
| Subjektiv rapport | Etter stopp kan brukeren rapportere komfort, tretthet, press og motivasjon før progresjon vurderes. | `ExerciseWindow.xaml.cs`, `Models/SubjectiveReport.cs`, `ProgressionOrchestrator.cs` |

## Lydanalyse og biofeedback

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Audio capture | Leser mikrofoninput og sender frames videre til analyse. | `Audio/AudioCaptureService.cs`, `Audio/AudioAnalysisEngine.cs` |
| Pitch detection | Beregner tonehøyde fra mikrofoninput og filtrerer ugyldige/ustabile frames. | `Audio/PitchDetectionService.cs`, `Audio/AudioAnalyzerService.cs` |
| Pitch trace stabilisering | Korrigerer sannsynlige harmoniske hopp og avviser ekstreme spikes i grafen. | `Services/PitchTraceStabilizer.cs` |
| Resonansanalyse | Bruker formanter og proxy-score for å anslå resonansplassering og resonansstyrke. | `Audio/ResonanceProxyEngine.cs`, `Audio/FormantDetectionService.cs` |
| Spectrogram intelligence | Viser resonans/formant-informasjon i analyzer slik at resonans kan forstås visuelt. | `Views/AnalyzerWindow.xaml.cs`, `Services/SpectrogramResonanceMapper.cs` |
| FemVoice score | Lager normaliserte score-signaler som kan brukes av feedback, exercise og progresjon. | `Services/FemVoiceScoreEngine.cs`, `Services/FemVoiceScore.cs` |
| Comfort zone | Vurderer om pitch/teknikk ligger i et trygt og komfortabelt område. | `Services/ComfortZoneController.cs`, `Services/AdaptiveComfortZoneService.cs` |

## Mikrofon og hardware

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Mikrofonkalibrering | Måler stille rom og stemme/humming for å finne terskler per mikrofon. | `Views/MicrophoneCalibrationWindow.xaml`, `Audio/MicrophoneCalibrationService.cs` |
| Per-device profiler | Lagrer egne kalibreringsprofiler for USB, jack/analog, headset eller laptop-mikrofon. | `Audio/MicrophoneCalibrationProfile.cs`, `MicrophoneCalibrationService.cs` |
| Signalråd | Skiller tekniske mikrofonproblemer fra stemmefeedback, for eksempel lav output, støy, clipping eller noise gate. | `MicrophoneCalibrationService.cs`, `AudioAnalyzerService.cs` |
| Hear own voice | Innstilling for om brukeren skal høre egen mikrofon-monitorering. Skal ikke være aktiv når funksjonen er av. | `SettingsWindow.xaml`, `MainViewModel.cs`, `AudioAnalyzerService.cs` |

## SmartCoach og feedback

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| SmartCoach | Gir anbefalinger basert på økter, progresjon, målprofil og feedback-sikkerhet. | `Services/SmartCoachEngine.cs`, `ViewModels/SmartCoachViewModel.cs`, `Views/SmartCoachDetailView.xaml` |
| Inline coach | Gir korte, kontekstuelle coach-meldinger mens brukeren øver. | `Models/InlineCoachMessage.cs`, `ExerciseIntelligenceCoordinator.cs` |
| FeedbackPipeline | Felles port for meldinger før UI viser dem. | `Services/FeedbackPipeline.cs` |
| FeedbackConsistencyGuard | Prioriterer sikkerhet/helse over ros, progresjon og teknikkhint. Undertrykker motstridende meldinger. | `Services/FeedbackConsistencyGuard.cs` |
| Feedback mappers | Oversetter SmartCoach, inline coach, progression, hydration og vocal health til felles feedbackformat. | `FeedbackPipeline.cs` |
| Legacy feedback service | Brukes særlig på hoveddashboardet for session feedback og sanntidstekst. | `Services/FeedbackService.cs` |

## Helse og sikkerhet

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| VocalHealthSupervisor | Vurderer strain, fatigue, pausebehov, recovery og safety state ut fra live-metrikker. | `Services/VocalHealthSupervisor.cs` |
| Health safety states | Kan eskalere til caution, restrict eller lock når systemet ser risiko. | `VocalHealthSupervisor.cs`, `Models/ExerciseLiveState.cs` |
| HydrationAdvisor | Gir støttende hydrering-/pauseforslag basert på belastning, resonansdrift og stabilitet. | `Services/HydrationAdvisor.cs` |
| ProgressionSafetyGate | Hindrer progresjon når helse, fatigue eller safety-historikk tilsier pause eller forsiktighet. | `Services/ProgressionSafetyGate.cs` |
| Safety-copy policy | Tester språkfiler for å unngå pressende, skamfull eller pitch-jagende tekst. | `FemVoiceStudio.Tests/ResourceTextPolicyTests.cs`, `Resources/Strings*.resx` |

## Progresjon og analytics

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| SessionAnalyticsStore | Lagrer øktdata, exercise summaries, health events, hydration events og progresjonssignaler uten rå audio. | `Services/SessionAnalyticsStore.cs` |
| ExerciseSessionRecorder | Journalfører fullførte øvelser og sender relevante data til analytics og health. | `Services/ExerciseSessionRecorder.cs` |
| MasteryEvaluator | Vurderer mastery over tid basert på stabil og trygg gjennomføring. | `Services/MasteryEvaluator.cs` |
| ProgressionOrchestrator | Vurderer om øvelsesprofil bør beholdes, tilpasses, pauses eller regresseres etter økt. | `Services/ProgressionOrchestrator.cs` |
| ExerciseProfileStore | Lagrer personlige øvelsesprofil-tilpasninger. | `Services/ExerciseProfileStore.cs` |
| Progression-dashboard | Viser progresjonsrelaterte data og anbefalinger. | `Views/ProgressionWindow.xaml`, `Views/ProgressionDashboard.xaml`, `ViewModels/ProgressionDashboardViewModel.cs` |
| Calendar/statistics | Viser historikk, dagdetaljer og statistikk. | `Views/CalendarWindow.xaml`, `Views/DayDetailsWindow.xaml`, `Views/StatisticsWindow.xaml` |

## Personalisering

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Voice Goal Profile | Lagrer brukerens stemmemål og fokus, for eksempel resonans, intonasjon, pust eller pitch-komfort. | `Models/VoiceGoalProfile.cs`, `Services/LocalVoiceGoalProfileStore.cs` |
| Førstegangsoppsett | Lar bruker velge grunninnstillinger ved første oppstart. | `Views/FirstTimeSetupWindow.xaml`, `Services/FirstTimeSetupService.cs` |
| Settings | Håndterer tema, språk, voice goal profile, mic calibration og database-reset. | `Views/SettingsWindow.xaml`, `Services/ThemeManager.cs`, `Services/LocalizationService.cs` |
| Tema | Støtter lys, mørk og systemstyrt visning. | `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml`, `ThemeManager.cs` |
| Språk/RESX | Bruker språkfiler for norsk, engelsk og flere andre språk. | `Resources/Strings*.resx`, `LocalizationService.cs` |

## Analysevinduer

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Analyzer | Viser mer detaljert audio-/spectrogramanalyse og resonansstatus. | `Views/AnalyzerWindow.xaml`, `Views/AnalyzerWindow.xaml.cs` |
| Resonance window | Eget vindu for resonansanalyse med start, stopp og reset. | `Views/ResonanceWindow.xaml`, `Views/ResonanceWindow.xaml.cs` |
| Analysis window | Viser analyse-side/rapportering knyttet til stemmedata. | `Views/AnalysisWindow.xaml`, `ViewModels/AnalysisPageViewModel.cs` |

## Data og persistens

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| DatabaseService | SQLite/database-lag for brukerdata, økter og historikk. | `Data/DatabaseService.cs`, `Data/DatabaseSchema.sql` |
| Repository interfaces | Skiller dataaksess fra app-logikk. | `Data/IUserRepository.cs`, `Data/IScoreRepository.cs`, `Data/IDatabaseService.cs` |
| In-memory repositories | Brukes for testbarhet og fallback der persistent data ikke trengs. | `Services/InMemoryExerciseRepositories.cs` |
| Debug/test services | Støtte for test, debug og lokal verifisering. | `Services/TestSettingsService.cs`, `Services/TestLocalizationService.cs`, `Services/DebugSettingsService.cs` |

## Tester og kvalitetssikring

| Funksjon | Hva den gjør | Viktige filer |
| -------- | ------------ | ------------- |
| Unit tests | Tester scoring, progression, health, calibration, feedback, RESX-policy og exercise-flyt. | `FemVoiceStudio.Tests/*.cs` |
| Release smoke tests | Verifiserer sentrale release-forutsetninger. | `FemVoiceStudio.Tests/ReleaseReadinessSmokeTests.cs` |
| Resource policy tests | Hindrer utrygg brukerrettet språkbruk i språkfiler. | `FemVoiceStudio.Tests/ResourceTextPolicyTests.cs` |
| Pitch/graph tests | Tester target-zoner, pitch trace og chart axis policy. | `PitchTargetZonePolicyTests.cs`, `PitchTraceStabilizerTests.cs`, `PitchChartAxisRangeCalculatorTests.cs` |

## Legacy, merge- og cleanup-kandidater

Disse finnes i prosjektet, men bør behandles forsiktig fordi noen er parallelle eller eldre systemer:

| Område | Kort status |
| ------ | ----------- |
| `Subsystems/*` | Eldre subsystem-lag med audio/data/analysis/progression/smartcoach. Ikke slett blindt, men vurder merge mot aktiv `App.ConfigureServices`-arkitektur. |
| `Infra/DependencyInjection.cs` | Eldre DI-oppsett. Aktiv app bruker primært `App.xaml.cs`. Bør merge-audites før cleanup. |
| `LiveFeedbackView` / `ExerciseSummaryView` | UserControls med egen VM. Ser ut som exercise UX-kandidater som bør merges eller kobles tydelig før eventuell opprydding. |
| `VoiceHealthService` / `VoiceHealthModule` | Eldre/parallel health-linje. Ny aktiv linje er `VocalHealthSupervisor`, men klinisk rolle gjør at den bør merge-audites. |
| `.old`, `.old2`, `AudioAnalysisEngine_new.cs`, `part2.cs` | Rene artefakter/stubs ifølge cleanup-rapport, men sletting bør først skje i egen cleanup-runde. |

## Kort systemflyt

```text
Mikrofon
  -> audio capture/analyse
  -> pitch, resonans, formanter, score, comfort-zone
  -> ExerciseIntelligenceCoordinator / MainViewModel
  -> live UI, pitch-graf og feedback
  -> VocalHealthSupervisor / HydrationAdvisor
  -> SessionAnalyticsStore
  -> ProgressionOrchestrator / SmartCoach
  -> FeedbackPipeline / FeedbackConsistencyGuard
  -> trygg brukerrettet feedback
```

## Viktigste prinsipp

FemVoice skal hjelpe en trans jente med stemmefeminisering uten å gjøre pitch til eneste mål. Systemet skal støtte resonans, stabilitet, komfort, bærekraftig øving, helse og personlig progresjon. Pitch brukes som en del av bildet, men feedback og progresjon skal ikke belønne press, strain eller ekstremt høye verdier.
