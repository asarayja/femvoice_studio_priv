# FEMVOICE – Dead Systems Integration Report

**Type:** Runtime-integrasjonsaudit (post-fix) — hva finnes, hva kjører, hva kjører aldri.
**Dato:** 2026-06-06 · **Metode:** 7 parallelle lag-auditorer + 3 adversarial kryssverifikatorer
(dead-påstander forsøkt motbevist mot XAML/refleksjon/service-locator/DI-fabrikker; active-påstander
sporet tilbake til App.OnStartup; completeness-kritiker fant 10 systemer ingen auditor dekket).
**Definisjon av runtime:** kallkjeder fra `App.OnStartup` → `ConfigureServices` (App.xaml.cs) →
MainWindow → vinduene den åpner. Tester teller ikke. `Infra/DependencyInjection.AddFemVoiceStudio()`
kalles aldri og er IKKE del av runtime.

## Hovedtall

| Status | Antall | Andel |
|---|---|---|
| ✅ ACTIVE | 38 | 33 % |
| 🟡 PARTIAL (deler av API-et kjører) | 12 | 10 % |
| 🟠 DORMANT (instansiert/koblet, mottar aldri data) | 18 | 16 % |
| ❌ DEAD (aldri nådd) | 48 | 41 % |
| **Totalt (etter dedupe)** | **116** | |

> **Det viktigste bildet:** Kjernen er frisk — alle tre lydpipelines, øvelsesløkka
> (koordinator → recorder → supervisor → analytics → gates) og de nylig aktiverte kliniske
> systemene kjører ende-til-ende. Men over halvparten av kodebasen (66 systemer)
> deltar ikke i runtime: tre hele parallellgenerasjoner (Subsystems-laget, RealtimeAnalysisEngine-stakken,
> VoiceHealthModule), den «fem motorer»-arkitekturen koordinatoren ble bygget for (3 av 5 motorer
> sover), og hele den brukerrettede helse-/hydrerings-/progresjons-coachingen via FeedbackPipeline.

---

## Seksjon 1 – Dead System Discovery (fullt inventar)

| System | Lag | DI | Kalt fra produksjon | Status |
|---|---|---|---|---|
| Subsystems/Analysis/AnalysisSubsystem (IAnalysisSubsyst | Analytics | — | INGEN. Kun i SubsystemViewModelBase (ViewModelBase.cs:44/51) og AddFemVoiceStudio (DependencyIn | ❌ DEAD |
| TrendAnalysisService | Analytics | — | INGEN | ❌ DEAD |
| AdaptivePitchDetector | Audio | — | Kun AsyncAudioPipeline.cs:31 (new) — men AsyncAudioPipeline er selv død | ❌ DEAD |
| AsyncAudioPipeline | Audio | — | INGEN | ❌ DEAD |
| RealtimeAnalysisEngine | Audio | — | INGEN | ❌ DEAD |
| SpeechRateAnalyzer | Audio | — | INGEN | ❌ DEAD |
| Subsystems/Audio/AudioSubsystem (IAudioSubsystem) | Audio | — | INGEN. Kun referert i SubsystemViewModelBase (ViewModelBase.cs:43/50/62) og registrert i AddFem | ❌ DEAD |
| VoiceActivityDetector | Audio | — | Kun AsyncAudioPipeline.cs:32 (new) og Subsystems/Analysis/AnalysisSubsystem.cs:31 (new) — begge | ❌ DEAD |
| VoiceMetricsCalculator | Audio | — | INGEN | ❌ DEAD |
| AdaptiveTargetZoneService (SmartCoachModule) | Coaching | — | INGEN. Klassen defineres men 'new AdaptiveTargetZoneService' finnes ikke i produksjon eller tes | ❌ DEAD |
| CoachMessageFormatter | Coaching | — | INGEN. Aldri instansiert — verken i produksjon eller tester. | ❌ DEAD |
| CoachMessageGenerator | Coaching | — | INGEN i produksjon. Eneste 'new CoachMessageGenerator()' er i FemVoiceStudio/Tests/CoachMessage | ❌ DEAD |
| ExerciseFeedbackEngine | Coaching | — | INGEN i produksjon. Refereres kun via LiveFeedbackViewModel.AttachFeedbackEngine (cs:85) og Exe | ❌ DEAD |
| FeedbackRuleEngine (CompositeEvaluator + IRuleEvaluator | Coaching | — | INGEN. CompositeEvaluator og alle IRuleEvaluator-implementasjoner refereres ikke utenfor sin eg | ❌ DEAD |
| SmartCoachExerciseAdapter | Coaching | — | Kun fra LiveFeedbackViewModel (new SmartCoachExerciseAdapter, LiveFeedbackViewModel.cs:72,93) o | ❌ DEAD |
| Subsystems/SmartCoach/SmartCoachSubsystem (ISmartCoachS | Coaching | — | INGEN. Kun i SubsystemViewModelBase (ViewModelBase.cs:45/52) og AddFemVoiceStudio (DependencyIn | ❌ DEAD |
| VoiceFeminizationExerciseService + ResonanceModuleDocum | Coaching | — | INGEN (kun param-referanse i ResonanceModuleDocumentation som selv aldri kalles) | ❌ DEAD |
| IComfortZoneRepository | Data | — | INGEN. Grep gir kun interface-definisjonen; ingen implementasjon, ingen DI-registrering, ingen  | ❌ DEAD |
| Services/VoiceProfileExtensions.cs (VoiceProfileExtensi | Data | — | INGEN i prod. Eneste referanser til VoiceProfileExtensions er testfilen FemVoiceStudio/Tests/Vo | ❌ DEAD |
| Subsystems/Data/DataSubsystem (IDataSubsystem) inkl. ba | Data | — | INGEN. IDataSubsystem injiseres kun i SubsystemViewModelBase (ViewModelBase.cs:46/53), som inge | ❌ DEAD |
| RestProtocolService (VoiceHealthModule) | Helse | — | INGEN. Ikke DI-registrert; ingen produksjons-instansiering (grep: kun selvreferanse). | ❌ DEAD |
| StrainMonitor (VoiceHealthModule) | Helse | — | INGEN. Ikke DI-registrert; ingen produksjons-instansiering (grep: kun selvreferanse). | ❌ DEAD |
| TrendAlertService | Helse | — | INGEN. Ikke DI-registrert; ingen 'new TrendAlertService(...)' i produksjon. (CalculateHealth-tr | ❌ DEAD |
| VoiceHealthService (sesjons-/pause-timer) | Helse | — | INGEN. Ikke DI-registrert, ingen 'new VoiceHealthService(...)' i produksjon (grep: kun selve fi | ❌ DEAD |
| VoiceStrainDetector | Helse | manuell new | Konstrueres i RealtimeAnalysisEngine (Audio/RealtimeAnalysisEngine.cs:97) og AnalysisSubsystem  | ❌ DEAD |
| Infra/DependencyInjection.cs (AddFemVoiceStudio + Servi | Infra | — | INGEN. App.OnStartup (App.xaml.cs:30) kaller ConfigureServices, ikke AddFemVoiceStudio. Grep vi | ❌ DEAD |
| ServiceCollectionExtensions.AddFemVoiceStudio + alle Su | Infra | — | INGEN. AddFemVoiceStudio() har ingen kallere (kun definisjonen finnes). App.OnStartup bruker Co | ❌ DEAD |
| AdaptiveDifficultyService | Progresjon | — | INGEN | ❌ DEAD |
| GamificationService | Progresjon | — | INGEN | ❌ DEAD |
| Models/PeriodizationModels.cs (PeriodizationConfig/Stat | Progresjon | — | INGEN. Konsumeres kun av PeriodizationService, som ikke har EN eneste prod-kaller (grep gir 0 t | ❌ DEAD |
| Models/TrainingLoad.cs (TrainingLoad, IntensityLevel, T | Progresjon | — | INGEN aktiv. TrainingLoad refereres kun som valgfri property på WeeklySchedule (WeeklySchedule. | ❌ DEAD |
| Models/UserProgressionProfile.cs | Progresjon | — | INGEN aktiv. Konsumeres kun av ProgressionEngine (ProgressionEngine.cs:20/35/79) og WeeklyPlann | ❌ DEAD |
| Models/WeeklySchedule.cs (WeeklySchedule, ScheduledSess | Progresjon | — | WeeklySchedule/ScheduledSession konsumeres kun av WeeklyPlannerEngine (død). TargetAdjustment/P | ❌ DEAD |
| PeriodizationService | Progresjon | — | INGEN | ❌ DEAD |
| ProgressionConfig | Progresjon | — | Kun fra døde ProgressionEngine/WeeklyPlannerEngine | ❌ DEAD |
| ProgressionDashboardViewModel (ViewModels/) | Progresjon | — | INGEN | ❌ DEAD |
| ProgressionEngine (Services/Progression/) | Progresjon | — | INGEN i produksjon — kun registrert i Infra/DependencyInjection.cs:28 via AddFemVoiceStudio(),  | ❌ DEAD |
| ProgressionOrchestrator | Progresjon | Singleton | INGEN (kun registrert i App.xaml.cs:137; aldri resolvet via GetService) | ❌ DEAD |
| ProgressionRateCalculator | Progresjon | — | INGEN | ❌ DEAD |
| ProgressionSubsystem (Subsystems/Progression/) | Progresjon | — | INGEN i produksjon — registrert i Infra/DependencyInjection.cs:35 (AddFemVoiceStudio, aldri kal | ❌ DEAD |
| Subsystems/Progression/ProgressionSubsystem (IProgressi | Progresjon | — | INGEN. Kun i SubsystemViewModelBase (ViewModelBase.cs:42/49) og AddFemVoiceStudio (DependencyIn | ❌ DEAD |
| WeeklyPlannerEngine (Services/Progression/) | Progresjon | — | INGEN i produksjon — kun Infra/DependencyInjection.cs:29 (AddFemVoiceStudio, aldri kalt) | ❌ DEAD |
| ExerciseListViewModel | UI | — | INGEN (kun RelayCommand-TYPEN i fila brukes av andre) | ❌ DEAD |
| ExerciseSummaryView + ExerciseSummaryViewModel | UI | — | INGEN. ExerciseSummaryView forekommer kun i egen XAML (x:Class). ExerciseSummaryViewModel har i | ❌ DEAD |
| LiveFeedbackView + LiveFeedbackViewModel | UI | — | INGEN. LiveFeedbackView forekommer kun i sin egen XAML (x:Class) — ingen annen vindu/UserContro | ❌ DEAD |
| PitchChartViewModel | UI | — | INGEN. Null prod-referanser (kun egen definisjon). | ❌ DEAD |
| ViewModelBase / SubsystemViewModelBase | UI | — | INGEN. Grep på ': ViewModelBase' og ': SubsystemViewModelBase' gir kun definisjonene selv. Alle | ❌ DEAD |
| ViewModels/ProgressionDashboardViewModel (FemVoiceStudi | UI | — | INGEN. Den AKTIVE varianten er den nestede klassen i Views/ProgressionDashboard.xaml.cs (instan | ❌ DEAD |
| HydrationFeedbackMapper | Coaching | Singleton | INGEN. Registrert i App.xaml.cs:172, men .Map(HydrationAdvice) kalles ingen steder i produksjon | 🟠 DORMANT |
| ProgressionFeedbackMapper | Coaching | Singleton | INGEN. Registrert i App.xaml.cs:162, men .Map(ProgressionOrchestratorDecision) kalles ingen ste | 🟠 DORMANT |
| SmartCoachFeedbackMapper | Coaching | Singleton | SmartCoachEngine.SaveCoachMessageThroughPipeline: .Map (SmartCoachEngine.cs:645) og .BuildConte | 🟠 DORMANT |
| TrainingFrequencyService | Coaching | manuell new | ExerciseWindow.InitializeServices: new TrainingFrequencyService(_exerciseService) (ExerciseWind | 🟠 DORMANT |
| VocalHealthFeedbackMapper | Coaching | Singleton | INGEN. Registrert i App.xaml.cs:169, men .Map(VocalHealthDecision) kalles ingen steder i produk | 🟠 DORMANT |
| ExerciseProfileStore (write-path) | Data | Singleton | INGEN i produksjon (SaveAsync kun fra tester) | 🟠 DORMANT |
| IExerciseProfileStore -> SqliteExerciseProfileStore | Data | Singleton | INGEN. Ingen produksjonsklasse ctor-injiserer eller resolver IExerciseProfileStore. Eneste refe | 🟠 DORMANT |
| InMemoryExerciseRepository (IUserRepository + IScoreRep | Data | Singleton | App.xaml.cs:115-117 registrerer den som seg selv + IUserRepository + IScoreRepository. Injisere | 🟠 DORMANT |
| MasteryLevel/ExerciseProfileStore (IExerciseProfileStor | Data | Singleton | INGEN (registrert i App.xaml.cs:127 som SqliteExerciseProfileStore; aldri resolvet) | 🟠 DORMANT |
| ComfortZoneController (ZoneUpdated) | Helse | Singleton | INGEN av de tilstandsendrende metodene. Konstrueres av DI (App.xaml.cs:106) og injiseres i koor | 🟠 DORMANT |
| ComfortZoneState | Helse | — | Kun som type i dormant ComfortZoneController/koordinator-handler | 🟠 DORMANT |
| HydrationAdvisor | Helse | Singleton | INGEN. DI-konstruert med baseline-avledede HydrationAdvisorOptions (App.xaml.cs:171, options fr | 🟠 DORMANT |
| HydrationAdvisorOptions (factory-reg) | Helse | Singleton | Transitivt ctor-arg til HydrationAdvisor-fabrikken (App.xaml.cs:171) — men HydrationAdvisor er  | 🟠 DORMANT |
| Subjektiv rapport i ExerciseWindow (_lastSubjectiveRepo | Helse | — | OnSubmitSubjectiveReportClick (ExerciseWindow.xaml.cs:653) skriver feltet (linje 657). | 🟠 DORMANT |
| VocalHealthLegacyBridge | Helse | Singleton | Den DI-registrerte instansen (App.xaml.cs:170): INGEN konsumenter. Koordinatoren bruker en EGEN | 🟠 DORMANT |
| VoiceHealthMonitor (HealthWarning/HealthCritical/Lockou | Helse | Singleton | INGEN. Konstrueres av DI (App.xaml.cs:107) og injiseres i ExerciseIntelligenceCoordinator (Serv | 🟠 DORMANT |
| FemVoiceScoreEngine | Progresjon | Singleton | App.xaml.cs:105 (singleton). Konstrueres av ExerciseIntelligenceCoordinator (ctor, koordinator. | 🟠 DORMANT |
| Services/ZoneConfiguration.cs (ZoneConfiguration, ZoneC | Progresjon | — | Konsumeres kun av ComfortZoneController (felt _configuration, ComfortZoneController.cs:64/84/88 | 🟠 DORMANT |
| SessionAnalyticsStore | Analytics | Singleton | ExerciseSessionRecorder.PersistAsync (Services/ExerciseSessionRecorder.cs:269,286,299,314) ← Ex | 🟡 PARTIAL |
| FeedbackConsistencyGuard | Coaching | Singleton | FeedbackPipeline.Submit (FeedbackPipeline.cs:22) ← ExerciseDetailViewModel.cs:917 (inline coach | 🟡 PARTIAL |
| FeedbackPipeline | Coaching | Singleton | ExerciseDetailViewModel.SubmitInlineCoachMessage (ExerciseDetailViewModel.cs:917) ← OnInlineCoa | 🟡 PARTIAL |
| FeedbackService | Coaching | manuell new | MainViewModel: new FeedbackService() (MainViewModel.cs:175). GenerateFeedback kalles i StopReco | 🟡 PARTIAL |
| SmartCoachEngine | Coaching | Singleton | To distinkte instanser: (1) DI-instansen (med FeedbackPipeline+SmartCoachFeedbackMapper, App.xa | 🟡 PARTIAL |
| DatabaseService | Data | Singleton | INGEN via DI. Registrert som singleton + aliaset til IDatabaseService (App.xaml.cs:95), men ing | 🟡 PARTIAL |
| ExerciseDataService (Data.ExerciseDataService) | Data | Singleton | Resolves i ExerciseDetailViewModel.EnsureProgressService (ExerciseDetailViewModel.cs:187, App.S | 🟡 PARTIAL |
| IDatabaseService (alias) | Data | Singleton | Transitivt fra DI-SmartCoachEngine-fabrikken (App.xaml.cs:175: sp.GetRequiredService<IDatabaseS | 🟡 PARTIAL |
| IVoiceGoalProfileProvider -> LocalVoiceGoalProfileStore | Data | Singleton | Transitivt ctor-injisert i DI-SmartCoachEngine (App.xaml.cs:179) OG resolvet direkte i Settings | 🟡 PARTIAL |
| ComplexityEngine | Progresjon | manuell new | SmartCoachDetailWindow ctor:19 (DI SmartCoachViewModel) → OnLoaded:34 → InitializeAsync → LoadD | 🟡 PARTIAL |
| IExerciseProfileFactory -> ExerciseProfileFactory | Progresjon | Singleton | Ctor-injisert i ExerciseDetailViewModel (cs:149) OG resolvet direkte i ExerciseWindow.xaml.cs:1 | 🟡 PARTIAL |
| LevelClassificationSystem | Progresjon | manuell new | Statiske display-metoder: Views/ProgressionDashboard.xaml.cs:223-225 (GetLevelName/Emoji/Focus) | 🟡 PARTIAL |
| ExerciseSessionRecorder | Analytics | Singleton | App.OnStartup → MainWindow → ExerciseWindow ctor:69 → InitializeLiveFeedback:111 → App.Services | ✅ ACTIVE |
| SpectrogramResonanceMapper | Analytics | manuell new | new() i AnalyzerWindow (AnalyzerWindow.xaml.cs:38). AnalyzerWindow åpnes fra MainWindow.xaml.cs | ✅ ACTIVE |
| AudioAnalysisEngine | Audio | manuell new | MainViewModel ctor (MainViewModel.cs:180 new, :181-183 event-abonnement) → StartRecording (:343 | ✅ ACTIVE |
| AudioAnalyzerService | Audio | manuell new | MainViewModel ctor (MainViewModel.cs:177 new) → InitializeAudio:279 Initialize, StartRecording: | ✅ ACTIVE |
| AudioCaptureService | Audio | manuell new | ExerciseWindow.StartExerciseAudio (ExerciseWindow.xaml.cs:689 new + :710 StartRecording) ← OnSt | ✅ ACTIVE |
| FormantDetectionService | Audio | manuell new | ResonanceWindow.InitializeServices (ResonanceWindow.xaml.cs:67 new) → OnAudioDataAvailable:118  | ✅ ACTIVE |
| MicrophoneCalibrationService | Audio | manuell new | MicrophoneCalibrationWindow.FinishCalibration (MicrophoneCalibrationWindow.xaml.cs:141 new, :14 | ✅ ACTIVE |
| PitchDetectionService | Audio | manuell new | ExerciseWindow.StartExerciseAudio (ExerciseWindow.xaml.cs:688 new) → OnExerciseAudioDataAvailab | ✅ ACTIVE |
| ResonanceProxyEngine | Audio | Singleton | DI-registrert i App.ConfigureServices (App.xaml.cs:104). ExerciseWindow.StartExerciseAudio hent | ✅ ACTIVE |
| ResonansScoringService | Audio | manuell new | ResonanceWindow.InitializeServices (ResonanceWindow.xaml.cs:70 new) → OnAudioDataAvailable:123  | ✅ ACTIVE |
| AdaptiveComfortZoneService | Coaching | manuell new | MainViewModel: new AdaptiveComfortZoneService(_smartCoach) (MainViewModel.cs:192). GenerateExpl | ✅ ACTIVE |
| ExerciseIntelligenceCoordinator | Coaching | Singleton | Transitivt ctor-arg til ExerciseDetailViewModel (ExerciseDetailViewModel.cs:147) og ExerciseSes | ✅ ACTIVE |
| ExerciseTextService | Coaching | manuell new | MainViewModel: new ExerciseTextService() (MainViewModel.cs:174) — GetRandomText/GetLocalizedCon | ✅ ACTIVE |
| FemVoiceScore (live-score-kalkulatoren) | Coaching | manuell new | MainViewModel.cs:189 (new), :705 Calculate() i CalculateLiveScore ← OnPitchAnalyzed/OnPitchUpda | ✅ ACTIVE |
| GuidanceItems (ExerciseDetailViewModel) | Coaching | — | ExerciseDetailViewModel.RebuildGuidanceItems (cs:949) ← ApplyProfile (cs:216) ← ExerciseWindow. | ✅ ACTIVE |
| InlineCoachFeedbackMapper | Coaching | Singleton | ExerciseDetailViewModel.SubmitInlineCoachMessage: .Map (ExerciseDetailViewModel.cs:909) og .Bui | ✅ ACTIVE |
| Models/VoiceGoalProfile.cs | Coaching | — | LocalVoiceGoalProfileStore (Get/SaveProfile), SettingsWindow.xaml.cs:152, SmartCoachEngine.cs:2 | ✅ ACTIVE |
| VoiceGoalProfile-systemet (IVoiceGoalProfileProvider /  | Coaching | Singleton | App.xaml.cs:173 (IVoiceGoalProfileProvider->LocalVoiceGoalProfileStore). Injiseres i den DI-byg | ✅ ACTIVE |
| ISessionAnalyticsRepository -> SqliteSessionAnalyticsRe | Data | Singleton | Transitivt ctor-arg til SessionAnalyticsStore (SessionAnalyticsStore.cs:573), som naas via Exer | ✅ ACTIVE |
| Models/ComplexityModels.cs (ComplexityEvaluation, Speec | Data | — | Konsumeres av ComplexityEngine, som 'new'-es i prod av aktive VM-er: AnalysisPageViewModel.cs:7 | ✅ ACTIVE |
| LiveMetricsService.CalculateHealth | Helse | manuell new | MainViewModel: 'new LiveMetricsService()' (ViewModels/MainViewModel.cs:191); CalculateHealth ka | ✅ ACTIVE |
| VocalHealthBaselineProvider | Helse | Singleton | App.ConfigureServices via DI-fabrikklambdaer: CreateVocalHealthOptions (App.xaml.cs:166) og Cre | ✅ ACTIVE |
| VocalHealthSupervisor (+ VocalHealthTrendEngine) | Helse | Singleton | ExerciseWindow.OnExerciseAudioDataAvailable (Views/ExerciseWindow.xaml.cs:767) -> ExerciseDetai | ✅ ACTIVE |
| VocalHealthSupervisorOptions (factory-reg) | Helse | Singleton | Transitivt ctor-arg til VocalHealthSupervisor-fabrikken (App.xaml.cs:168). Naas naar ExerciseSe | ✅ ACTIVE |
| DebugSettingsService | Infra | — | Singleton via .Instance. App.OnStartup:35 (EnsureDebugSection). MainViewModel.cs:319/373/530/57 | ✅ ACTIVE |
| FirstTimeSetupService | Infra | — | Singleton via .Instance. App.OnStartup:33 (IsFirstTime styrer FirstTimeSetupWindow). FirstTimeS | ✅ ACTIVE |
| ILocalizationService -> LocalizationService.Instance | Infra | Singleton | Transitivt: ctor-injisert i ExerciseDetailViewModel (ExerciseDetailViewModel.cs:148) og i DI-Sm | ✅ ACTIVE |
| ClinicalSessionScore | Progresjon | — | ExerciseWindow.xaml.cs:638 i CompleteSessionAndCalculateScore ← OnStopClick:365 | ✅ ACTIVE |
| DirectionAnalyzer (Services/DirectionAnalyzer.cs) | Progresjon | manuell new | new() i Views/ProgressionDashboard.xaml.cs:158/178. ProgressionDashboard (aktiv, hostet av Prog | ✅ ACTIVE |
| MasteryEvaluator | Progresjon | Singleton | App.OnStartup → MainWindow → ExerciseWindow → ExerciseDetailViewModel (DI-injisert ctor-param,  | ✅ ACTIVE |
| ProgressionSafetyGate | Progresjon | Singleton | App.OnStartup → MainWindow → MainViewModel ctor → App.Services.GetService(typeof(ProgressionSaf | ✅ ACTIVE |
| ProgressionService | Progresjon | manuell new | MainViewModel ctor:176 (new ProgressionService(_database)). EvaluateProgressionWithSafety:434,  | ✅ ACTIVE |
| ExerciseDetailViewModel | UI | Transient | Resolves i ExerciseWindow.xaml.cs:115. ExerciseWindow aapnes fra MainWindow.xaml.cs:485. | ✅ ACTIVE |
| PitchTargetZonePolicy | UI | — | MainViewModel.cs:301/474/489 (ClampForDifficulty/ForDifficulty) | ✅ ACTIVE |
| PitchTraceStabilizer | UI | manuell new | MainWindow.xaml.cs:29 (new), :334 Filter() i pitch-loopen | ✅ ACTIVE |
| SmartCoachViewModel | UI | Transient | Resolves i SmartCoachDetailWindow.xaml.cs:19 (GetRequiredService) og SmartCoachDashboardView.xa | ✅ ACTIVE |
| ThemeManager | UI | — | Singleton via .Instance. App.OnStartup:34 (Initialize). SettingsWindow.xaml.cs:46/67/71/75/84 ( | ✅ ACTIVE |
| Vindus-ViewModels (Calendar/Statistics/DayDetails/Analy | UI | manuell new | CalendarWindow:20, StatisticsWindow:18, CalendarViewModel:254, AnalysisWindow:26, ResonanceWind | ✅ ACTIVE |

---

## Seksjon 2 – Broken Runtime Chains

### Kjede A: Helse i øvelsesløkka — *1 av 3 signalkilder aktiv*
```
Forventet:  Audio → Coordinator → {VocalHealthSupervisor + VoiceHealthMonitor + ComfortZoneController} → Analytics → Gates
Faktisk:    Audio → Coordinator → VocalHealthSupervisor → Analytics → Gates        ✅ (aktiv etter fiksen)
                          ↛ VoiceHealthMonitor.Analyze()                            🟠 kalles aldri → HealthWarning/Critical/Lockout fyrer aldri
                          ↛ ComfortZoneController.InitializeAsync/UpdateZoneAsync   🟠 kalles aldri → ZoneUpdated fyrer aldri
```
**Manglende kall:** `_healthMonitor.Analyze(voiceMetrics)` (ingen produksjonskode bygger VoiceMetrics til den)
og `comfortZoneController.InitializeAsync(userId)` + `UpdateZoneAsync(...)`.
**Impact:** Adaptiv komfortsone justeres aldri (koordinatoren seeder pitch-grenser kun fra profilen,
Coordinator.cs:282-285); perturbasjonsbasert helse (jitter/shimmer/HNR) bidrar aldri; koordinatorens
`OnHealthWarning/Critical/Lockout`-handlere + `VocalHealthLegacyBridge` er reelt død kode i runtime.

### Kjede B: Koordinatorens «fem motorer» — *3 av 5 sover*
```
Forventet:  ResonanceProxyEngine + FemVoiceScoreEngine + ComfortZoneController + VoiceHealthMonitor + SmartCoachEngine → Coordinator
Faktisk:    ResonanceProxyEngine ✅ (via ExerciseWindow ProcessSamples)
            FemVoiceScoreEngine  🟠 CalculateScoreAsync kalles aldri → ScoreUpdated fyrer aldri
            ComfortZoneController 🟠 (se kjede A)
            VoiceHealthMonitor   🟠 (se kjede A)
            SmartCoachEngine     🟡 injisert men koordinatoren KALLER den aldri (død avhengighet i evalueringsløkka)
```
**Impact:** Koordinatoren lever i praksis av `UpdateMetrics`-argumentene fra ExerciseWindow, ikke av
motorene den ble designet rundt. Stabilitet = pitch.Confidence fra vinduet, ikke score-motorens beregning.

### Kjede C: Analytics skrive-sti — *øvelsesnivå aktivt, sesjonsnivå aldri*
```
Forventet:  RecordSessionStartedAsync → RecordExercisePerformanceAsync → RecordSessionCompletedAsync
Faktisk:    RecordExercisePerformanceAsync + RecordHealthEventAsync ✅ (ExerciseSessionRecorder.cs:269-322)
            RecordSessionStartedAsync/RecordSessionCompletedAsync   ❌ null kallere → SessionAnalyticsSessions-tabellen alltid tom
```
**Impact:** `GetDailySummaryAsync`/`GetWeeklyTrendAsync` aggregerer fra en tom sesjonstabell —
alt sesjonsbasert (daglig/ukentlig trend) er tomt. Eneste tiltenkte leser er den døde ProgressionOrchestrator.

### Kjede D: Brukerrettet helse-/hydrerings-/progresjonscoaching — *hele grenen kald*
```
Forventet:  {VocalHealthDecision, HydrationAdvice, ProgressionDecision} → FeedbackMapper → FeedbackPipeline → UI
Faktisk:    Kun InlineCoach- og SmartCoach-mapperne kalles. VocalHealth-/Hydration-/Progression-mapperne: .Map() kalles aldri.
```
**Manglende kall:** `vocalHealthFeedbackMapper.Map(decision)` etter `_healthSupervisor.Evaluate` i
ExerciseSessionRecorder.OnExerciseUpdated; `hydrationAdvisor.Evaluate(state)` (kalles aldri) + mapper;
progresjonsbeslutning → mapper i MainViewModel.StopRecording.
**Impact:** Helse påvirker lås/score (sidekanal via CurrentHealthScore) men genererer ALDRI en
brukerrettet coach-melding. Hydreringspåminnelser når aldri brukeren.

### Kjede E: Subjektiv selvrapport — *write-only*
```
Forventet:  SubjectiveReport → recorder/orchestrator (IndicatesHealthConcern → pause)
Faktisk:    ExerciseWindow lagrer _lastSubjectiveReport (:629) — feltet LESES ALDRI.
```
**Impact:** Brukerens egenrapporterte komfort/fatigue/strain påvirker ingenting.

### Kjede F: Det døde subsystem-laget
```
Forventet (gen-2-arkitektur):  AddFemVoiceStudio() → I*Subsystem → SubsystemViewModelBase → VMs
Faktisk:                       AddFemVoiceStudio() kalles aldri; ingen VM arver SubsystemViewModelBase.
```
**Impact:** Hele `Subsystems/` + `Infra/` + ProgressionEngine/WeeklyPlannerEngine/ProgressionConfig
(~tusenvis av linjer) er en parallell generasjon uten én eneste runtime-tråd inn.

### Kjede G (subtil, verdt å kjenne til): To opptaksflyter, én gating-database
MainWindow-opptak (LiveMetricsService-helse) og ExerciseWindow-øvelser (Supervisor-helse) skriver/leser
samme `femvoice.db`. Gatingen fungerer på tvers — men en bruker som KUN bruker MainWindow-opptak
genererer aldri SafetyFreeze/StrainPeriod/ComfortZoneBreach-events; da gjelder kun in-memory
`RecordStrainIncident` fra WarningFlags.

---

## Seksjon 3 – Event Wiring Audit

60 events kartlagt: **13 fired-and-consumed · 19 fyres-men-ingen-abonnent (partial) ·
7 abonnert-men-fyrer-aldri (dormant/orphan-handler) · 21 verken fyres eller abonneres (dead).**

### Orphan-handlers (abonnent venter, eventet fyrer aldri) — *de farligste*
| Event | Abonnent (venter forgjeves) | Hvorfor det aldri fyrer |
|---|---|---|
| FemVoiceScoreEngine.ScoreUpdated | Coordinator.cs:98 | CalculateScoreAsync kalles aldri i prod |
| ComfortZoneController.ZoneUpdated | Coordinator.cs:99 | InitializeAsync/UpdateZoneAsync kalles aldri |
| VoiceHealthMonitor.HealthWarning/HealthCritical/LockoutTriggered | Coordinator.cs:100-102 | Analyze() kalles aldri |
| ComplexityEngine.ComplexityLevelChanged | (lesesti) | TryAdvanceLevelAsync kalles aldri |
| HydrationAdvisor.HydrationSuggested | — | Evaluate() kalles aldri |

### Fyres-men-ingen-abonnent (funksjonen virker via returverdi; event-API-et er ren overflate)
VocalHealthSupervisor: alle 7 events (HealthStateUpdated/StrainDetected/FatigueDetected/PauseRecommended/
HydrationSuggested/RestrictTriggered/LockTriggered) · ProgressionService.SafetyLockEngaged/Released ·
FeedbackPipeline + FeedbackConsistencyGuard: Approved/Suppressed/Escalated ·
AudioAnalysisEngine.ConfidenceUpdated/RawPitchUpdated · ResonanceProxyEngine.ErrorOccurred ·
ThemeManager.PropertyChanged.
**Merk:** At supervisorens `LockTriggered` ikke har abonnent er ikke funksjonstap — låsen propageres via
`CurrentHealthScore=40` → koordinatorens <70-terskel. Men pause-/hydreringsanbefalinger tapes som varsler.

### Døde events (verken publisher-sti eller abonnent i runtime)
Alle events på: ProgressionOrchestrator (5), GamificationService (2), PeriodizationService (2),
VoiceHealthService (3), RestProtocolService (3+), StrainMonitor (3), RealtimeAnalysisEngine (3+),
AsyncAudioPipeline (2), ExerciseFeedbackEngine (3), VoiceHealthMonitor.LockoutEnded/HealthRecovered.

---

## Seksjon 4 – Dependency Injection Audit

### Registrert men aldri resolvet (DI-instansen oppstår aldri / brukes aldri)
| Registrering (App.xaml.cs) | Status |
|---|---|
| ProgressionOrchestrator (:137) | Aldri GetService/injisert — eneste eksterne referanse er typebruk i FeedbackPipeline |
| HydrationAdvisor (:171) + HydrationFeedbackMapper (:172) | Options-fabrikk kjører, men Evaluate/Map kalles aldri |
| VocalHealthFeedbackMapper (:169) | .Map() kalles aldri |
| ProgressionFeedbackMapper (:162) | .Map() kalles aldri |
| VocalHealthLegacyBridge (:170) | Koordinatoren new-er sin EGEN bridge (Coordinator.cs:90/:113) — DI-instansen ubrukt |
| IExerciseProfileStore (:127-134) | Aldri resolvet; SaveAsync-skrivestien har null kallere |

### Resolvet men «sover» (injisert i aktiv klasse, men aldri drevet)
FemVoiceScoreEngine, ComfortZoneController, VoiceHealthMonitor — alle tre injiseres i den aktive
koordinatoren som abonnerer på eventene deres, men ingen kaller metodene som fyrer dem (se Seksjon 2B).
SmartCoachEngine injiseres i koordinatoren som aldri kaller den.

### Manuell `new` der DI-registrering finnes (dobbel-instansiering)
| Tjeneste | DI | Manuelle new-steder |
|---|---|---|
| DatabaseService | Singleton (:94) | ~15 steder: MainViewModel:173, StatisticsViewModel:40, CalendarViewModel:47, AnalysisPageViewModel:112, SettingsWindow:20, ResonanceWindow:100/297, ExerciseWindow.GetHearOwnVoiceSetting:752, m.fl. — skjema-init kan kjøre per instans; singleton-kontrakten reelt brutt |
| SmartCoachEngine | Singleton m/full graf (:174-179) | Degradert `new SmartCoachEngine(db)` UTEN pipeline/mappere/goal-provider: MainViewModel:190, AdaptiveTargetZoneService:40 (død), ProgressionDashboard:156/176 |
| ResonanceProxyEngine | Singleton (:104) | AnalyzerWindow:53 `new ResonanceProxyEngine(sampleRate)` (ctor-arg støttes ikke av DI-registreringen) |
| ExerciseDataService | Singleton (:144-159) | ExerciseWindow.InitializeServices:86 |
| ExerciseProfileFactory | Singleton (:158) | ExerciseWindow:119/:493 fallback-new |
| MainViewModel | (ikke registrert) | MainWindow.xaml.cs:59 `new MainViewModel()` — hele hovedflyten står utenfor DI |

### Dobbeltregistrering
ExerciseIntelligenceCoordinator registreres både i App.ConfigureServices (:112) og i den døde
Infra/DependencyInjection.cs:47 — kun førstnevnte teller.

---

## Seksjon 5 – Health Layer Activation Audit

| System | Created? | Called? | Mottar data? | Produserer? | Konsumeres? | Status |
|---|---|---|---|---|---|---|
| VocalHealthSupervisor (+TrendEngine) | ✅ DI | ✅ Evaluate per tick (Recorder:217) | ✅ ExerciseLiveState | ✅ VocalHealthDecision | ✅ Recorder (helsescore Lock→40/Restrict→72/Caution→85 + fatigue/strain-telling) | ✅ ACTIVE |
| LiveMetricsService.CalculateHealth | ✅ new (MainViewModel:191) | ✅ per pitch-event | ✅ RMS/pitch | ✅ HealthState | ✅ MainWindow-badge + FemVoiceScore-input | ✅ ACTIVE |
| VocalHealthBaselineProvider | ✅ DI | ✅ options-fabrikker (:166-167) | ✅ DB-baseline | ✅ Supervisor-/Hydration-options | ✅ Supervisor | ✅ ACTIVE |
| VoiceHealthMonitor | ✅ DI | ❌ Analyze() aldri kalt | ❌ | ❌ events fyrer aldri | ❌ (koordinator-handlere nås aldri) | 🟠 DORMANT |
| ComfortZoneController | ✅ DI | ❌ Initialize/UpdateZone aldri kalt | ❌ får aldri pitch | ❌ ZoneUpdated fyrer aldri | ❌ | 🟠 DORMANT |
| HydrationAdvisor | ✅ DI (options kjører) | ❌ Evaluate aldri kalt | ❌ | ❌ | ❌ | 🟠 DORMANT |
| VocalHealthLegacyBridge | ✅ DI + intern new | ❌ (henger på monitor-events) | ❌ | ❌ | ❌ | 🟠 DORMANT |
| ComfortZoneState | (type) | — | ❌ | ❌ | ❌ | 🟠 DORMANT |
| VoiceHealthService | ❌ aldri instansiert | ❌ | ❌ | ❌ | ❌ | ❌ DEAD |
| TrendAlertService | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ DEAD |
| RestProtocolService (VoiceHealthModule) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ DEAD |
| StrainMonitor (VoiceHealthModule) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ DEAD |
| VoiceStrainDetector | ❌ (kun døde verter) | ❌ | ❌ | ❌ | ❌ | ❌ DEAD |

*Merk: «StrainDetectionPolicy/FatigueDetectionPolicy/RecoveryPolicy» fra audit-bestillingen finnes ikke
som egne klasser — funksjonene ligger i VocalHealthSupervisor (EvaluateStrain/EvaluateFatigue/Deescalate),
som ER aktiv.*

---

## Seksjon 6 – Progression Layer Activation Audit

| System | Input-kilde | Runtime-trigger | Output-konsument | Status |
|---|---|---|---|---|
| SessionAnalyticsStore | ExerciseSessionRecorder (øvelser+helse-events) | Øktslutt i ExerciseWindow | MasteryEvaluator, ProgressionSafetyGate | 🟡 PARTIAL (sesjonsnivå-API-et aldri kalt) |
| MasteryEvaluator | Analytics-trend (90 d) + ExerciseProgress | ApplyProfile → LoadMasteryAsync (EDVM:577) | Mastery-badge (ExerciseWindow.xaml:859) | ✅ ACTIVE |
| ProgressionSafetyGate | HealthEvents + summaries (7/14 d) | MainViewModel.StopRecording:427 | ProgressionService.ApplyExternalSafetyBlock | ✅ ACTIVE |
| ClinicalSessionScore | ExerciseSessionOutcome | OnStopClick → :638 | ExerciseProgress.AverageScore | ✅ ACTIVE |
| ProgressionService | TrainingSession + strain-flagg | StopRecording:434 (WithSafety) | DifficultyLevel/streaks i UserSettings | ✅ ACTIVE |
| ComplexityEngine | Siste 10 økter (DB) | SmartCoachDetailWindow → LoadComplexityDataAsync | Dashboard-visning | 🟡 PARTIAL (TryAdvanceLevelAsync aldri kalt — nivå kan aldri heves) |
| ProgressionOrchestrator | (skulle: analytics-trend) | ❌ ALDRI resolvet/kalt | ❌ ingen | ❌ DEAD (fase 2-kandidat) |
| IExerciseProfileStore | (skulle: orchestrator-output) | ❌ SaveAsync aldri kalt | ❌ | ❌ DEAD (kollateral av orchestrator) |
| LevelClassificationSystem.Classify | — | ❌ aldri kalt (kun statiske display-metoder brukes) | — | 🟠 DORMANT |
| GamificationService / PeriodizationService / AdaptiveDifficultyService / ProgressionRateCalculator / ProgressionEngine / WeeklyPlannerEngine / ProgressionConfig / ProgressionSubsystem | — | ❌ aldri instansiert | — | ❌ DEAD |

---

## Seksjon 7 – Coaching Layer Activation Audit

| System | Klassifisering | Detalj |
|---|---|---|
| InlineCoach-strømmen (Coordinator → EDVM → FeedbackPipeline → UI) | **Active** | Hel kjede verifisert (Submit: EDVM:917) |
| Guidance-systemet (GuidanceItems) | **Active** | Bygges per profil i EDVM, bindes i ExerciseWindow |
| FeedbackPipeline + FeedbackConsistencyGuard | **Partially Active** | Submit-stien aktiv KUN for inline-coach; Approved/Suppressed/Escalated-events uten abonnenter; 3 av 5 mappere kalles aldri |
| SmartCoachEngine | **Partially Active** | Dashboard-metodene aktive via SmartCoachDetailWindow; AnalyzeSessionForStrain + GenerateMotivationalMessages aldri kalt; degradert dobbel-instansiering (MainViewModel:190 uten feedback-graf); injisert i koordinatoren som aldri kaller den |
| SmartCoachFeedbackMapper | **Partially Active** | Kalles kun i DI-SmartCoachEngine-stien (ExerciseWindow-transitiv) |
| VocalHealth-/Hydration-/ProgressionFeedbackMapper | **Dormant** | .Map() kalles aldri — hele den brukerrettede helse-coachingen kald |
| AdaptiveComfortZoneService | **Active** | MainViewModel:191 → UpdateComfortZone (SessionType-anbefaling) |
| TrainingFrequencyService / ExerciseTextService / FeedbackService | **Active** | ExerciseWindow/MainViewModel |
| CoachMessageGenerator / CoachMessageFormatter | **Dead** | Aldri instansiert |
| ExerciseFeedbackEngine + FeedbackRuleEngine/ (IRuleEvaluator m.fl.) | **Dead** | Kun nådd via døde LiveFeedbackViewModel/tester |
| SmartCoachExerciseAdapter / AdaptiveTargetZoneService (SmartCoachModule) | **Dead** | Verter (ExerciseSummaryViewModel m.fl.) instansieres aldri |
| SmartCoachDashboardView (UserControl) | **Dead** | Hostes aldri i noen XAML (verifisert av kryssverifikator) — SmartCoachDetailWindow (i SmartCoachDetailView.xaml.cs!) er den aktive verten |

---

## Seksjon 8 – Runtime Coverage Score

Score = (ACTIVE + ½·PARTIAL) / totalt antall systemer i laget.

| Lag | Dekning | ACTIVE / PARTIAL / DORMANT / DEAD |
|---|---|---|
| **Helse** | **25 %** | 4 / 0 / 7 / 5 — kun Supervisor-stien + LiveMetrics av tre tiltenkte signalkilder |
| **Progresjon** | **26 %** | 5 / 3 / 2 / 15 — kjernen aktiv, men 4 parallellsystemer døde |
| **Data** | **31 %** | 2 / 4 / 4 / 3 — fungerer, men DI-kontrakten brutt av ~15 manuelle new |
| **Coaching** | **40 %** | 8 / 4 / 5 / 8 — inline-coach hel; helse-/hydrerings-coaching kald |
| **Analytics** | **50 %** | 2 / 1 / 0 / 2 — øvelsesnivå aktivt; sesjonsnivå + trendlesere tomme |
| **Dashboard/UI** | **50 %** | 6 / 0 / 0 / 6 — alle vinduer aktive; 6 døde UserControls/VMs |
| **Audio** | **53 %** | 8 / 0 / 0 / 7 — alle tre pipelines hele; RealtimeAnalysisEngine-stakken død |
| **Infra** | **60 %** | 3 / 0 / 0 / 2 — Subsystems/Infra-laget dødt |

---

## Seksjon 9 – Activation Roadmap

> Per audit-instruks: ingen redesign, ingen nye systemer — kun innkoblingspunkter.
> Der aktivering ville DUPLISERE et allerede aktivt system, anbefales i stedet sletting (merket 🗑).

### Critical (safety/helse/progresjon)
1. **ComfortZoneController** — `InitializeAsync(userId)` i ExerciseWindow.OnStartClick (:327) +
   `UpdateZoneAsync(øktsnitt)` i OnStopClick (:356). Koordinatoren abonnerer allerede; adaptiv
   komfortsone og dens safety-lock våkner uten andre endringer. *(Eneste manglende ledd i øvelsens helsekjede som ikke duplisererer noe.)*
2. **Sesjonsnivå-analytics** — `RecordSessionStartedAsync`/`RecordSessionCompletedAsync` fra
   ExerciseSessionRecorder.BeginSession/CompleteSession. Gir daglig/ukentlig trend reelle data
   (forutsetning for ProgressionOrchestrator i fase 2).
3. **Subjektiv selvrapport** — rut `_lastSubjectiveReport` (ExerciseWindow:629) inn i recorderens
   CompleteSession-persistering (f.eks. som HealthAnalyticsEvent ved IndicatesHealthConcern) slik at
   gatene ser den. I dag write-only.
4. **VoiceHealthMonitor** — 🗑 eller aktiver bevisst: krever en jitter/shimmer/HNR-kilde som ikke
   finnes i aktiv pipeline. Overlapper Supervisor-stien; å la den sove er trygt, men da bør
   koordinatorens tre døde handlere + DI-registreringen ryddes.

### Important (coaching/personalisering)
5. **VocalHealthFeedbackMapper** — `Map(decision)` + `Submit` etter `_healthSupervisor.Evaluate`
   i ExerciseSessionRecorder.OnExerciseUpdated (:217) → brukeren FÅR helsemeldingene som i dag kun
   påvirker lås/score i det stille.
6. **HydrationAdvisor** — injiser i recorderen, `Evaluate(state)` ved siden av supervisor-kallet,
   rut via HydrationFeedbackMapper. Hele grenen er ferdigbygget og testdekket.
7. **ProgressionOrchestrator + IExerciseProfileStore** — fase 2 som allerede avtalt: kall
   `OnSessionCompletedAsync` fra øktslutt, persister `SuggestedProfile` via ExerciseProfileStore.
   Forutsetter punkt 2.
8. **SmartCoachEngine-konsolidering** — bytt de degraderte `new SmartCoachEngine(db)`-instansene
   (MainViewModel:190, ProgressionDashboard:156/176) til DI-instansen med full feedback-graf.
9. **ComplexityEngine.TryAdvanceLevelAsync** — nivået kan i dag aldri heves; koble til
   SmartCoachViewModel-flyten hvis kompleksitetsprogresjon ønskes.

### Nice To Have (UI/statistikk)
10. **SafetyLockEngaged/Released → UI-varsel** — eventene fyrer reelt men ingen lytter; en abonnent i
    MainViewModel ville gjøre progresjonsblokkering synlig for brukeren.
11. **DatabaseService DI-opprydding** — bytt ~15 `new DatabaseService()` til IDatabaseService-injeksjon.
12. **🗑 Slettekandidater** (duplikater av aktive systemer; aktivering frarådes):
    Subsystems/ + Infra/ + ProgressionEngine/WeeklyPlannerEngine/ProgressionConfig ·
    RealtimeAnalysisEngine/AsyncAudioPipeline/AdaptivePitchDetector/VoiceMetricsCalculator/SpeechRateAnalyzer/MockAudioAnalysisEngine ·
    VoiceHealthModule/ (RestProtocol/StrainMonitor) + VoiceHealthService + TrendAlertService + TrendAnalysisService ·
    GamificationService/PeriodizationService/AdaptiveDifficultyService/ProgressionRateCalculator ·
    LiveFeedbackView(+VM)/ExerciseSummaryView(+VM)/PitchChartViewModel/SmartCoachDashboardView/ViewModels-ProgressionDashboardViewModel/ExerciseListViewModel ·
    ExerciseFeedbackEngine + FeedbackRuleEngine/ · CoachMessageGenerator/Formatter ·
    VoiceFeminizationExerciseService/ResonanceModuleDocumentation · AudioAnalysisEngine_new.cs/part2.cs (tomme stubber)

---

## Vedlegg: Detaljer for hvert dødt/sovende system

#### Subsystems/Analysis/AnalysisSubsystem (IAnalysisSubsystem) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Subsystems/Analysis/AnalysisSubsystem.cs`
- **Hvorfor død/sovende:** Aldri instansiert. Analyse i prod gjøres av PitchDetectionService/FormantDetectionService/ResonanceProxyEngine direkte, ikke gjennom dette subsystemet.
- **Eksakt manglende runtime-kall:** services.AddTransient<IAnalysisSubsystem,AnalysisSubsystem>() + konsument
- **Anbefalt aktiveringspunkt:** App.xaml.cs ConfigureServices — frarådes

#### TrendAnalysisService — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/TrendAnalysisService.cs`
- **Hvorfor død/sovende:** Statiske metoder; null produksjonskall (ikke forveksle med TrendAlertService)
- **Eksakt manglende runtime-kall:** Statiske kall fra en VM/analyse-side
- **Anbefalt aktiveringspunkt:** AnalysisPageViewModel eller StatisticsViewModel hvis trendfunksjonene ønskes

#### AdaptivePitchDetector — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Audio/AdaptivePitchDetector.cs`
- **Hvorfor død/sovende:** Eneste referansen er fra AsyncAudioPipeline som aldri instansieres. Produksjons-pitch går via PitchDetectionService og AudioAnalysisEngine, ikke denne.
- **Eksakt manglende runtime-kall:** Aktivering av AsyncAudioPipeline ELLER direkte new AdaptivePitchDetector fra et vindu
- **Anbefalt aktiveringspunkt:** Ikke anbefalt uten redesign.

#### AsyncAudioPipeline — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Audio/AsyncAudioPipeline.cs`
- **Hvorfor død/sovende:** Ingen produksjonskode gjør `new AsyncAudioPipeline`. Den news selv opp AdaptivePitchDetector (:31) og VoiceActivityDetector (:32), men siden ingen instansierer pipelinen er hele under-grafen død.
- **Eksakt manglende runtime-kall:** new AsyncAudioPipeline(...) + tilkobling til AudioCaptureService
- **Anbefalt aktiveringspunkt:** Ikke anbefalt uten redesign — capture-kjedene er allerede dekket av AudioCaptureService/AudioAnalysisEngine.

#### RealtimeAnalysisEngine — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Audio/RealtimeAnalysisEngine.cs`
- **Hvorfor død/sovende:** Ingen produksjonssti instansierer den. Null `new RealtimeAnalysisEngine` i hele kodebasen (verken Views, ViewModels, Services eller Subsystems). Den ER ikke koblet til noen capture-kjede; den eier sin egen VoiceStrainDetector/PitchDetectionService/FormantDetectionService internt (:97 m.fl.), men ingen kaller konstruktøren.
- **Eksakt manglende runtime-kall:** new RealtimeAnalysisEngine(...) + abonnement på dens analyse-events fra et vindu
- **Anbefalt aktiveringspunkt:** Ville krevd redesign — ingen anbefalt enkel innkobling. MainWindow/ExerciseWindow bruker allerede AudioAnalysisEngine/AudioCaptureService-kjedene for samme formål.

#### SpeechRateAnalyzer — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Audio/SpeechRateAnalyzer.cs`
- **Hvorfor død/sovende:** Null referanser i hele kodebasen (verken produksjon, Subsystems eller tester). Klassen er fullstendig isolert.
- **Eksakt manglende runtime-kall:** new SpeechRateAnalyzer(...) + innmating av VAD/voiced-segmenter
- **Anbefalt aktiveringspunkt:** Ikke anbefalt uten redesign — ingen konsument finnes for SpeechRateMetrics.

#### Subsystems/Audio/AudioSubsystem (IAudioSubsystem) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Subsystems/Audio/AudioSubsystem.cs`
- **Hvorfor død/sovende:** Produksjons-lydflyten går via AudioCaptureService direkte (ExerciseWindow/AnalyzerWindow), ikke via AudioSubsystem. AudioSubsystem instansieres aldri.
- **Eksakt manglende runtime-kall:** services.AddSingleton<IAudioSubsystem,AudioSubsystem>() + bruk i en aktiv VM
- **Anbefalt aktiveringspunkt:** App.xaml.cs ConfigureServices — frarådes (dublerer AudioCaptureService)

#### VoiceActivityDetector — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Audio/VoiceActivityDetector.cs`
- **Hvorfor død/sovende:** Begge eierne er døde (AsyncAudioPipeline aldri instansiert; AnalysisSubsystem kun reachable via AddFemVoiceStudio() som aldri kalles). Voiced/unvoiced-avgjørelse i produksjon gjøres av PitchDetectionService (IsVoiced) i stedet.
- **Eksakt manglende runtime-kall:** Aktivering av AsyncAudioPipeline eller AnalysisSubsystem
- **Anbefalt aktiveringspunkt:** Ikke anbefalt uten redesign — IsVoiced fra PitchDetectionService brukes allerede i de aktive kjedene.

#### VoiceMetricsCalculator — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Audio/VoiceMetricsCalculator.cs`
- **Hvorfor død/sovende:** Null `new VoiceMetricsCalculator` i produksjon. Filen refererer kun internt til PitchDetection/FormantDetection (:13-tabellen viser bruk i egen fil), men ingen vindu/VM/tjeneste instansierer klassen. Runtime bruker LiveMetricsService (Services/) i MainViewModel i stedet.
- **Eksakt manglende runtime-kall:** new VoiceMetricsCalculator(...) fra et vindu eller ViewModel
- **Anbefalt aktiveringspunkt:** Ikke anbefalt uten redesign — overlapper LiveMetricsService som allerede er aktiv i MainViewModel.

#### AdaptiveTargetZoneService (SmartCoachModule) — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/SmartCoachModule/AdaptiveTargetZoneService.cs`
- **Hvorfor død/sovende:** Aldri instansiert. Den faktisk brukte mål-/komfortsone-justeringen skjer via AdaptiveComfortZoneService (MainViewModel) og ComfortZoneController (koordinator) — denne SmartCoachModule-varianten er en frakoblet parallell.
- **Eksakt manglende runtime-kall:** new AdaptiveTargetZoneService(db).GetAdaptedDefinition(definition, userId) fra ExerciseWindow
- **Anbefalt aktiveringspunkt:** ExerciseWindow.ShowExerciseDetail (Views/ExerciseWindow.xaml.cs:493) før CreateProfile — men overlapper med ExerciseProfileFactory/koordinator; aktivering krever vurdering mot eksisterende path.

#### CoachMessageFormatter — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/CoachMessageFormatter.cs`
- **Hvorfor død/sovende:** Ingen referanser til CoachMessageFormatter noe sted utenfor egen fil. Helt frakoblet.
- **Eksakt manglende runtime-kall:** new CoachMessageFormatter().<Format>(...) fra runtime
- **Anbefalt aktiveringspunkt:** Ville måtte kobles til CoachMessageGenerator-output (som selv er dead) — krever redesign.

#### CoachMessageGenerator — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/CoachMessageGenerator.cs`
- **Hvorfor død/sovende:** Instansieres kun i enhetstest. Ingen produksjons-VM/-tjeneste kaller GenerateMessage. SmartCoach-meldinger genereres i stedet direkte i SmartCoachEngine.GenerateMotivationalMessages (som selv er dormant).
- **Eksakt manglende runtime-kall:** new CoachMessageGenerator().GenerateMessage(direction, level, score) fra runtime
- **Anbefalt aktiveringspunkt:** SmartCoachViewModel eller MainViewModel — men overlapper med SmartCoachEngine sin egen meldingsgenerering; vurder før aktivering.

#### ExerciseFeedbackEngine — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/ExerciseFeedbackEngine.cs`
- **Hvorfor død/sovende:** Aldri instansiert i produksjon. Den nye live-feedback-pathen går via ExerciseIntelligenceCoordinator (+ExerciseSessionRecorder), ikke via ExerciseFeedbackEngine. Klinisk øktscore beregnes av ClinicalSessionScore, ikke av denne engine.
- **Eksakt manglende runtime-kall:** new ExerciseFeedbackEngine(...).Start(definition, level) + AddMetrics(...) fra en aktiv øvelses-VM
- **Anbefalt aktiveringspunkt:** Vil kreve full re-kobling i ExerciseWindow — overlapper med eksisterende koordinator-path; aktivering frarådes uten redesign.

#### FeedbackRuleEngine (CompositeEvaluator + IRuleEvaluator: Pitch/Resonance/Intonation/Breathing) — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/FeedbackRuleEngine/CompositeEvaluator.cs`
- **Hvorfor død/sovende:** Hele FeedbackRuleEngine-mappen er aldri instansiert eller referert fra produksjon eller tester. Evalueringslogikken duplikeres i praksis av ExerciseIntelligenceCoordinator.EvaluateExerciseStateCore, som er den faktisk brukte pathen.
- **Eksakt manglende runtime-kall:** new CompositeEvaluator().Evaluate(...) fra en runtime-konsument
- **Anbefalt aktiveringspunkt:** Ingen ikke-invasiv plass — koordinatoren har allerede sin egen regel-evaluering. Aktivering = redesign.

#### SmartCoachExerciseAdapter — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/SmartCoachExerciseAdapter.cs`
- **Hvorfor død/sovende:** De eneste callerne (LiveFeedbackViewModel, ExerciseSummaryViewModel) instansieres aldri i produksjon — deres views (LiveFeedbackView/ExerciseSummaryView) er ikke embeddet i noe vindu-XAML og new'es aldri. Adapteren nås derfor aldri.
- **Eksakt manglende runtime-kall:** Hele kjeden LiveFeedbackView/ExerciseSummaryView mangler oppretting
- **Anbefalt aktiveringspunkt:** Krever at en aktiv øvelses-VM (f.eks. ExerciseDetailViewModel i ExerciseWindow) kaller GenerateCoachHint/PrioritizeExercises direkte — ellers redesign.

#### Subsystems/SmartCoach/SmartCoachSubsystem (ISmartCoachSubsystem) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Subsystems/SmartCoach/SmartCoachSubsystem.cs`
- **Hvorfor død/sovende:** Aldri instansiert. Coaching i prod går via SmartCoachEngine (DI-bygd), ikke SmartCoachSubsystem.
- **Eksakt manglende runtime-kall:** services.AddSingleton<ISmartCoachSubsystem,SmartCoachSubsystem>() + konsument
- **Anbefalt aktiveringspunkt:** App.xaml.cs ConfigureServices — frarådes (dublerer SmartCoachEngine)

#### VoiceFeminizationExerciseService + ResonanceModuleDocumentation — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/VoiceFeminizationExerciseService.cs (533 l)`
- **Hvorfor død/sovende:** Aldri instansiert; øvelsesinnhold dekkes av ExerciseTextService/ExerciseDataService
- **Eksakt manglende runtime-kall:** new + GetExercises-kall
- **Anbefalt aktiveringspunkt:** Ikke anbefalt aktivert — duplikat av aktivt øvelsessystem

#### IComfortZoneRepository — ❌ DEAD
- **Fil:** `FemVoiceStudio/Data/IComfortZoneRepository.cs`
- **Hvorfor død/sovende:** Interfacet har ingen implementerende klasse og ingen konsument. ComfortZoneController bruker IUserRepository/IScoreRepository, ikke IComfortZoneRepository.
- **Eksakt manglende runtime-kall:** En klasse 'X : IComfortZoneRepository' + DI-registrering + injeksjon
- **Anbefalt aktiveringspunkt:** Ingen — ubrukt abstraksjon

#### Services/VoiceProfileExtensions.cs (VoiceProfileExtensions, VoiceProfile, ExerciseEffectiveness, DailyProgress) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/VoiceProfileExtensions.cs`
- **Hvorfor død/sovende:** Klassen har kun testdekning, ingen produksjonskonsument. (ExerciseEffectiveness-navnet kolliderer med en SQLite-tabell i DatabaseService, men selve C#-klassen i denne filen brukes ikke i prod.)
- **Eksakt manglende runtime-kall:** En aktiv tjeneste/VM som kaller VoiceProfileExtensions-metoder
- **Anbefalt aktiveringspunkt:** Ingen — kun test-relikt

#### Subsystems/Data/DataSubsystem (IDataSubsystem) inkl. backup/eksport (CreateBackupAsync/ExportDataAsync/ImportDataAsync/RestoreBackupAsync) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Subsystems/Data/DataSubsystem.cs`
- **Hvorfor død/sovende:** Aldri instansiert i prod. Eneste konsument (SubsystemViewModelBase) er selv ubrukt. Backup/eksport-API-et (CreateBackupAsync/ExportDataAsync m.fl.) har null prod-kallere.
- **Eksakt manglende runtime-kall:** services.AddSingleton<IDataSubsystem,DataSubsystem>() + en aktiv ViewModel som arver SubsystemViewModelBase
- **Anbefalt aktiveringspunkt:** App.xaml.cs ConfigureServices + ny VM-hierarki — krever redesign, frarådes

#### RestProtocolService (VoiceHealthModule) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/VoiceHealthModule/RestProtocolService.cs`
- **Hvorfor død/sovende:** Hele VoiceHealthModule-mappen er frakoblet; klassen instansieres aldri i noen runtime-vei.
- **Eksakt manglende runtime-kall:** new RestProtocolService().StartSession() + periodiske oppdateringer fra lydloopen, samt abonnement pa hvile/lockout-eventene.
- **Anbefalt aktiveringspunkt:** ExerciseWindow.OnStartClick (Views/ExerciseWindow.xaml.cs:327) / OnStopClick (Views/ExerciseWindow.xaml.cs:356) for okt-livssyklus.

#### StrainMonitor (VoiceHealthModule) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/VoiceHealthModule/StrainMonitor.cs`
- **Hvorfor død/sovende:** Frakoblet runtime; instansieres aldri og ingen abonnenter pa output.
- **Eksakt manglende runtime-kall:** new StrainMonitor(...) + mating av lyd/metrikker per frame og konsum av deteksjonsresultat.
- **Anbefalt aktiveringspunkt:** ExerciseWindow.OnExerciseAudioDataAvailable (Views/ExerciseWindow.xaml.cs:742) hvis strain-monitorering pa frame-niva onskes (overlapper konseptuelt med VocalHealthSupervisor).

#### TrendAlertService — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/TrendAlertService.cs`
- **Hvorfor død/sovende:** Aldri instansiert eller registrert. Helt frakoblet runtime.
- **Eksakt manglende runtime-kall:** new TrendAlertService(database).RunSafetyCheck(pitch, rms, userId) ved oktslutt, og handtering av SafetyCheckResult.
- **Anbefalt aktiveringspunkt:** MainViewModel.StopRecording (ViewModels/MainViewModel.cs:358) — der okt lagres og progresjon evalueres — naturlig sted for trend-/sikkerhetssjekk.

#### VoiceHealthService (sesjons-/pause-timer) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/VoiceHealthService.cs`
- **Hvorfor død/sovende:** Klassen instansieres aldri i noen produksjons-kallkjede og er ikke registrert i DI-containeren.
- **Eksakt manglende runtime-kall:** new VoiceHealthService(...).StartSession() + periodisk RegisterSpeaking()/RegisterSilence() fra lydloopen, og abonnement pa BreakRequired/SessionEnded.
- **Anbefalt aktiveringspunkt:** MainWindow/MainViewModel (ViewModels/MainViewModel.cs:311 StartRecording / :358 StopRecording) eller ExerciseWindow.OnStartClick — der opptaksøkter starter/stopper.

#### VoiceStrainDetector — ❌ DEAD
- **Fil:** `FemVoiceStudio/Audio/VoiceStrainDetector.cs`
- **Hvorfor død/sovende:** Kalles kun fra to verter som selv er utenfor produksjons-runtime (AnalysisSubsystem via ubrukt AddFemVoiceStudio; RealtimeAnalysisEngine aldri new-et). Derfor nas detektoren aldri i normal bruk.
- **Eksakt manglende runtime-kall:** Enten instansiering av RealtimeAnalysisEngine i et aktivt vindu, eller direkte new VoiceStrainDetector().Analyze(samples, pitch) i den aktive lydloopen.
- **Anbefalt aktiveringspunkt:** ExerciseWindow.OnExerciseAudioDataAvailable (Views/ExerciseWindow.xaml.cs:742) eller MainViewModel pitch-loopen (ViewModels/MainViewModel.cs:504/559) — der raa samples finnes.

#### Infra/DependencyInjection.cs (AddFemVoiceStudio + ServiceCollection/ServiceProviderExtensions + AnalysisSubsystemFactory) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Infra/DependencyInjection.cs`
- **Hvorfor død/sovende:** Hele dette parallelle DI-oppsettet er foreldet. Produksjon bruker App.ConfigureServices. AddFemVoiceStudio kalles aldri, så ingen av subsystem-registreringene materialiseres.
- **Eksakt manglende runtime-kall:** serviceCollection.AddFemVoiceStudio() i App.ConfigureServices
- **Anbefalt aktiveringspunkt:** App.xaml.cs:88 ConfigureServices — men dette er bevisst ikke i bruk; subsystemene er erstattet av flat tjenesteregistrering

#### ServiceCollectionExtensions.AddFemVoiceStudio + alle Subsystem-registreringer (LocalizationService, ThemeManager, ProgressionEngine, WeeklyPlannerEngine, IDataSubsystem/DataSubsystem, IProgressionSubsystem/ProgressionSubsystem, IAudioSubsystem/AudioSubsystem, IAnalysisSubsystem/AnalysisSubsystem, ISmartCoachSubsystem/SmartCoachSubsystem, ExerciseIntelligenceCoordinator) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Infra/DependencyInjection.cs:21-50`
- **Hvorfor død/sovende:** Denne DI-modulen kalles aldri. Alle typer som er registrert KUN her er dermed utenfor produksjons-DI: ProgressionEngine, WeeklyPlannerEngine, IDataSubsystem/DataSubsystem, IProgressionSubsystem/ProgressionSubsystem, IAudioSubsystem/AudioSubsystem, IAnalysisSubsystem/AnalysisSubsystem, ISmartCoachSubsystem/SmartCoachSubsystem. (LocalizationService/ThemeManager/ExerciseIntelligenceCoordinator finnes ogsaa via andre veier: Instance-singletons / ConfigureServices.) ServiceProviderExtensions (GetDataSubsystem osv.) og AnalysisSubsystemFactory har heller ingen kallere.
- **Eksakt manglende runtime-kall:** App.OnStartup maa kalle serviceCollection.AddFemVoiceStudio() (eller subsystemene maa registreres i ConfigureServices) for at noe av dette skal naas.
- **Anbefalt aktiveringspunkt:** FemVoiceStudio/App.xaml.cs:30 — legg til services.AddFemVoiceStudio() i ConfigureServices dersom subsystem-laget skal aktiveres (krever ogsaa konsumenter som faktisk resolver subsystemene).

#### AdaptiveDifficultyService — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/AdaptiveDifficultyService.cs`
- **Hvorfor død/sovende:** Klassen er kun definert. Ingen new, ingen DI-registrering, ingen referanse noe sted i produksjon eller tester. Fullstendig frakoblet.
- **Eksakt manglende runtime-kall:** new AdaptiveDifficultyService(...) + en konsument av dens output.
- **Anbefalt aktiveringspunkt:** Ingen naturlig innkoblingspunkt uten redesign; logikken overlapper med ProgressionService/ProgressionOrchestrator.

#### GamificationService — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/GamificationService.cs`
- **Hvorfor død/sovende:** Kun klassedefinisjon (ctor med connectionString). Ingen new/DI/referanse noe sted i produksjon. Aldri instansiert.
- **Eksakt manglende runtime-kall:** new GamificationService(connectionString) + konsum av poeng/badges i UI.
- **Anbefalt aktiveringspunkt:** Ingen gamification-UI finnes; ville krevd ny visning.

#### Models/PeriodizationModels.cs (PeriodizationConfig/State/Result, TrainingPhase) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Models/PeriodizationModels.cs`
- **Hvorfor død/sovende:** PeriodizationService instansieres aldri i prod, så periodiserings-modellene materialiseres aldri.
- **Eksakt manglende runtime-kall:** new PeriodizationService(...) + kall til EvaluatePhase/GetState fra en aktiv VM
- **Anbefalt aktiveringspunkt:** Ingen aktiv periodiserings-UI finnes; krever ny funksjon

#### Models/TrainingLoad.cs (TrainingLoad, IntensityLevel, TrainingFocus, TrainingDayType) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Models/TrainingLoad.cs`
- **Hvorfor død/sovende:** Eneste konsument-kjede (WeeklySchedule->WeeklyPlannerEngine) er død (WeeklyPlannerEngine registreres kun i den aldri-kalte AddFemVoiceStudio).
- **Eksakt manglende runtime-kall:** Aktiv WeeklyPlannerEngine/treningslast-beregning
- **Anbefalt aktiveringspunkt:** Ingen — avhenger av død WeeklyPlannerEngine

#### Models/UserProgressionProfile.cs — ❌ DEAD
- **Fil:** `FemVoiceStudio/Models/UserProgressionProfile.cs`
- **Hvorfor død/sovende:** Kun brukt av Services/Progression/*-motorene som aldri instansieres i prod.
- **Eksakt manglende runtime-kall:** Aktiv ProgressionEngine.Evaluate(...) i prod
- **Anbefalt aktiveringspunkt:** Ingen — ProgressionEngine er død

#### Models/WeeklySchedule.cs (WeeklySchedule, ScheduledSession, TargetAdjustment, ProgressionDecisionResult) — ❌ DEAD
- **Fil:** `FemVoiceStudio/Models/WeeklySchedule.cs`
- **Hvorfor død/sovende:** WeeklyPlannerEngine og ProgressionEngine er kun registrert via den aldri-kalte AddFemVoiceStudio (DependencyInjection.cs:28-29) og 'new'-es ikke i prod. (Merk: ComplexityEvaluation-property på WeeklySchedule er aktiv via ComplexityEngine andre steder, men selve WeeklySchedule-typen er det ikke.)
- **Eksakt manglende runtime-kall:** Aktiv WeeklyPlannerEngine.GenerateWeeklyPlan fra en aktiv VM
- **Anbefalt aktiveringspunkt:** Ingen — avhenger av død planlegger

#### PeriodizationService — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/PeriodizationService.cs`
- **Hvorfor død/sovende:** Kun klassedefinisjon (to konstruktører). Ingen new/DI/referanse i produksjon. Aldri nådd.
- **Eksakt manglende runtime-kall:** new PeriodizationService(database) + kall til periodiseringsmetodene fra en planleggings-viewmodel.
- **Anbefalt aktiveringspunkt:** Ingen aktiv planleggingsflyt finnes; ville krevd ny UI/orchestrering.

#### ProgressionConfig — ❌ DEAD
- **Fil:** `FemVoiceStudio/Services/ProgressionConfig.cs (330 l)`
- **Hvorfor død/sovende:** Eneste konsumenter er selv døde
- **Eksakt manglende runtime-kall:** —
- **Anbefalt aktiveringspunkt:** Følger evt. aktivering av Progression-motorene (ikke planlagt)

#### ProgressionDashboardViewModel (ViewModels/) — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/ViewModels/ProgressionDashboardViewModel.cs`
- **Hvorfor død/sovende:** Det finnes TO klasser med navnet ProgressionDashboardViewModel. ProgressionWindow → ProgressionDashboard UserControl instansierer den INNI Views/ProgressionDashboard.xaml.cs (ctor med ProgressionDashboard-param, linje 25/169). Denne separate ViewModels/-klassen — som er den eneste som kaller complexityEngine.EvaluateCurrentLevel:213 og GetProgressionSteps:229 — instansieres ALDRI (ingen 'new ProgressionDashboardViewModel(this)' treffer den; den har ikke den ctor-signaturen).
- **Eksakt manglende runtime-kall:** new ProgressionDashboardViewModel(database) + binding i ProgressionDashboard/ProgressionWindow, ELLER fjern duplikaten og flytt complexity-lasting inn i den aktive Views-klassen.
- **Anbefalt aktiveringspunkt:** Views/ProgressionDashboard.xaml.cs:25 — bytt til denne VM-en, eller port LoadComplexityData inn i den aktive inner-VM.

#### ProgressionEngine (Services/Progression/) — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/Progression/ProgressionEngine.cs`
- **Hvorfor død/sovende:** Registrert kun i den ubrukte Infra/DependencyInjection.cs (AddFemVoiceStudio har null kallere — bekreftet via grep; kun nevnt i analysis.txt). Ingen new i produksjon. Singletonen bygges aldri.
- **Eksakt manglende runtime-kall:** services.AddFemVoiceStudio() i App.ConfigureServices, ELLER eksplisitt new ProgressionEngine(db, score) + konsum.
- **Anbefalt aktiveringspunkt:** FemVoiceStudio/App.xaml.cs:ConfigureServices — men dette overlapper ProgressionService/Orchestrator; krever bevisst valg, ikke ren innkobling.

#### ProgressionOrchestrator — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/ProgressionOrchestrator.cs`
- **Hvorfor død/sovende:** Fortsatt ingen konsument — bekreftet. Registrert som lazy singleton men aldri GetService'd, så konstruktøren kjører aldri og EvaluateAsync/OnSessionCompletedAsync/On*Async kalles aldri. Dette er den eneste leseren av SessionAnalyticsStore.GetDailySummaryAsync/GetWeeklyTrendAsync, som dermed også blir effektivt døde.
- **Eksakt manglende runtime-kall:** App.Services.GetService<ProgressionOrchestrator>() + await orchestrator.OnSessionCompletedAsync(context) ved øktslutt, og abonnement på eventene (f.eks. → FeedbackPipeline).
- **Anbefalt aktiveringspunkt:** ViewModels/MainViewModel.cs:StopRecording (etter EvaluateProgressionWithSafety:434) — resolve orchestrator, bygg ProgressionOrchestratorContext og kall OnSessionCompletedAsync; rut eventene til FeedbackPipeline.

#### ProgressionRateCalculator — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/SmartCoachModule/ProgressionRateCalculator.cs`
- **Hvorfor død/sovende:** Kun klassedefinisjon (ctor med DatabaseService). Ingen new/DI/referanse i produksjon eller tester. Aldri instansiert.
- **Eksakt manglende runtime-kall:** new ProgressionRateCalculator(database) + konsum i SmartCoachViewModel/ProgressionDashboard.
- **Anbefalt aktiveringspunkt:** ViewModels/SmartCoachViewModel.cs:LoadDataAsync — kunne beregne progresjonsrate ved siden av complexity-data.

#### ProgressionSubsystem (Subsystems/Progression/) — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Subsystems/Progression/ProgressionSubsystem.cs`
- **Hvorfor død/sovende:** Hele subsystem-laget (IProgressionSubsystem, ViewModelBase, SmartCoachSubsystem) instansieres kun via AddFemVoiceStudio(), som aldri kalles. App bruker konkrete ViewModels (MainViewModel, SmartCoachViewModel) direkte, ikke ViewModelBase-hierarkiet. Ingen produksjons-kode resolver IProgressionSubsystem.
- **Eksakt manglende runtime-kall:** services.AddFemVoiceStudio() + bytte ViewModels til subsystem-baserte ViewModelBase-arvinger.
- **Anbefalt aktiveringspunkt:** Krever full migrering til subsystem-arkitekturen i App.xaml.cs; ikke en enkel innkobling.

#### Subsystems/Progression/ProgressionSubsystem (IProgressionSubsystem) inkl. CalculateScoreAsync — ❌ DEAD
- **Fil:** `FemVoiceStudio/Subsystems/Progression/ProgressionSubsystem.cs`
- **Hvorfor død/sovende:** Aldri instansiert. Progresjon i prod håndteres av ProgressionService/ProgressionOrchestrator/MasteryEvaluator, ikke dette subsystemet.
- **Eksakt manglende runtime-kall:** services.AddSingleton<IProgressionSubsystem,ProgressionSubsystem>() + konsument
- **Anbefalt aktiveringspunkt:** App.xaml.cs ConfigureServices — frarådes (overlapper aktiv ProgressionService)

#### WeeklyPlannerEngine (Services/Progression/) — ❌ DEAD
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/Progression/WeeklyPlannerEngine.cs`
- **Hvorfor død/sovende:** Samme som ProgressionEngine: kun registrert i den ubrukte AddFemVoiceStudio-extensionen. Ingen produksjons-new, ingen GenerateWeeklySchedule-kaller. SetComplexityEngine/EvaluateCurrentLevel-stien nås aldri.
- **Eksakt manglende runtime-kall:** new WeeklyPlannerEngine(cfg, complexityEngine) + en ukeplan-visning som kaller plan-generering.
- **Anbefalt aktiveringspunkt:** Ingen ukeplan-UI; krever ny visning/orchestrering.

#### ExerciseListViewModel — ❌ DEAD
- **Fil:** `FemVoiceStudio/ViewModels/ExerciseListViewModel.cs`
- **Hvorfor død/sovende:** Aldri instansiert; ExerciseWindow har egen listehåndtering i code-behind
- **Eksakt manglende runtime-kall:** new ExerciseListViewModel + binding
- **Anbefalt aktiveringspunkt:** Ikke anbefalt — duplikat. NB: RelayCommand-klassen i samme fil ER aktiv

#### ExerciseSummaryView + ExerciseSummaryViewModel — ❌ DEAD
- **Fil:** `FemVoiceStudio/Views/ExerciseSummaryViewModel.cs`
- **Hvorfor død/sovende:** Øktoppsummering i prod vises av ExerciseWindow-code-behind (CompleteSessionAndCalculateScore + FeedbackText), ikke av ExerciseSummaryView. Ingen hoster kontrollen. Konstruktøren instansierer også en egen SmartCoachExerciseAdapter (linje 84) som er død via denne stien.
- **Eksakt manglende runtime-kall:** <views:ExerciseSummaryView/> i et aktivt vindu med DataContext = new ExerciseSummaryViewModel(db, exerciseDataService)
- **Anbefalt aktiveringspunkt:** ExerciseWindow.xaml ved øktslutt — krever redesign

#### LiveFeedbackView + LiveFeedbackViewModel — ❌ DEAD
- **Fil:** `FemVoiceStudio/Views/LiveFeedbackViewModel.cs`
- **Hvorfor død/sovende:** ExerciseWindow bruker IKKE LiveFeedbackView/LiveFeedbackViewModel; den binder ExerciseDetailViewModel direkte mot navngitte UI-elementer (ExerciseWindow.xaml.cs:115-127). LiveFeedbackViewModel.ctor instansierer en egen SmartCoachExerciseAdapter (linje 72/93) som dermed også er død via denne stien.
- **Eksakt manglende runtime-kall:** <views:LiveFeedbackView/> i et aktivt vindu, eller new LiveFeedbackViewModel(...) med DataContext-sett
- **Anbefalt aktiveringspunkt:** ExerciseWindow.xaml — men ExerciseDetailViewModel dekker allerede funksjonen; aktivering krever redesign

#### PitchChartViewModel — ❌ DEAD
- **Fil:** `FemVoiceStudio/Views/PitchChartViewModel.cs`
- **Hvorfor død/sovende:** Ingen vindu/XAML binder eller instansierer PitchChartViewModel. (Merk: den separate tjenesten PitchChartAxisRangeCalculator er derimot AKTIV — kalt direkte i MainWindow.xaml.cs:427 — men VM-en selv er død.)
- **Eksakt manglende runtime-kall:** new PitchChartViewModel() bundet som DataContext i et aktivt pitch-chart
- **Anbefalt aktiveringspunkt:** MainWindow/ExerciseWindow pitch-visualisering — krever redesign

#### ViewModelBase / SubsystemViewModelBase — ❌ DEAD
- **Fil:** `FemVoiceStudio/ViewModels/ViewModelBase.cs`
- **Hvorfor død/sovende:** Ingen ViewModel arver fra ViewModelBase/SubsystemViewModelBase. SubsystemViewModelBase-konstruktøren (som abonnerer på AudioSubsystem.AudioSampleAvailable og krever alle 5 subsystem) kjøres aldri.
- **Eksakt manglende runtime-kall:** En produksjons-VM med 'public class X : SubsystemViewModelBase'
- **Anbefalt aktiveringspunkt:** Ingen — hele base-hierarkiet er subsystem-koblet og dødt

#### ViewModels/ProgressionDashboardViewModel (FemVoiceStudio.ViewModels) — ❌ DEAD
- **Fil:** `FemVoiceStudio/ViewModels/ProgressionDashboardViewModel.cs`
- **Hvorfor død/sovende:** Navnekollisjon: to klasser heter ProgressionDashboardViewModel. ProgressionDashboard.xaml.cs bygger den nestede Views-varianten i kode-behind. ViewModels-varianten er en foreldet duplikat.
- **Eksakt manglende runtime-kall:** DataContext = new FemVoiceStudio.ViewModels.ProgressionDashboardViewModel(...) i et aktivt vindu
- **Anbefalt aktiveringspunkt:** Ingen — den nestede Views-varianten dekker allerede dashboardet

#### HydrationFeedbackMapper — 🟠 DORMANT
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/FeedbackPipeline.cs`
- **Hvorfor død/sovende:** HydrationAdvisor er DI-registrert (App.xaml.cs:171) men dens Advise/Evaluate kalles aldri i runtime (advisoren mottar ingen data), og HydrationAdvice rutes aldri til denne mapperen eller FeedbackPipeline. Hele hydrerings-coachgrenen er kaldt.
- **Eksakt manglende runtime-kall:** var advice = hydrationAdvisor.<evaluate>(...); var c = hydrationFeedbackMapper.Map(advice); feedbackPipeline.Submit(c, hydrationFeedbackMapper.BuildContext(advice, liveState));
- **Anbefalt aktiveringspunkt:** ExerciseSessionRecorder (Services/ExerciseSessionRecorder.cs) eller ExerciseDetailViewModel — kall HydrationAdvisor periodisk/ved øktslutt og rut output gjennom mapper+pipeline.

#### ProgressionFeedbackMapper — 🟠 DORMANT
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/FeedbackPipeline.cs`
- **Hvorfor død/sovende:** Mapperen er DI-registrert men ingen runtime-kode kaller .Map. ProgressionOrchestrator (DI-singleton) produserer ProgressionOrchestratorDecision, men ingen kobler dens output til denne mapperen eller til FeedbackPipeline. MainViewModel.StopRecording bruker ProgressionService.EvaluateProgressionWithSafety direkte (ikke ProgressionOrchestrator+mapper).
- **Eksakt manglende runtime-kall:** var c = progressionFeedbackMapper.Map(decision); feedbackPipeline.Submit(c, progressionFeedbackMapper.BuildContext(decision));
- **Anbefalt aktiveringspunkt:** MainViewModel.StopRecording (ViewModels/MainViewModel.cs ~rad 434) etter EvaluateProgressionWithSafety, eller der ProgressionOrchestrator faktisk produserer en decision.

#### SmartCoachFeedbackMapper — 🟠 DORMANT
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/FeedbackPipeline.cs`
- **Hvorfor død/sovende:** Mapperen er korrekt injisert i den aktive DI-SmartCoachEngine, men de eneste to metodene som ruter gjennom pipelinen (AnalyzeSessionForStrain, GenerateMotivationalMessages) kalles aldri. SmartCoachViewModel kaller kun GenerateDailyRecommendation/GetOrCalculateBaseline/GenerateGoals/CalculateWeeklyProgress/GetStatusSummary — som skriver direkte til DB uten pipeline. Koordinatoren kaller ingen SmartCoachEngine-metode i det hele tatt.
- **Eksakt manglende runtime-kall:** _engine.GenerateMotivationalMessages(1) og/eller _engine.AnalyzeSessionForStrain(session) fra en runtime-konsument
- **Anbefalt aktiveringspunkt:** SmartCoachViewModel.LoadDataAsync (ViewModels/SmartCoachViewModel.cs ~rad 190-214) — kall _engine.GenerateMotivationalMessages(1) i Batch 2 før GetUnreadMessages, eller AnalyzeSessionForStrain ved øktslutt i MainViewModel.StopRecording.

#### TrainingFrequencyService — 🟠 DORMANT
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/TrainingFrequencyService.cs`
- **Hvorfor død/sovende:** Instansieres i ExerciseWindow men ingen av metodene kalles noen gang (_trainingService. forekommer ikke utenom feltdeklarasjon+konstruksjon). Output forbrukes ikke i UI.
- **Eksakt manglende runtime-kall:** _trainingService.<GetRecommended.../GetWeeklyTarget...>() fra ExerciseWindow-UI
- **Anbefalt aktiveringspunkt:** ExerciseWindow.UpdateTodaysStatus (Views/ExerciseWindow.xaml.cs:571) — vis treningsfrekvens/ukesmål ved siden av minutter/økter.

#### VocalHealthFeedbackMapper — 🟠 DORMANT
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/FeedbackPipeline.cs`
- **Hvorfor død/sovende:** VocalHealthSupervisor.Evaluate kalles aktivt av ExerciseSessionRecorder.OnExerciseUpdated (ExerciseSessionRecorder.cs:217), men recorderen bruker kun decision.State/StrainDetected/FatigueDetected til å sette tellere og CurrentHealthScore. VocalHealthDecision rutes ALDRI til VocalHealthFeedbackMapper eller FeedbackPipeline — så ingen helse-coachmelding genereres fra supervisor-output.
- **Eksakt manglende runtime-kall:** var c = vocalHealthFeedbackMapper.Map(decision); if (c != null) feedbackPipeline.Submit(c, vocalHealthFeedbackMapper.BuildContext(decision));
- **Anbefalt aktiveringspunkt:** ExerciseSessionRecorder.OnExerciseUpdated (Services/ExerciseSessionRecorder.cs:217), rett etter _healthSupervisor.Evaluate(state) — injiser FeedbackPipeline + VocalHealthFeedbackMapper i recorderen og rut decision dit.

#### ExerciseProfileStore (write-path) — 🟠 DORMANT
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/ExerciseProfileStore.cs`
- **Hvorfor død/sovende:** Skrives den noen gang? NEI — bekreftet. SaveAsync har ingen produksjonskaller; den eneste tiltenkte skriveren (ProgressionOrchestrator) er selv død og holder ikke engang en referanse til storen. (Samme fysiske objekt som IExerciseProfileStore-oppføringen over; oppført separat for å eksplisitt svare på 'skrives den noen gang?'.)
- **Eksakt manglende runtime-kall:** SaveAsync(ExerciseProfileOverride) fra en aktiv ProgressionOrchestrator-event-handler.
- **Anbefalt aktiveringspunkt:** Etter aktivering av ProgressionOrchestrator: abonner ExerciseProfileUpdated → store.SaveAsync.

#### IExerciseProfileStore -> SqliteExerciseProfileStore — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/App.xaml.cs:127-135`
- **Hvorfor død/sovende:** Registrert singleton, men aldri resolvet eller injisert i produksjon. Lazy-singleton => aldri instansiert. Aapner aldri DB-tilkoblingen.
- **Eksakt manglende runtime-kall:** Mangler en konsument: f.eks. en ProfileStore-injeksjon i ExerciseDetailViewModel eller ProgressionOrchestrator. Ingen GetService<IExerciseProfileStore>() finnes.
- **Anbefalt aktiveringspunkt:** Services/ProgressionOrchestrator.cs eller ViewModels/ExerciseDetailViewModel.cs:146 (ctor) — injiser IExerciseProfileStore der profiler skal persisteres/leses.

#### InMemoryExerciseRepository (IUserRepository + IScoreRepository) — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/InMemoryExerciseRepositories.cs`
- **Hvorfor død/sovende:** Instansiert og injisert, men begge konsumentene (ComfortZoneController + FemVoiceScoreEngine) får aldri sine data-metoder kalt i prod. Repo-en er ren in-memory (ingen persistens), så selv ved kall ville den tømmes ved omstart.
- **Eksakt manglende runtime-kall:** ComfortZoneController.InitializeAsync(userId)/UpdateZoneAsync(...) eller FemVoiceScoreEngine.CalculateScoreAsync(...) fra øvelsesløkka
- **Anbefalt aktiveringspunkt:** ExerciseDetailViewModel.UpdateLiveMetrics (ExerciseDetailViewModel.cs:762) eller ExerciseWindow.OnExerciseAudioDataAvailable — der disse engine-metodene måtte vært kalt

#### MasteryLevel/ExerciseProfileStore (IExerciseProfileStore) — 🟠 DORMANT
- **Fil:** `/mnt/raid/home/vgrd/Repos/femvoice_studio_priv/FemVoiceStudio/Services/ExerciseProfileStore.cs`
- **Hvorfor død/sovende:** Registrert i DI men ingen produksjonskode resolver IExerciseProfileStore eller kaller SaveAsync/GetAsync. ProgressionOrchestrator (tiltenkt skriver, jf. ExerciseProfileOverride.Source='ProgressionOrchestrator') tar ikke engang en IExerciseProfileStore i konstruktøren — den raiser kun events. Skrives den noen gang? NEI. Lazy-singletonen instansieres aldri, så selv DB-tabellen ExerciseProfileOverrides opprettes aldri i runtime.
- **Eksakt manglende runtime-kall:** profileStore.SaveAsync(new ExerciseProfileOverride{...}) ved ExerciseProfileUpdated-beslutning, og GetAsync i ExerciseProfileFactory/ExerciseDetailViewModel.ApplyProfile for å laste lagrede overrides.
- **Anbefalt aktiveringspunkt:** Abonner på ProgressionOrchestrator.ExerciseProfileUpdated → SaveAsync; og i Services/ExerciseProfileFactory.cs / ExerciseDetailViewModel.ApplyProfile: les GetAsync(userId, exerciseId) før CreateProfile.

#### ComfortZoneController (ZoneUpdated) — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/ComfortZoneController.cs`
- **Hvorfor død/sovende:** Registrert og event-koblet, men ingen produksjonskode kaller InitializeAsync (forste pakrevde steg) eller UpdateZoneAsync/RecordStrainIncidentAsync som er de eneste som RaiseZoneUpdatedEvent. Resultat: ZoneUpdated fyres aldri; komfortsone-pitchgrenser i koordinatoren seedes i stedet kun fra profilen i UpdateMetrics (Services/ExerciseIntelligenceCoordinator.cs:282-285).
- **Eksakt manglende runtime-kall:** await comfortZoneController.InitializeAsync(userId) ved oktstart, deretter comfortZoneController.UpdateZoneAsync(...) per okt/tick. Begge mangler.
- **Anbefalt aktiveringspunkt:** ExerciseWindow.OnStartClick (Views/ExerciseWindow.xaml.cs:327) for init + ExerciseWindow.OnStopClick (Views/ExerciseWindow.xaml.cs:356) for UpdateZoneAsync med oktens snitt-metrikker. (Koordinatoren abonnerer allerede.)

#### ComfortZoneState — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/ComfortZoneState.cs`
- **Hvorfor død/sovende:** Vert-kontrolleren er dormant
- **Eksakt manglende runtime-kall:** Følger ComfortZoneController-aktivering
- **Anbefalt aktiveringspunkt:** Som ComfortZoneController

#### HydrationAdvisor — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/HydrationAdvisor.cs`
- **Hvorfor død/sovende:** Resolves/instansieres (options-fabrikken kjorer), men Evaluate kalles aldri => ingen hydreringsvurdering skjer. Til forskjell fra VocalHealthSupervisor som ER koblet inn i recorderen, er HydrationAdvisor ikke koblet til live-state-strommen.
- **Eksakt manglende runtime-kall:** hydrationAdvisor.Evaluate(state) per tick — typisk ved siden av healthSupervisor.Evaluate.
- **Anbefalt aktiveringspunkt:** ExerciseSessionRecorder.OnExerciseUpdated (Services/ExerciseSessionRecorder.cs:217), rett etter _healthSupervisor.Evaluate(state) — injiser HydrationAdvisor i recorderens konstruktor og kall Evaluate der.

#### HydrationAdvisorOptions (factory-reg) — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/App.xaml.cs:167`
- **Hvorfor død/sovende:** Naas kun via HydrationAdvisor-fabrikken, som aldri resolves.
- **Eksakt manglende runtime-kall:** Henger sammen med dormant HydrationAdvisor.
- **Anbefalt aktiveringspunkt:** Se HydrationAdvisor.

#### Subjektiv rapport i ExerciseWindow (_lastSubjectiveReport) — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Views/ExerciseWindow.xaml.cs`
- **Hvorfor død/sovende:** Rapporten er write-only: feltet settes men konsumeres aldri. ProgressionOrchestrator har EvaluateSubjectiveReport/OnSubjectiveReportSubmittedAsync (ProgressionOrchestrator.cs:274/279), men ExerciseWindow kaller dem aldri. Subjektiv selvrapportering påvirker dermed verken score, helse-gating eller progresjon.
- **Eksakt manglende runtime-kall:** _sessionRecorder?.RecordSubjectiveReport(_lastSubjectiveReport) ELLER progressionOrchestrator.OnSubjectiveReportSubmittedAsync(context med SubjectiveReport)
- **Anbefalt aktiveringspunkt:** ExerciseWindow.OnSubmitSubjectiveReportClick (ExerciseWindow.xaml.cs:653) — videresend rapporten til ExerciseSessionRecorder/ProgressionOrchestrator i stedet for kun å lagre i feltet

#### VocalHealthLegacyBridge — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/VocalHealthLegacyBridge.cs`
- **Hvorfor død/sovende:** DI-registreringen er ubrukt (dobbeltregistrering vs. internt new). Den interne bridgen er kodd inn, men hele grenen henger pa VoiceHealthMonitor-eventene som aldri fyrer.
- **Eksakt manglende runtime-kall:** Avhenger av at VoiceHealthMonitor.Analyze() begynner a fyre HealthWarning/HealthCritical/LockoutTriggered (samme manglende ledd som VoiceHealthMonitor).
- **Anbefalt aktiveringspunkt:** Aktiveres indirekte nar VoiceHealthMonitor.Analyze() kobles inn (Views/ExerciseWindow.xaml.cs:742). Den ubrukte DI-registreringen kan fjernes eller injiseres i koordinatoren i stedet for internt new.

#### VoiceHealthMonitor (HealthWarning/HealthCritical/LockoutTriggered) — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/VoiceHealthMonitor.cs`
- **Hvorfor død/sovende:** Instansiert og event-koblet, men Analyze() kalles aldri => HealthWarning/HealthCritical/LockoutTriggered fyrer aldri => koordinatorens helse-event-handlere og VocalHealthLegacyBridge (den interne) nas aldri. Dette er nayktig det prompten mistenkte: Analyze() er fortsatt aldri kalt.
- **Eksakt manglende runtime-kall:** _healthMonitor.Analyze(voiceMetrics) ma kalles per audio-frame med reelle jitter/shimmer/HNR-data. Ingen produksjonskode produserer VoiceMetrics til denne.
- **Anbefalt aktiveringspunkt:** ExerciseWindow.OnExerciseAudioDataAvailable (Views/ExerciseWindow.xaml.cs:742) — der lyd allerede behandles — kunne bygge VoiceMetrics og kalle den DI-resolverte VoiceHealthMonitor.Analyze(); evt. via en jitter/shimmer-kilde. (Per instruks: ingen redesign, kun innkoblingspunkt.)

#### FemVoiceScoreEngine — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/FemVoiceScoreEngine.cs`
- **Hvorfor død/sovende:** Registrert og koblet, men CalculateScoreAsync har null prod-kallere. ScoreUpdated fyrer aldri → koordinatorens _cachedStabilityScore fra denne motoren oppdateres aldri (stabilitet i prod kommer i stedet fra pitch.Confidence via UpdateMetrics).
- **Eksakt manglende runtime-kall:** await _scoreEngine.SetUser(...)/CalculateScoreAsync(resonance,pitch,stability,health) per evalueringstick
- **Anbefalt aktiveringspunkt:** ExerciseIntelligenceCoordinator.UpdateMetrics (ExerciseIntelligenceCoordinator.cs:272) eller EvaluateExerciseStateFromCache — der motoren burde drives

#### Services/ZoneConfiguration.cs (ZoneConfiguration, ZoneChangeRequest, ZoneChangeAction) — 🟠 DORMANT
- **Fil:** `FemVoiceStudio/Services/ZoneConfiguration.cs`
- **Hvorfor død/sovende:** ZoneConfiguration konstrueres som default-felt når ComfortZoneController instansieres, men brukes aldri i en aktiv beregning fordi kontrolleren er dormant. ZoneChangeRequest/ZoneChangeAction berøres aldri.
- **Eksakt manglende runtime-kall:** ComfortZoneController.InitializeAsync/UpdateZoneAsync (samme aktivering som ComfortZoneController)
- **Anbefalt aktiveringspunkt:** ExerciseIntelligenceCoordinator.UpdateMetrics — via aktivering av ComfortZoneController

