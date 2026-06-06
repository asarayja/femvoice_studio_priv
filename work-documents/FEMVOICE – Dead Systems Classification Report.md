# FEMVOICE – Dead Systems Classification Report

**Dato:** 2026-06-06 · **Mot:** HEAD `fcc4f70` (ETTER aktiveringsrunden `6b7f152..fcc4f70`)
**Metode:** 5 lag-klassifiserere + DELETE-skeptiker + ACTIVATE/FIX-skeptiker (adversarial verifisering;
alle 47 DELETE-kandidater forsøkt motbevist — 0 omklassifisert; 2 FIX nedjustert til KEEP av skeptiker).
**Viktig premiss:** Eksempelklassifiseringene i bestillingen var utdaterte — VocalHealthSupervisor,
SessionAnalyticsStore og ProgressionOrchestrator er alle **allerede aktivert** i forrige runde, og
VoiceHealthMonitor er allerede slettet. Denne rapporten klassifiserer mot dagens faktiske runtime.

## Hovedkonklusjon

| Kategori | Antall |
|---|---|
| KEEP (aktiv, ingen handling) | 35 |
| **ACTIVATE** | **0 — alt aktiverbart ble aktivert i forrige runde** |
| FIX | 8 |
| DELETE | 47 |

> Aktiveringspotensialet er uttømt. Det som gjenstår er **8 reparasjoner** (én klinisk viktig:
> IsRead-skjemabomben som blokkerer SmartCoach-strain-analyse) og **47 slettekandidater** med
> verifisert trygge slettestier. Ingen av slettekandidatene har funksjonalitet som ikke dekkes
> av en aktiv komponent.

---

## Seksjon 1 – System Inventory

| System | Finnes | Status @ HEAD | Klassifisering |
|---|---|---|---|
| AnalysisSubsystem (implementasjonsklassen, IKKE typene i IAn | ✓ | dead | **DELETE** |
| Infra/DependencyInjection.cs (AddFemVoiceStudio + extensions | ✓ | dead | **DELETE** |
| RealtimeAnalysisEngine (inkl. RollingBuffer<T> og SignalSmoo | ✓ | dead | **DELETE** |
| AdaptivePitchDetector (inkl. RollingStatistics) | ✓ | dead | **DELETE** |
| VoiceStrainDetector (inkl. StrainAnalysis/StrainLevel) | ✓ | dead | **DELETE** |
| ProgressionDashboardViewModel (ViewModels-namespace) | ✓ | dead | **DELETE** |
| In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert inn  | ✓ | dead | **DELETE** |
| PeriodizationService | ✓ | dead | **DELETE** |
| GamificationService | ✓ | dead | **DELETE** |
| ProgressionEngine | ✓ | dead | **DELETE** |
| ProgressionConfig | ✓ | dead | **DELETE** |
| ExerciseFeedbackEngine | ✓ | dead | **DELETE** |
| VoiceFeminizationExerciseService + ResonanceModuleDocumentat | ✓ | dead | **DELETE** |
| VoiceMetricsCalculator | ✓ | dead | **DELETE** |
| AsyncAudioPipeline | ✓ | dead | **DELETE** |
| VoiceActivityDetector (inkl. avhengighet av RollingStatistic | ✓ | dead | **DELETE** |
| ViewModelBase / SubsystemViewModelBase | ✓ | dead | **DELETE** |
| DataSubsystem (backup/eksport/RestoreBackup-bug) | ✓ | dead | **DELETE** |
| .old/.old2-filer i hele treet | ✓ | dead | **DELETE** |
| VoiceHealthService (sesjons-/pause-timer) | ✓ | dead | **DELETE** |
| TrendAlertService | ✓ | dead | **DELETE** |
| StrainMonitor (VoiceHealthModule) | ✓ | dead | **DELETE** |
| VoiceStrainDetector | ✓ | dead | **DELETE** |
| AdaptiveDifficultyService | ✓ | dead | **DELETE** |
| ProgressionRateCalculator | ✓ | dead | **DELETE** |
| WeeklyPlannerEngine | ✓ | dead | **DELETE** |
| Models: PeriodizationModels / TrainingLoad / WeeklySchedule  | ✓ | dead | **DELETE** |
| SmartCoachExerciseAdapter | ✓ | dead | **DELETE** |
| FeedbackRuleEngine (CompositeEvaluator + 4 IRuleEvaluator) | ✓ | dead | **DELETE** |
| CoachMessageGenerator / CoachMessageFormatter | ✓ | dead | **DELETE** |
| SpeechRateAnalyzer | ✓ | dead | **DELETE** |
| LiveFeedbackView + LiveFeedbackViewModel | ✓ | dead | **DELETE** |
| ExerciseSummaryView + ExerciseSummaryViewModel | ✓ | dead | **DELETE** |
| PitchChartViewModel | ✓ | dead | **DELETE** |
| SmartCoachDashboardView | ✓ | dead | **DELETE** |
| VoiceProfileExtensions (VoiceProfile/ExerciseEffectiveness/D | ✓ | dead | **DELETE** |
| Migration-SQL 001_exercise_feedback_system.sql | ✓ | dead | **DELETE** |
| RestProtocolService (VoiceHealthModule) | ✓ | dead | **DELETE** |
| TrendAnalysisService | ✓ | dead | **DELETE** |
| AdaptiveTargetZoneService | ✓ | dead | **DELETE** |
| MockAudioAnalysisEngine (inni AudioAnalysisEngine.cs) | ✓ | dead | **DELETE** |
| ExerciseListViewModel | ✓ | dead | **DELETE** |
| IComfortZoneRepository | ✓ | dead | **DELETE** |
| SmartCoachBaselines (plural) orphan-tabell | ✓ | dead | **DELETE** |
| AudioAnalysisEngine_new.cs (tom stub) | ✓ | dead | **DELETE** |
| part2.cs (tomt namespace) | ✓ | dead | **DELETE** |
| generate_comfort.py (stub) | ✓ | dead | **DELETE** |
| Subsystems/ (Audio/Analysis/Data/Progression/SmartCoach + I* | ✓ | partial | **FIX** |
| SmartCoachHealthMonitoring IsRead-kolonne (skjema/lese-misma | ✓ | partial | **FIX** |
| LevelClassificationSystem | ✓ | partial | **FIX** |
| InMemoryExerciseRepository (IUserRepository + IScoreReposito | ✓ | active | **FIX** |
| SmartCoachFeedbackMapper | ✓ | partial | **FIX** |
| SmartCoachEngine | ✓ | partial | **FIX** |
| TrainingFrequencyService | ✓ | dormant | **FIX** |
| FeedbackService | ✓ | partial | **FIX** |
| ComplexityEngine | ✓ | active | **KEEP** |
| ComfortZoneController | ✓ | active | **KEEP** |
| SessionAnalyticsStore | ✓ | active | **KEEP** |
| ExerciseSessionRecorder | ✓ | active | **KEEP** |
| ProgressionService | ✓ | active | **KEEP** |
| ProgressionOrchestrator | ✓ | active | **KEEP** |
| IExerciseProfileStore -> SqliteExerciseProfileStore | ✓ | active | **KEEP** |
| IExerciseProfileFactory -> ExerciseProfileFactory | ✓ | active | **KEEP** |
| AudioAnalysisEngine | ✓ | active | **KEEP** |
| VocalHealthSupervisor (+VocalHealthTrendEngine) | ✓ | active | **KEEP** |
| HydrationAdvisor (+HydrationAdvisorOptions) | ✓ | active | **KEEP** |
| VocalHealthBaselineProvider | ✓ | active | **KEEP** |
| LiveMetricsService | ✓ | active | **KEEP** |
| Subjektiv rapport-kjeden | ✓ | active | **KEEP** |
| ComfortZoneState / ZoneConfiguration | ✓ | active | **KEEP** |
| SpectrogramResonanceMapper | ✓ | active | **KEEP** |
| MasteryEvaluator | ✓ | active | **KEEP** |
| ProgressionSafetyGate | ✓ | active | **KEEP** |
| DirectionAnalyzer | ✓ | active | **KEEP** |
| FeedbackPipeline | ✓ | active | **KEEP** |
| FeedbackConsistencyGuard | ✓ | active | **KEEP** |
| InlineCoachFeedbackMapper | ✓ | active | **KEEP** |
| ProgressionFeedbackMapper | ✓ | active | **KEEP** |
| VocalHealthFeedbackMapper | ✓ | active | **KEEP** |
| HydrationFeedbackMapper | ✓ | active | **KEEP** |
| ExerciseTextService | ✓ | active | **KEEP** |
| VoiceGoalProfile-systemet | ✓ | active | **KEEP** |
| AudioAnalyzerService | ✓ | active | **KEEP** |
| ResonanceProxyEngine | ✓ | active | **KEEP** |
| MicrophoneCalibrationService | ✓ | active | **KEEP** |
| ResonansScoringService | ✓ | active | **KEEP** |
| ClinicalSessionScore | ✓ | active | **KEEP** |
| AudioCaptureService | ✓ | active | **KEEP** |
| PitchDetectionService | ✓ | active | **KEEP** |
| FormantDetectionService | ✓ | active | **KEEP** |

---

## Seksjon 2 – Klassifiseringskriterier (anvendt)

- **KEEP** — aktiv og fungerende ved HEAD; ingen handling.
- **ACTIVATE** — arkitektonisk gyldig + klinisk nyttig + ferdig implementert + mangler kun wiring.
  **Tom:** ComfortZoneController, HydrationAdvisor, ProgressionOrchestrator, IExerciseProfileStore,
  FemVoiceScoreEngine, sesjons-analytics, subjektiv rapport, mappere m.fl. ble alle aktivert i
  commits `6b7f152..fcc4f70` og er nå KEEP.
- **FIX** — nyttig men har bugs / manglende persistens / delvis integrasjon (8 systemer, seksjon 6).
- **DELETE** — foreldet/erstattet/duplikat/uten klinisk verdi (47 systemer, seksjon 7).

---

## Seksjon 3 – Duplicate Detection

| Duplikat (dødt/delvis) | Original/erstatning (aktiv) | Anbefaling |
|---|---|---|
| VoiceHealthService (sesjons-/pause-timer) | Subjektiv rapport-kjeden (Views/ExerciseWindow.xaml.cs) + Progres | DELETE |
| TrendAlertService | ProgressionSafetyGate + VocalHealthSupervisor | DELETE |
| RestProtocolService (VoiceHealthModule) | Subjektiv rapport-kjeden + VocalHealthSupervisor (pause-/hvileanb | DELETE |
| StrainMonitor (VoiceHealthModule) | VocalHealthSupervisor (+VocalHealthTrendEngine) | DELETE |
| VoiceStrainDetector | VocalHealthSupervisor (aktiv strain-deteksjon) | DELETE |
| TrendAnalysisService | SessionAnalyticsStore (aggregater) / VocalHealthTrendEngine | DELETE |
| LevelClassificationSystem | ProgressionService (Classify-delen duplikerer EvaluateProgression | FIX |
| AdaptiveDifficultyService | ProgressionService + ProgressionOrchestrator | DELETE |
| PeriodizationService | ProgressionSafetyGate + ProgressionOrchestrator | DELETE |
| ProgressionRateCalculator | ProgressionOrchestrator + ComplexityEngine | DELETE |
| GamificationService | DatabaseService (achievements-tabeller + GetAchievements/UnlockAc | DELETE |
| ProgressionEngine | ProgressionService + ProgressionOrchestrator (og ProgressionGateS | DELETE |
| WeeklyPlannerEngine | ProgressionOrchestrator (variasjon/progresjon dekkes adaptivt der | DELETE |
| RealtimeAnalysisEngine (inkl. RollingBuffer<T> og  | VocalHealthSupervisor + SafetyLockEngaged/Released (aktiv strain/ | DELETE |
| VoiceMetricsCalculator | LiveMetricsService (MainViewModel live-metrikk) | DELETE |
| AsyncAudioPipeline | AudioCaptureService (aktiv capture-kjede) | DELETE |
| AdaptivePitchDetector (inkl. RollingStatistics) | PitchDetectionService (aktiv pitch) | DELETE |
| VoiceActivityDetector (inkl. avhengighet av Rollin | PitchDetectionService (IsVoiced-felt erstatter VAD) | DELETE |
| VoiceStrainDetector (inkl. StrainAnalysis/StrainLe | VocalHealthSupervisor + SafetyLock (aktiv strain-overvåkning) | DELETE |
| MockAudioAnalysisEngine (inni AudioAnalysisEngine. | AudioAnalysisEngine (selve den aktive engine) | DELETE |
| AudioAnalysisEngine_new.cs (tom stub) | AudioAnalysisEngine (navnelikhet, ikke faktisk innhold) | DELETE |
| AnalysisSubsystem (implementasjonsklassen, IKKE ty | ResonansScoringService + LiveMetricsService (aktiv analyse-sti) | DELETE |
| ViewModelBase / SubsystemViewModelBase | CommunityToolkit.Mvvm ObservableObject (de aktive VMene arver den | DELETE |
| LiveFeedbackView + LiveFeedbackViewModel | ExerciseWindow inline live-feedback-panel (aktiv) | DELETE |
| ExerciseSummaryView + ExerciseSummaryViewModel | Oktslutt-floyt (FemVoiceScoreEngine + selvrapport-panel + Session | DELETE |
| PitchChartViewModel | PitchChartAxisRangeCalculator (Services/, aktiv) for akse-logikk; | DELETE |
| ProgressionDashboardViewModel (ViewModels-namespac | FemVoiceStudio.Views.ProgressionDashboardViewModel (nested i Prog | DELETE |
| SmartCoachDashboardView | SmartCoachDetailView/SmartCoachDetailWindow (aktiv vert for Smart | DELETE |
| Infra/DependencyInjection.cs (AddFemVoiceStudio +  | App.ConfigureServices (App.xaml.cs:88, den faktisk brukte DI-konf | DELETE |
| Subsystems/ (Audio/Analysis/Data/Progression/Smart | App.ConfigureServices erstatter subsystem-DI; Models/VoiceMetrics | FIX |
| DataSubsystem (backup/eksport/RestoreBackup-bug) | DatabaseService (aktiv, den faktiske datatilgangen) | DELETE |
| InMemoryExerciseRepository (IUserRepository + ISco | DatabaseService/ExerciseDataService (SQLite) - den tiltenkte pers | FIX |
| IComfortZoneRepository | IUserRepository/IScoreRepository (faktisk brukt av ComfortZoneCon | DELETE |
| VoiceProfileExtensions (VoiceProfile/ExerciseEffec | Subsystems.SmartCoach.VoiceProfile (egen variant) og SmartCoachEn | DELETE |
| Migration-SQL 001_exercise_feedback_system.sql | DatabaseService.InitializeDatabase + RunMigrations (den faktiske, | DELETE |
| SmartCoachBaselines (plural) orphan-tabell | SmartCoachBaseline (entall, l.395 - den faktisk brukte tabellen) | DELETE |
| .old/.old2-filer i hele treet | Hver .old er en utdatert kopi av sin .cs/.xaml-motpart med samme  | DELETE |
| In-exe testmappe FemVoiceStudio/Tests/ (xunit komp | FemVoiceStudio.Tests/ (det riktige testprosjektet); FemVoiceScore | DELETE |

**Mønster:** Tre hele parallellgenerasjoner (Subsystems-laget, RealtimeAnalysisEngine-stakken,
VoiceHealthModule) duplisererer aktive systemer. Skeptikeren avkreftet to falske delingsbekymringer:
`RelayCommand` ligger i egen fil (ikke i ExerciseListViewModel som kommentarene påstår), og de
levende typene `VoiceMetrics`/`ResonanceCategory` ligger i `IAnalysisSubsystem.cs` (interface-fila),
ikke i implementasjonen — DELETE-scopingen tar høyde for begge.

---

## Seksjon 4 – Runtime Value Assessment (1–10)

| System | Klassif. | Klinisk verdi | Teknisk verdi | Vedlikeholdskost |
|---|---|---|---|---|
| ProgressionSafetyGate | KEEP | 10 | 9 | 2 |
| AudioCaptureService | KEEP | 9 | 9 | 1 |
| ClinicalSessionScore | KEEP | 9 | 8 | 1 |
| ComfortZoneController | KEEP | 9 | 8 | 3 |
| FeedbackConsistencyGuard | KEEP | 9 | 9 | 2 |
| FormantDetectionService | KEEP | 9 | 9 | 1 |
| MasteryEvaluator | KEEP | 9 | 9 | 2 |
| PitchDetectionService | KEEP | 9 | 9 | 1 |
| ProgressionOrchestrator | KEEP | 9 | 9 | 3 |
| ProgressionService | KEEP | 9 | 8 | 3 |
| ResonanceProxyEngine | KEEP | 9 | 8 | 2 |
| ResonansScoringService | KEEP | 9 | 8 | 2 |
| VocalHealthFeedbackMapper | KEEP | 9 | 7 | 2 |
| VocalHealthSupervisor (+VocalHealthTrendEngine) | KEEP | 9 | 9 | 2 |
| AudioAnalysisEngine | KEEP | 8 | 8 | 3 |
| AudioAnalyzerService | KEEP | 8 | 8 | 2 |
| ComplexityEngine | KEEP | 8 | 7 | 4 |
| ExerciseSessionRecorder | KEEP | 8 | 9 | 3 |
| FeedbackPipeline | KEEP | 8 | 9 | 2 |
| IExerciseProfileStore -> SqliteExerciseProfileStore | KEEP | 8 | 8 | 3 |
| InlineCoachFeedbackMapper | KEEP | 8 | 8 | 2 |
| MicrophoneCalibrationService | KEEP | 8 | 8 | 2 |
| SessionAnalyticsStore | KEEP | 8 | 9 | 3 |
| SmartCoachEngine | FIX | 8 | 7 | 4 |
| Subjektiv rapport-kjeden | KEEP | 8 | 7 | 2 |
| ComfortZoneState / ZoneConfiguration | KEEP | 7 | 8 | 2 |
| DirectionAnalyzer | KEEP | 7 | 7 | 2 |
| ExerciseTextService | KEEP | 7 | 6 | 2 |
| HydrationAdvisor (+HydrationAdvisorOptions) | KEEP | 7 | 7 | 2 |
| HydrationFeedbackMapper | KEEP | 7 | 6 | 2 |
| IExerciseProfileFactory -> ExerciseProfileFactory | KEEP | 7 | 7 | 3 |
| ProgressionFeedbackMapper | KEEP | 7 | 7 | 2 |
| SmartCoachHealthMonitoring IsRead-kolonne (skjema/lese- | FIX | 7 | 6 | 8 |
| VoiceGoalProfile-systemet | KEEP | 7 | 7 | 2 |
| FeedbackService | FIX | 6 | 6 | 3 |
| InMemoryExerciseRepository (IUserRepository + IScoreRep | FIX | 6 | 5 | 5 |
| LiveMetricsService | KEEP | 6 | 7 | 2 |
| RealtimeAnalysisEngine (inkl. RollingBuffer<T> og Signa | DELETE | 6 | 7 | 8 |
| SpectrogramResonanceMapper | KEEP | 6 | 6 | 2 |
| VocalHealthBaselineProvider | KEEP | 6 | 7 | 2 |
| AnalysisSubsystem (implementasjonsklassen, IKKE typene  | DELETE | 5 | 5 | 9 |
| LevelClassificationSystem | FIX | 5 | 5 | 5 |
| SmartCoachFeedbackMapper | FIX | 5 | 6 | 4 |
| StrainMonitor (VoiceHealthModule) | DELETE | 5 | 3 | 6 |
| TrainingFrequencyService | FIX | 5 | 5 | 4 |
| AdaptivePitchDetector (inkl. RollingStatistics) | DELETE | 4 | 5 | 8 |
| ExerciseSummaryView + ExerciseSummaryViewModel | DELETE | 4 | 3 | 6 |
| LiveFeedbackView + LiveFeedbackViewModel | DELETE | 4 | 3 | 6 |
| PeriodizationService | DELETE | 4 | 3 | 7 |
| ProgressionEngine | DELETE | 4 | 3 | 7 |
| RestProtocolService (VoiceHealthModule) | DELETE | 4 | 2 | 5 |
| SpeechRateAnalyzer | DELETE | 4 | 4 | 6 |
| TrendAlertService | DELETE | 4 | 3 | 6 |
| VoiceMetricsCalculator | DELETE | 4 | 5 | 7 |
| VoiceStrainDetector (inkl. StrainAnalysis/StrainLevel) | DELETE | 4 | 4 | 8 |
| AdaptiveDifficultyService | DELETE | 3 | 2 | 6 |
| AdaptiveTargetZoneService | DELETE | 3 | 3 | 5 |
| AsyncAudioPipeline | DELETE | 3 | 5 | 7 |
| DataSubsystem (backup/eksport/RestoreBackup-bug) | DELETE | 3 | 3 | 7 |
| ProgressionConfig | DELETE | 3 | 2 | 7 |
| ProgressionDashboardViewModel (ViewModels-namespace) | DELETE | 3 | 2 | 8 |
| ProgressionRateCalculator | DELETE | 3 | 2 | 6 |
| SmartCoachDashboardView | DELETE | 3 | 2 | 6 |
| TrendAnalysisService | DELETE | 3 | 4 | 5 |
| VoiceActivityDetector (inkl. avhengighet av RollingStat | DELETE | 3 | 4 | 7 |
| VoiceFeminizationExerciseService + ResonanceModuleDocum | DELETE | 3 | 2 | 7 |
| VoiceHealthService (sesjons-/pause-timer) | DELETE | 3 | 2 | 6 |
| VoiceProfileExtensions (VoiceProfile/ExerciseEffectiven | DELETE | 3 | 2 | 6 |
| WeeklyPlannerEngine | DELETE | 3 | 2 | 6 |
| CoachMessageGenerator / CoachMessageFormatter | DELETE | 2 | 2 | 6 |
| ExerciseFeedbackEngine | DELETE | 2 | 3 | 7 |
| ExerciseListViewModel | DELETE | 2 | 2 | 5 |
| FeedbackRuleEngine (CompositeEvaluator + 4 IRuleEvaluat | DELETE | 2 | 3 | 6 |
| GamificationService | DELETE | 2 | 2 | 7 |
| IComfortZoneRepository | DELETE | 2 | 1 | 5 |
| Models: PeriodizationModels / TrainingLoad / WeeklySche | DELETE | 2 | 2 | 6 |
| PitchChartViewModel | DELETE | 2 | 2 | 6 |
| SmartCoachExerciseAdapter | DELETE | 2 | 2 | 6 |
| Subsystems/ (Audio/Analysis/Data/Progression/SmartCoach | FIX | 2 | 4 | 9 |
| VoiceStrainDetector | DELETE | 2 | 2 | 6 |
| .old/.old2-filer i hele treet | DELETE | 1 | 1 | 7 |
| AudioAnalysisEngine_new.cs (tom stub) | DELETE | 1 | 1 | 4 |
| In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert | DELETE | 1 | 2 | 8 |
| Infra/DependencyInjection.cs (AddFemVoiceStudio + exten | DELETE | 1 | 3 | 9 |
| Migration-SQL 001_exercise_feedback_system.sql | DELETE | 1 | 1 | 6 |
| MockAudioAnalysisEngine (inni AudioAnalysisEngine.cs) | DELETE | 1 | 2 | 5 |
| SmartCoachBaselines (plural) orphan-tabell | DELETE | 1 | 1 | 5 |
| ViewModelBase / SubsystemViewModelBase | DELETE | 1 | 2 | 7 |
| generate_comfort.py (stub) | DELETE | 1 | 1 | 3 |
| part2.cs (tomt namespace) | DELETE | 1 | 1 | 4 |

---

## Seksjon 5 – Activation Candidates

**Ingen.** Samtlige systemer som oppfylte ACTIVATE-kriteriene ble aktivert i forrige runde
(se commits `6b7f152` Critical, `62c4c1d` Important, `fcc4f70` reviewfikser). Verifisert av
begge skeptikere mot HEAD.

---

## Seksjon 6 – Repair Candidates (FIX)

#### Subsystems/ (Audio/Analysis/Data/Progression/SmartCoach + I*-interfaces og *Subsystem-klasser)
- **Fil:** `FemVoiceStudio/Subsystems/ (Analysis/AnalysisSubsystem.cs, Audio/AudioSubsystem.cs, Data/DataSubsystem.cs, Progression/ProgressionSubsystem.cs, SmartC`
- **Begrunnelse:** Subsystem-INTERFACENE og -KLASSENE (IAnalysisSubsystem/AnalysisSubsystem, IAudioSubsystem/AudioSubsystem, IDataSubsystem/DataSubsystem, I/ProgressionSubsystem, I/SmartCoachSubsystem) er DOEDE - refereres kun av ViewModelBase + Infra/DependencyInjection.cs + hverandre. MEN mappen kan IKKE slettes raatt: filen Subsystems/Analysis/IAnalysisSubsystem.cs definerer ogsaa TYPENE 'enum ResonanceCategory' (eneste definisjon i repoet) og 'class VoiceMetrics' (Subsystems-varianten), som brukes av AKTIV kode: Models/TrainingSession.cs:43 (ResonanceCategory), Models/Feedback.cs:69 (ResonanceCategory), Views/ResonanceWindow.xaml.cs:321 (cast (Subsystems.Analysis.ResonanceCategory)), og Services/ExerciseFeedbackEngine.cs:7 (alias VoiceAnalysisMetrics = Subsystems.Analysis.VoiceMetrics). I tillegg har Audio/ResonansScoringService.cs en UNOEDVENDIG 'using FemVoiceStudio.Subsystems.Analysis' (bruker ingen type derfra - kun lokal AudioResonanceCategory). Klassifiseres FIX (ikke DELETE) nettopp fordi en trygg fjerning krever et avhengighetsflytt foerst, ikke bare en sletting. Ekstremt hoy maintenanceCost: stor doed kodebase som ogsaa skjuler at den hoster to live typer.
- **Detaljer:** FIX (klargjoer for sletting): Brutt logikk/manglende kobling: subsystem-interfacene har null aktive konsumenter, men IAnalysisSubsystem.cs er feilaktig samlokalisert med to live typer (ResonanceCategory, VoiceMetrics). Avhengigheter som maa loeses foer DELETE: (1) flytt 'enum ResonanceCategory' til Models/ (f.eks. Models/ResonanceCategory.cs) og oppdater using i TrainingSession.cs, Feedback.cs, ResonanceWindow.xaml.cs (casten paa l.321); (2) avgjoer skjebnen til Subsystems.Analysis.VoiceMetrics - den brukes kun av ExerciseFeedbackEngine, som selv kun naas av doede LiveFeedback/ExerciseSummary-VMer; hvis de slettes kan VoiceMetrics-varianten + ExerciseFeedbackEngine ryke samlet; (3) fjern den unoedvendige using i ResonansScoringService.cs:5. Etter dette kan HELE Subsystems-mappen + Infra/DependencyInjection.cs + ViewModelBase.cs slettes samlet. Beroerte tester: ExerciseFeedbackEngineTests.cs (eneste test med 'using FemVoiceStudio.Subsystems.Analysis').

#### SmartCoachHealthMonitoring IsRead-kolonne (skjema/lese-mismatch)
- **Fil:** `FemVoiceStudio/Data/DatabaseService.cs (CREATE l.324, SaveHealthMonitoring l.2077, GetRecentHealthIssues l.2104)`
- **Begrunnelse:** Bekreftet UFIKSET ved HEAD og verre enn baseline antydet. CREATE TABLE SmartCoachHealthMonitoring (l.324-334) har 9 kolonner og INGEN IsRead (IsRead paa l.343 tilhoerer den separate SmartCoachMessages-tabellen). Ingen ALTER/AddColumnIfNotExists legger til IsRead. Likevel: SaveHealthMonitoring (l.2085-2086) INSERTer @IsRead -> SQLite-feil (no such column). GetRecentHealthIssues (l.2132-2133) leser reader.GetInt32(8) som IsRead (men index 8 er egentlig CreatedAt) OG reader.GetString(9) som CreatedAt -> IndexOutOfRange (kun 9 kolonner, gyldige indekser 0-8). Bonus-bug: interface IDatabaseService.GetRecentHealthIssues(userId, days) har MOTSATT parameterrekkefoelge av impl GetRecentHealthIssues(days, userId) - reddes i dag kun av navngitte argumenter. Lese-stien naas allerede i prod via SmartCoachViewModel (CalculateWeeklyProgress/GenerateGoals/GenerateDailyRecommendation/GetStatusSummary) + PeriodizationService, men krasjer ikke i dag fordi tabellen alltid er TOM (skrive-stien AnalyzeSessionForStrain har ingen prod-kaller, saa while(reader.Read()) gaar aldri inn). Bomben utloeses i det AnalyzeSessionForStrain noensinne wires.
- **Detaljer:** FIX: Brutt logikk/manglende kolonne. Manglende data: legg IsRead INTEGER DEFAULT 0 til i CREATE TABLE SmartCoachHealthMonitoring (l.324-334) OG som AddColumnIfNotExists(connection, 'SmartCoachHealthMonitoring', 'IsRead', 'INTEGER DEFAULT 0') i RunMigrations (slik at eksisterende databaser fra HEAD-skjemaet healer). Da blir SELECT * = 10 kolonner og lese-mappingen (index 8 IsRead, index 9 CreatedAt) korrekt. Rett ogsaa parameterrekkefoelge-mismatchen mellom IDatabaseService.cs:34 og DatabaseService.cs:2104 (samme signatur begge steder) for aa fjerne fremtidig footgun. Beroerte filer: DatabaseService.cs (+ IDatabaseService.cs for signatur). Tester: SmartCoachDecisionTests.cs (AnalyzeSessionForStrain) og TestDatabaseService.cs i FemVoiceStudio.Tests boer kjore mot rettet skjema. Hoy klinisk verdi fordi dette er stemmehelse-/strain-overvaaking - den feilen blokkerer reell aktivering av AnalyzeSessionForStrain.

#### LevelClassificationSystem
- **Fil:** `FemVoiceStudio/Services/LevelClassificationSystem.cs`
- **Begrunnelse:** Statiske display-metoder (GetLevelName/Emoji/Focus/Tolerance/PitchRange/ResonanceMinimum/RecommendedExercises) ER aktive i live ProgressionDashboard (linje 233-235) og ProgressionDashboardViewModel. MEN instans-metoden Classify(...) (linje 225) kalles aldri i produksjon - kun fra Tests/LevelClassificationSystemTests.cs. Classify dupliserer ProgressionService sin promoterings-/demoteringslogikk (7-av-10/5-av-10-terskler, 14d mellom overganger). Dashboardet leser nivaet fra userSettings.CurrentDifficulty (eid av ProgressionService), ikke fra Classify. Filen kan ikke slettes (display-metodene er load-bearing for UI), men Classify + IDatabaseService-felt + instanskonstruktorene er dod vekt.
- **Detaljer:** FIX: behold de statiske display-/konfig-metodene (live i ProgressionDashboard). Fjern eller aktiver den ubrukte instans-Classify-metoden, _database-feltet og instanskonstruktorene (inkl. [Obsolete] DatabaseService-ctor). Hvis Classify slettes ma Tests/LevelClassificationSystemTests.cs justeres (Classify-testene fjernes). Brutt logikk: Classify er en konkurrerende, aldri-koblet kilde til nivabeslutning - forvirringsrisiko mot ProgressionService.

#### InMemoryExerciseRepository (IUserRepository + IScoreRepository)
- **Fil:** `FemVoiceStudio/Services/InMemoryExerciseRepositories.cs`
- **Begrunnelse:** Bekreftet NAA AKTIV (baseline-oppdatering stemmer): registrert i App.xaml.cs:116-118 som seg selv + IUserRepository + IScoreRepository, og injiseres i de aktive ComfortZoneController (l.82-83) og FemVoiceScoreEngine (l.115-116) som begge drives ved oktslutt. Klassen er imidlertid IN-MEMORY (Dictionary/List bak en lock) og fila sin egen doc sier eksplisitt at den kun holder ting konstruerbart 'until these values are backed by the main SQLite data model'. Det betyr at komfortsone-justeringer, scoring-config, helse-/toleransedata og score-snapshots IKKE persisteres mellom oekter -> klinisk adaptiv progresjon nullstilles ved hver app-start. Derfor FIX, ikke KEEP.
- **Detaljer:** FIX: Manglende persistens. Brutt logikk: alle Save*Async-metoder skriver kun til in-memory-strukturer som forsvinner ved app-avslutning; Get*Async returnerer defaults paa neste oppstart. Manglende data/avhengighet: en SQLite-backet implementasjon av IUserRepository/IScoreRepository (via DatabaseService) maa skrives og registreres i App.ConfigureServices istedenfor (eller i tillegg til) InMemoryExerciseRepository. Inntil da fungerer runtime-wiring, men klinisk verdi (langtids komfortsone/score-baseline) gaar tapt. Ingen sletting - dette er en fungerende stubb som boer oppgraderes, ikke fjernes.

#### SmartCoachFeedbackMapper
- **Fil:** `FemVoiceStudio/Services/FeedbackPipeline.cs`
- **Begrunnelse:** DI-registrert (App.xaml.cs:164) og injisert i den aktive SmartCoachEngine, men de eneste produksjonsstiene som ruter gjennom mapperen (SaveCoachMessageThroughPipeline kalt fra AnalyzeSessionForStrain + GenerateMotivationalMessages) kalles ALDRI i prod. Mapperen er arkitektonisk korrekt og testet (FeedbackConsistencyGuardTests). Den er ikke død (den er korrekt koblet inn i en aktiv tjeneste), men effektivt inert fordi vertsmetodene er sovende. Klassifiseres FIX fordi aktivering avhenger av å fikse/aktivere SmartCoachEngine-metodene (særlig IsRead-skjemabugen i AnalyzeSessionForStrain-stien), ikke bare wiring.
- **Detaljer:** Brutt logikk/avhengighet: mapperens eneste prod-sti (SmartCoachEngine.SaveCoachMessageThroughPipeline) nås kun fra AnalyzeSessionForStrain (krasjer på IsRead-skjemabug i SaveHealthMonitoring) og GenerateMotivationalMessages (aldri kalt). Aktivering krever: (1) fikse SmartCoachHealthMonitoring-tabellen (mangler IsRead-kolonne) i DatabaseService.cs:324-334 vs INSERT på 2084-2086 / lesing GetRecentHealthIssues:2132; (2) kalle GenerateMotivationalMessages/AnalyzeSessionForStrain fra en runtime-konsument (f.eks. SmartCoachViewModel.LoadDataAsync eller MainViewModel.StopRecording).

#### SmartCoachEngine
- **Fil:** `FemVoiceStudio/Services/SmartCoachEngine.cs`
- **Begrunnelse:** Dashboard-metoder aktive (CalculateBaseline/GenerateGoals/GenerateDailyRecommendation/GetStatusSummary/CalculateWeeklyProgress kalles av SmartCoachViewModel; AdaptiveComfortZoneService bruker også engine, MainViewModel:197). MEN to helse-/motivasjonsmetoder er sovende OG én har en latent runtime-krasj: AnalyzeSessionForStrain (linje 508) kaller SaveHealthMonitoring som INSERTer kolonnen IsRead i SmartCoachHealthMonitoring-tabellen — tabellen har ingen IsRead-kolonne (DatabaseService.cs:324-334), så INSERT feiler; GetRecentHealthIssues leser dessuten reader.GetInt32(8) som IsRead (faktisk CreatedAt) og GetString(9) som ville kaste IndexOutOfRange. GenerateMotivationalMessages (linje 579) kalles aldri i prod. Engine beholdes (klinisk verdifull, mye er aktivt), men de sovende metodene + skjemabugen må fikses for full verdi.
- **Detaljer:** Brutt logikk/skjemabug: SmartCoachHealthMonitoring-tabellen (DatabaseService.cs:324) mangler IsRead-kolonne, men SaveHealthMonitoring (2084-2086) INSERTer IsRead og GetRecentHealthIssues (2132-2133) leser kolonneindeks 8/9 feil → runtime-feil i AnalyzeSessionForStrain-stien. Manglende kall: GenerateMotivationalMessages og AnalyzeSessionForStrain har ingen prod-konsument. Fix: (1) legg IsRead-kolonne i tabellen ELLER fjern IsRead fra INSERT/SELECT-mapping og rett kolonneindekser; (2) kall metodene fra en runtime-konsument.

#### TrainingFrequencyService
- **Fil:** `FemVoiceStudio/Services/TrainingFrequencyService.cs`
- **Begrunnelse:** Instansieres i ExerciseWindow (linje 115) men INGEN metode kalles noen gang — _trainingService forekommer kun ved feltdeklarasjon (linje 41) og konstruksjon (linje 115). Output (GetTodaysRecommendations/GetTodaysStatus/GetWeeklyProgress/GetMotivationalMessage) forbrukes ikke i UI. Klassen er ferdig implementert og klinisk nyttig (treningsfrekvens/ukesmål), men instansieres unødvendig og er ren dead weight slik den står. Klassifiseres FIX (ikke ren ACTIVATE) fordi den allerede er instansiert men feilkoblet — wiring av faktiske metodekall + UI-binding mangler, ikke bare DI-registrering.
- **Detaljer:** Manglende kall: ingen av tjenestens metoder kalles fra ExerciseWindow etter instansiering. Aktiveringspunkt: ExerciseWindow.UpdateTodaysStatus (Views/ExerciseWindow.xaml.cs:571) — vis GetTodaysStatus/GetWeeklyProgress ved siden av minutter/økter, eller fjern den ubrukte instansieringen om frekvens-UI ikke ønskes.

#### FeedbackService
- **Fil:** `FemVoiceStudio/Services/FeedbackService.cs`
- **Begrunnelse:** Delvis aktiv: GenerateFeedback (MainViewModel:403) og GetRealtimeFeedback (MainViewModel:582/624) kalles aktivt. MEN tre metoder er døde innad: GetProgressiveTip (linje 235) har ingen kaller; GenerateResonanceFeedback (linje 270) kalles kun internt av AddResonanceFeedback (linje 351), som selv ingen kaller utenfor klassen. Tjenesten beholdes (kjerne-metodene er aktive), men de tre ubrukte metodene gir falske grep-treff og bør enten aktiveres (resonans-feedback er klinisk relevant via ResonansSessionResult) eller fjernes.
- **Detaljer:** Manglende kall: GetProgressiveTip og AddResonanceFeedback/GenerateResonanceFeedback har ingen prod-konsument. ResonansSessionResult produseres av Audio/ResonansScoringService og brukes i ResonanceWindow — aktiveringspunkt for resonans-feedback finnes der. Fix: kall AddResonanceFeedback fra ResonanceWindow/MainViewModel resonans-sti, eller fjern de tre ubrukte metodene.


---

## Seksjon 7 – Deletion Candidates (DELETE)

> Ingenting er slettet — kun identifisert, med verifisert trygg slettesti per system.
> DELETE-skeptikeren forsøkte å motbevise samtlige og bekreftet alle 47.

#### AnalysisSubsystem (implementasjonsklassen, IKKE typene i IAnalysisSubsystem.cs)
- **Fil:** `FemVoiceStudio/Subsystems/Analysis/AnalysisSubsystem.cs`
- **Begrunnelse:** Funnet under audit, ikke i baseline: AnalysisSubsystem er registrert i Infra/DependencyInjection.cs (AddTransient<IAnalysisSubsystem,AnalysisSubsystem>), men AddFemVoiceStudio() KALLES ALDRI — appen bruker App.xaml.cs ConfigureServices i stedet. Eneste konsument er SubsystemViewModelBase som IKKE har subklasser. Klassen new-er VoiceActivityDetector og VoiceStrainDetector (de døde) og duplikerer ResonansScoringService sin resonanslogikk. KRITISK ADVARSEL: filen IAnalysisSubsystem.cs i samme mappe definerer de AKTIVE typene VoiceMetrics/ResonanceCategory/VoiceParameter/TargetZone/HealthIndicators (brukt av FeedbackService, ResonanceWindow, DatabaseService, ResonansScoringService, ExerciseFeedbackEngine, TrainingSession, Feedback) — de må BEHOLDES.
- **Detaljer:** Hvorfor foreldet: AddFemVoiceStudio kalles aldri; ingen SubsystemViewModelBase-subklasser; duplikat av aktiv analyse-sti. Trygg slettesti: (1) slett AnalysisSubsystem.cs (implementasjonsklassen) — den er eneste live-eier av VoiceActivityDetector/VoiceStrainDetector sammen med RealtimeAnalysisEngine. (2) BEHOLD IAnalysisSubsystem.cs (type-definisjoner er aktive). (3) Fjern AddTransient<IAnalysisSubsystem,AnalysisSubsystem> og AnalysisSubsystemFactory.Create() i Infra/DependencyInjection.cs. Berørte filer: AnalysisSubsystem.cs (slett), Infra/DependencyInjection.cs (rediger). Ingen tester refererer klassen direkte. MERK: del av samme dead-klynge som VoiceActivityDetector/VoiceStrainDetector — slett samlet.

#### Infra/DependencyInjection.cs (AddFemVoiceStudio + extensions + AnalysisSubsystemFactory)
- **Fil:** `FemVoiceStudio/Infra/DependencyInjection.cs`
- **Begrunnelse:** App.OnStartup (App.xaml.cs:29) kaller sin EGEN inline ConfigureServices, IKKE AddFemVoiceStudio. Grep paa AddFemVoiceStudio/AnalysisSubsystemFactory gir null treff utenfor denne fila. Dette er den komplette doede gen-2 DI-grafen som registrerer alle de doede Subsystem-interfacene + ProgressionEngine/WeeklyPlannerEngine. Svaert hoy maintenanceCost: ser ut som den ekte DI-roten (klassisk navn), gir et FULLSTENDIG parallell-univers av registreringer som er rene loegner om hva som faktisk kjorer, og er en av kun to gjenvaerende konsumenter som holder Subsystem-interfacene kompilerende.
- **Detaljer:** DELETE: Erstattet-av App.ConfigureServices. Trygg slettesti: slett hele Infra/DependencyInjection.cs (helst sammen med Subsystems-mappen og ViewModelBase.cs i samme rydderunde, siden disse tre er de eneste tingene som holder hverandre kompilerende). Hvis Subsystems slettes separat foerst, vil DependencyInjection.cs ikke kompilere (refererer DataSubsystem/AnalysisSubsystem osv.). Ingen tester refererer fila.

#### RealtimeAnalysisEngine (inkl. RollingBuffer<T> og SignalSmoothing)
- **Fil:** `FemVoiceStudio/Audio/RealtimeAnalysisEngine.cs`
- **Begrunnelse:** Den rikeste parallellmotoren (YIN+LPC+strain+lockout), men null `new RealtimeAnalysisEngine` i hele kodebasen (Views/ViewModels/Services/Subsystems/Tests). Aldri koblet til capture-kjeden. Klinisk verdi i strain/lockout-konseptet er reelt, men er nå dekket av den AKTIVE VocalHealthSupervisor/SafetyLock-stien — denne er en foreldet duplikat. Høy maintenanceCost: 717 linjer som gir falske grep-treff og holder VoiceStrainDetector/PitchDetection/FormantDetection kunstig i live via interne news.
- **Detaljer:** Hvorfor foreldet: aldri instansiert; strain/lockout dekkes av aktiv VocalHealthSupervisor-sti. Erstattet-av: VocalHealthSupervisor/SafetyLock. RollingBuffer<T> (linje 546) og SignalSmoothing (linje 636) er definert i denne filen og brukes KUN her — trygt å slette med filen. Trygg slettesti: slett hele filen; ingen eksterne referanser til RealtimeAnalysisEngine, RollingBuffer eller SignalSmoothing. Berørte filer: kun RealtimeAnalysisEngine.cs. Ingen tester refererer den. MERK: slett som klynge sammen med VoiceStrainDetector (felles eierskap til VoiceStrainDetector-instans her).

#### AdaptivePitchDetector (inkl. RollingStatistics)
- **Fil:** `FemVoiceStudio/Audio/AdaptivePitchDetector.cs`
- **Begrunnelse:** Eneste referanse er fra AsyncAudioPipeline:31 (selv død). Produksjons-pitch går via PitchDetectionService/AudioAnalysisEngine. KRITISK: denne filen DEFINERER RollingStatistics (linje 158), som også brukes av VoiceStrainDetector og VoiceActivityDetector. Høy maintenanceCost fordi den binder hele den døde detektor-klyngen sammen.
- **Detaljer:** Hvorfor foreldet: kun via død AsyncAudioPipeline; erstattet av PitchDetectionService. Trygg slettesti: RollingStatistics (definert her) brukes av VoiceStrainDetector og VoiceActivityDetector — disse MÅ slettes i SAMME klynge, ellers brytes kompilering. Slett rekkefølge/klynge: AsyncAudioPipeline + AdaptivePitchDetector + VoiceActivityDetector + VoiceStrainDetector + RealtimeAnalysisEngine + AnalysisSubsystem sammen. Berørte filer: AdaptivePitchDetector.cs. Ingen tester.

#### VoiceStrainDetector (inkl. StrainAnalysis/StrainLevel)
- **Fil:** `FemVoiceStudio/Audio/VoiceStrainDetector.cs`
- **Begrunnelse:** Kalles kun fra to verter utenfor produksjons-runtime: AnalysisSubsystem:32 (via ubrukt AddFemVoiceStudio) og RealtimeAnalysisEngine:97 (aldri new-et). I tillegg KLINISK MISVISENDE: JitterValue=0 og ShimmerValue=0 er hardkodet (linje 42-43), og CalculateStdDev returnerer Mean*0.1 (linje 98) — en placeholder som ignorerer pitch-historikken den samler. Strain/lockout i produksjon dekkes av aktiv VocalHealthSupervisor/SafetyLock. Høy maintenanceCost pga risiko for at noen tror jitter/shimmer faktisk beregnes.
- **Detaljer:** Hvorfor foreldet: begge verter døde; jitter/shimmer/stddev er placeholder (linje 42-43, 98); erstattet av VocalHealthSupervisor-stien. Avhenger av RollingStatistics (AdaptivePitchDetector.cs). Trygg slettesti: slett sammen med RealtimeAnalysisEngine og AnalysisSubsystem (de eneste som new-er den) som klynge. StrainAnalysis/StrainLevel-typene defineres i denne filen og brukes kun av disse døde verterne. Berørte filer: VoiceStrainDetector.cs. Ingen tester.

#### ProgressionDashboardViewModel (ViewModels-namespace)
- **Fil:** `FemVoiceStudio/ViewModels/ProgressionDashboardViewModel.cs`
- **Begrunnelse:** Navnekollisjon bekreftet: det finnes TO klasser ved navn ProgressionDashboardViewModel. Den i FemVoiceStudio.ViewModels (denne fila) refereres ALDRI. Den aktive er den nestede i Views/ProgressionDashboard.xaml.cs (namespace FemVoiceStudio.Views), som instansieres paa linje 25 'new ProgressionDashboardViewModel(this)' og hostes via ProgressionWindow -> ProgressionDashboard (MainWindow.xaml.cs:531). Hoy maintenanceCost nettopp pga. den identiske navngivningen i to namespace - svaert forvirrende ved grep/refaktorering.
- **Detaljer:** DELETE: Duplikat (navnekollisjon) - den aktive varianten ligger i Views/ProgressionDashboard.xaml.cs. Trygg slettesti: slett FemVoiceStudio/ViewModels/ProgressionDashboardViewModel.cs. Siden den ligger i et annet namespace (ViewModels vs Views) og ingen 'using FemVoiceStudio.ViewModels' refererer typen, paavirkes ingen kompilering. Ingen tester refererer den.

#### In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert inn i WPF-exe)
- **Fil:** `FemVoiceStudio/Tests/{CoachMessageGeneratorTests.cs, DirectionAnalyzerTests.cs, FemVoiceScoreTests.cs, LevelClassificationSystemTests.cs, VoiceProfile`
- **Begrunnelse:** 5 xunit-testfiler ligger i selve WPF-prosjektet (FemVoiceStudio/Tests/) og kompileres INN i WinExe-en fordi csproj inkluderer xunit + Microsoft.NET.Test.Sdk PackageReferences og har default Compile-glob uten Remove. Det riktige stedet er FemVoiceStudio.Tests/. FemVoiceScoreTests.cs er attpaatil et DUPLIKAT (finnes ogsaa som FemVoiceStudio.Tests/FemVoiceScoreTests.cs). VoiceProfileExtensionsTests er den eneste tingen som holder den doede VoiceProfileExtensions 'i bruk'. Svaert hoy maintenanceCost: dra-test-rammeverk inn i produksjons-exe, duplikat testnavn, og maskerer doede produksjonsklasser.
- **Detaljer:** DELETE: Feilplassert/duplikat. Trygg slettesti: slett hele FemVoiceStudio/Tests/-mappen (alle 5 filer). For de fire som tester FORTSATT-aktive klasser (CoachMessageGenerator, DirectionAnalyzer, FemVoiceScore, LevelClassificationSystem) boer dekningen verifiseres mot FemVoiceStudio.Tests/ foer sletting (FemVoiceScore er allerede dekket der). VoiceProfileExtensionsTests slettes sammen med selve VoiceProfileExtensions.cs (se den raden). Ideelt boer ogsaa xunit/Test.Sdk PackageReferences fjernes fra FemVoiceStudio.csproj etterpaa saa testrammeverket ikke lenger lekker inn i exe-en. Ingen produksjonskode importerer disse testene, saa hovedappen kompilerer uendret.

#### PeriodizationService
- **Fil:** `FemVoiceStudio/Services/PeriodizationService.cs`
- **Begrunnelse:** Null prod-kallere (kun PeriodizationServiceTests i SafetyLockTests.cs). Har eget separat safety-lock-/fase-system (3 aktive + 1 vedlikehold) med egne events (PhaseTransition/ProgressionBlocked) som aldri abonneres. Overlapper ProgressionSafetyGate (persistert helse-gate) og ProgressionOrchestrator (recovery/pause). Hoy maintenanceCost: stort dodt subsystem med egne modeller + tester som gir falsk inntrykk av aktivt periodiseringssystem.
- **Detaljer:** DELETE. Berorte filer: Services/PeriodizationService.cs; Models/PeriodizationModels.cs (PeriodizationConfig/State/Result/WeeklyTrainingStats - konsumeres kun her); test: PeriodizationServiceTests-klassen INNE i FemVoiceStudio.Tests/SafetyLockTests.cs (linje ~331-slutt) ma fjernes (IKKE hele filen - resten tester aktiv SafetyLock). Trygg slettesti: fjern PeriodizationServiceTests-klassen, slett PeriodizationService.cs + PeriodizationModels.cs. Sjekk at PeriodizationCycle-enum (definert i ProgressionSessionData.cs) ikke etterlates ubrukt-i-praksis (brukes kun av dode ProgressionConfig/WeeklyPlanner).

#### GamificationService
- **Fil:** `FemVoiceStudio/Services/GamificationService.cs`
- **Begrunnelse:** Null prod-referanser (ingen new GamificationService). XP/level/achievements/streak-motor som aldri kjorer. Achievements-PERSISTENS (CREATE TABLE/INSERT/GetAchievements/UnlockAchievement) ligger i DatabaseService (linje 121/607/1537/1570) - GamificationService er en separat, ubrukt duplikat-implementasjon av achievement-logikken. Hoy maintenanceCost: false-positive grep-treff mot DatabaseService.UnlockAchievement antyder at gamification er aktiv naar den ikke er det. Lav klinisk verdi (gamification nevnes ikke i klinisk arkitekturdok).
- **Detaljer:** DELETE. Berorte filer: Services/GamificationService.cs (inkl. SessionReward/AchievementUnlockedEventArgs/LevelUpEventArgs/UserProgress hvis definert i samme fil - verifiser). Ingen testfil refererer GamificationService. Trygg slettesti: DatabaseService sin achievement-API er uavhengig og forblir intakt; slett kun GamificationService.cs etter a ha bekreftet at dens egne event-/reward-typer ikke brukes annensteds.

#### ProgressionEngine
- **Fil:** `FemVoiceStudio/Services/Progression/ProgressionEngine.cs`
- **Begrunnelse:** Kun registrert i Infra/DependencyInjection.cs:28 via AddFemVoiceStudio() - som ALDRI kalles (prod-DI er App.xaml.cs ConfigureServices, som ikke kaller AddFemVoiceStudio). Aldri instansiert i prod. EvaluateSession/EvaluateProgressionWithSafety overlapper ProgressionService + ProgressionOrchestrator. Definerer en KONKURRERENDE ProgressionGateStatus (det finnes ogsa en i Models/ProgressionSessionData.cs) - duplikat-type som gir forvirring. Hoy maintenanceCost.
- **Detaljer:** DELETE. Berorte filer: Services/Progression/ProgressionEngine.cs (inkl. dens egen ProgressionGateStatus). Trygg slettesti: fjern registreringen i Infra/DependencyInjection.cs:28; helst slett hele DependencyInjection.cs/Subsystems-/Infra-grenen som dod (samordnes med Subsystems-omradeansvarlig). Avhenger av UserProgressionProfile (dod) og ComplexityEngine (aktiv - ComplexityEngine ma BEHOLDES, kun referansen fra ProgressionEngine fjernes). Ingen testfil refererer ProgressionEngine.

#### ProgressionConfig
- **Fil:** `FemVoiceStudio/Services/ProgressionConfig.cs`
- **Begrunnelse:** 330-linjers konfig kun konsumert av dode ProgressionEngine/WeeklyPlannerEngine (CreateDefault/CreateForBeginner/CreateForAdvanced). Inneholder OGSA ProgressionEvaluationResult (linje 304) som har NULL konsumenter. Refererer flere shared typer (ProgressionGateStatus, PeriodizationCycle, SessionType, ProgressionMilestone, ProgressionMode) - ma slettes SAMMEN med de dode motorene for ren kompilering. Hoy maintenanceCost pga storrelse og falsk inntrykk av aktivt config-system.
- **Detaljer:** DELETE (sammen med ProgressionEngine/WeeklyPlannerEngine). Berorte filer: Services/ProgressionConfig.cs (inkl. ProgressionEvaluationResult). Trygg slettesti: ma slettes I SAMME runde som ProgressionEngine + WeeklyPlannerEngine (de er eneste konsumenter). ProgressionEvaluationResult er allerede helt ubrukt. Pass pa: SessionType (definert i ScoreSnapshot.cs) og ProgressionMode/ProgressionMilestone brukes andre steder - kun referansene FRA ProgressionConfig forsvinner, ikke selve enum-/modelldefinisjonene. PeriodizationCycle blir ubrukt etter sletting (definert i ProgressionSessionData.cs).

#### ExerciseFeedbackEngine
- **Fil:** `FemVoiceStudio/Services/ExerciseFeedbackEngine.cs`
- **Begrunnelse:** Død i prod. Aldri instansiert i en aktiv sti — refereres kun fra LiveFeedbackViewModel (linje 17/69/85) og ExerciseSummaryViewModel.LoadFromEngine (linje 140), som begge er døde (vertskontrollene hostes ikke). Live-feedback i prod går via ExerciseIntelligenceCoordinator + ExerciseSessionRecorder; klinisk øktscore beregnes av ClinicalSessionScore. Høy maintenanceCost: stor klasse + 13 testtilfeller (ExerciseFeedbackEngineTests.cs) gir betydelig falskt aktivitetsinntrykk.
- **Detaljer:** Foreldet/erstattet-av: ExerciseIntelligenceCoordinator/ExerciseSessionRecorder + ClinicalSessionScore. Berørte filer: ExerciseFeedbackEngine.cs; FemVoiceStudio.Tests/ExerciseFeedbackEngineTests.cs (slett hele testfilen); de døde vertene LiveFeedbackViewModel.cs + LiveFeedbackView.xaml(.cs) og ExerciseSummaryViewModel.cs + ExerciseSummaryView.xaml(.cs) (som også bruker SmartCoachExerciseAdapter). Models/ExerciseEvaluationResult.cs har kun dokkommentar-referanse (ikke kode-avhengighet) — behold. Trygg slettesti: slett engine + dens testfil + de to døde view-paret samlet; verifiser at ingen XAML hoster LiveFeedbackView/ExerciseSummaryView (bekreftet: ingen gjør det).

#### VoiceFeminizationExerciseService + ResonanceModuleDocumentation
- **Fil:** `FemVoiceStudio/Services/VoiceFeminizationExerciseService.cs`
- **Begrunnelse:** Død. VoiceFeminizationExerciseService refereres kun som parameter i ResonanceModuleDocumentation.GetResonanceExercises (linje 137), som selv aldri kalles. Ingen prod-konsument av GetAllEnhancedExercises/GetExercisesByGoal osv. Øvelsesinnhold dekkes i prod av ExerciseTextService. Bekreftet mojibake/feil-encoding i filen (f.eks. 'stemmetreningsÃ¸velser', '3Ã—/uke' — latin1-dekodet UTF-8). Høy maintenanceCost: 533 linjer øvelsesdata som forvirrer (ser ut som kjernedata) men er død og korrupt-encodet.
- **Detaljer:** Foreldet/erstattet-av: ExerciseTextService leverer øvelsesinnhold i prod. Berørte filer: VoiceFeminizationExerciseService.cs (inkl. EnhancedExercise-modellen og ukeplan-typene definert i samme fil) og ResonanceModuleDocumentation.cs. Ingen dedikert testfil. Trygg slettesti: ResonanceModuleDocumentation refererer VoiceFeminizationExerciseService/EnhancedExercise — slett begge filene samtidig. Verifiser at EnhancedExercise ikke brukes andre steder (grep viser kun innen VoiceFeminizationExerciseService.cs) før sletting.

#### VoiceMetricsCalculator
- **Fil:** `FemVoiceStudio/Audio/VoiceMetricsCalculator.cs`
- **Begrunnelse:** Null `new VoiceMetricsCalculator` i produksjon eller tester. Refererer kun internt PitchDetection/FormantDetection. Produksjon bruker LiveMetricsService i MainViewModel i stedet — dette er en foreldet duplikat. 465 linjer død kode gir betydelig forvirring/falske treff.
- **Detaljer:** Hvorfor foreldet: erstattet av LiveMetricsService. Trygg slettesti: slett filen; ingen eksterne referanser. Berørte filer: kun VoiceMetricsCalculator.cs. Ingen tester. Kompilerer uendret etter sletting.

#### AsyncAudioPipeline
- **Fil:** `FemVoiceStudio/Audio/AsyncAudioPipeline.cs`
- **Begrunnelse:** Ingen produksjonskode gjør `new AsyncAudioPipeline`. News selv opp AdaptivePitchDetector (:31) og VoiceActivityDetector (:32), så hele under-grafen er død fordi roten aldri instansieres. Aktiv async-capture går via AudioCaptureService-stien.
- **Detaljer:** Hvorfor foreldet: aldri instansiert; capture går via AudioCaptureService. Trygg slettesti: slett filen først (den er eneste eksterne konsument av AdaptivePitchDetector), deretter kan AdaptivePitchDetector slettes. Berørte filer: AsyncAudioPipeline.cs. Ingen tester. MERK: må slettes FØR/sammen med AdaptivePitchDetector og VoiceActivityDetector som klynge.

#### VoiceActivityDetector (inkl. avhengighet av RollingStatistics)
- **Fil:** `FemVoiceStudio/Audio/VoiceActivityDetector.cs`
- **Begrunnelse:** Begge verter er døde: AsyncAudioPipeline:32 (aldri instansiert) og AnalysisSubsystem:31 (kun reachable via AddFemVoiceStudio() som ALDRI kalles). Verifisert: aktive tjenester PitchDetectionService/AudioAnalyzerService/AudioAnalysisEngine bruker den IKKE. Voiced/unvoiced i produksjon gjøres av PitchDetectionService.IsVoiced.
- **Detaljer:** Hvorfor foreldet: begge verter døde; erstattet av PitchDetectionService.IsVoiced. Avhenger av RollingStatistics (i AdaptivePitchDetector.cs) — slett i klynge. Trygg slettesti: må slettes sammen med AsyncAudioPipeline og AnalysisSubsystem (eierne). Berørte filer: VoiceActivityDetector.cs. Ingen tester.

#### ViewModelBase / SubsystemViewModelBase
- **Fil:** `FemVoiceStudio/ViewModels/ViewModelBase.cs`
- **Begrunnelse:** Grep paa ': ViewModelBase' og ': SubsystemViewModelBase' gir KUN definisjonene selv - ingen VM arver dem. SubsystemViewModelBase er kjernen i den forlatte gen-2-arkitekturen (AddFemVoiceStudio -> I*Subsystem -> SubsystemViewModelBase -> VMs) som aldri ble wiret; appen bruker en helt annen DI-sti (App.ConfigureServices). Den ene grunnen til at den kompilerer er at den drar inn HELE Subsystems-namespacet (5 using-linjer) og holder dermed de doede subsystem-interfacene 'i bruk'. Hoy maintenanceCost: skaper falske grep-treff naar man leter etter den ekte ViewModel-baseklassen og maskerer at subsystem-interfacene er doede.
- **Detaljer:** DELETE: Foreldet - tilhoerer den aldri-aktiverte gen-2 subsystem-arkitekturen. Erstattet-av: ObservableObject fra CommunityToolkit.Mvvm som alle aktive VMer (SmartCoachViewModel, ExerciseDetailViewModel, CalendarViewModel osv.) faktisk bruker. Trygg slettesti: slett hele ViewModelBase.cs. Ingen aktiv kode arver klassene. Fjerner samtidig en av de fem aktive konsumentene av Subsystems-namespacet (de fire andre er Infra/DependencyInjection.cs + selve Subsystems-mappen). Beroerte filer: kun ViewModelBase.cs - ingen tester refererer den.

#### DataSubsystem (backup/eksport/RestoreBackup-bug)
- **Fil:** `FemVoiceStudio/Subsystems/Data/DataSubsystem.cs (+ IDataSubsystem.cs)`
- **Begrunnelse:** Aldri instansiert i prod. Eneste konsument er IDataSubsystem injisert i SubsystemViewModelBase (doed) + Infra/DependencyInjection.cs (doed) + intern bruk fra andre doede subsystemer (ProgressionSubsystem, SmartCoachSubsystem). RestoreBackupAsync har en bekreftet bug: kaller _databaseService.Dispose() TO ganger og reinitialiserer aldri (kommentaren sier 'Reinitialize' men kaller bare Dispose paa nytt) - men siden hele subsystemet er doedt er det DELETE, ikke FIX. Backup/eksport-API-et (CreateBackupAsync/ExportDataAsync/ImportDataAsync/RestoreBackupAsync) har null prod-kallere.
- **Detaljer:** DELETE: Foreldet - tilhoerer den doede subsystem-arkitekturen; backup/eksport er aldri eksponert i UI. Trygg slettesti: inngaar i samme rydderunde som hele Subsystems-mappen (se Subsystems-raden). DataSubsystem.cs + IDataSubsystem.cs slettes sammen med resten av Subsystems + Infra/DependencyInjection.cs + ViewModelBase.cs. Merk avhengighet: DataSubsystem.cs importerer Subsystems.SmartCoach (VoiceProfile-varianten der) - maa slettes samlet med SmartCoach-subsystemet. Ingen dedikert test.

#### .old/.old2-filer i hele treet
- **Fil:** `FemVoiceStudio/{App.xaml.cs.old, Data/DatabaseService.cs.old, Data/DatabaseService.cs.old2, Data/IDatabaseService.cs.old, Views/ExerciseWindow.xaml(.c`
- **Begrunnelse:** 14 .old/.old2-filer funnet (utenfor obj/bin). De kompileres ikke (.old-endelsen plukkes ikke opp av default Compile-glob - bekreftet ingen eksplisitt referanse i csproj), men ligger ved siden av sine aktive motparter (App.xaml.cs.old vs App.xaml.cs osv.). Hoy maintenanceCost: de gir falske grep-treff (man finner gammel kode naar man soeker etter symbolnavn) og skaper tvil om hvilken versjon som er gjeldende.
- **Detaljer:** DELETE: Foreldede sikkerhetskopier. Trygg slettesti: slett alle 14 filene - de kompileres ikke saa null kompileringskonsekvens. Liste: App.xaml.cs.old, Data/DatabaseService.cs.old, Data/DatabaseService.cs.old2, Data/IDatabaseService.cs.old, Views/ExerciseWindow.xaml.old, Views/ExerciseWindow.xaml.old2, Views/ExerciseWindow.xaml.cs.old, Converters/SeverityToBrushConverter.cs.old, Converters/ProgressToPercentConverter.cs.old, Models/ExerciseTargetProfile.cs.old, Models/ExerciseDefinition.cs.old, ViewModels/SmartCoachViewModel.cs.old, ViewModels/MainViewModel.cs.old, Services/ExerciseIntelligenceCoordinator.cs.old. Ingen tester.

#### VoiceHealthService (sesjons-/pause-timer)
- **Fil:** `FemVoiceStudio/Services/VoiceHealthService.cs`
- **Begrunnelse:** Aldri instansiert: 0 produksjonskall (grep finner kun selve filen). Ikke DI-registrert, ingen 'new VoiceHealthService(...)'. Mangler StartSession() + periodisk RegisterSpeaking()/RegisterSilence() fra lydloopen og abonnement paa BreakRequired/SessionEnded. Sesjons-/pausetimer-funksjonen dekkes naa av den aktive subjektiv-rapport-kjeden (oktslutt) + ProgressionSafetyGate. Hoy maintenanceCost: navnelikheten med det slettede VoiceHealthMonitor og det aktive VocalHealthSupervisor skaper forvirring og falske grep-treff. Bekreftet DEAD i Dead Systems Integration Report.
- **Detaljer:** FORELDET/erstattet: pause-/sesjonsstyringen leveres naa av subjektiv selvrapport ved oktslutt (PauseRecommended-events) og den aktive helse-stien. TRYGG SLETTESTI: ingen live compile-avhengighet (SessionWarningEventArgs deles ogsaa av det dode RestProtocolService, ikke av live kode). BEROERTE FILER: FemVoiceStudio/Services/VoiceHealthService.cs. Ingen tester refererer den (grep i FemVoiceStudio.Tests = 0). Kan slettes direkte; resten kompilerer uendret.

#### TrendAlertService
- **Fil:** `FemVoiceStudio/Services/TrendAlertService.cs`
- **Begrunnelse:** Aldri instansiert: 0 produksjonskall, ikke DI-registrert, ingen 'new TrendAlertService(...)'. Mangler RunSafetyCheck(pitch, rms, userId) ved oktslutt og handtering av SafetyCheckResult. Klinisk funksjon (pitch >280Hz maalsone-reduksjon, intensitetsterskler, helse-decline) overlapper med den aktive ProgressionSafetyGate (leser persistert helse-event-historikk) + VocalHealthSupervisor + subjektiv rapport. Hoy maintenanceCost pga forvekslingsfare med det dode TrendAnalysisService og navneslektskap med fjernede systemer. Bekreftet DEAD i rapporten.
- **Detaljer:** FORELDET/erstattet: sikkerhetssjekk ved oktslutt leveres naa av ProgressionSafetyGate (SessionAnalyticsStore-historikk) + VocalHealthSupervisor. TRYGG SLETTESTI: avhenger kun av DatabaseService/ILocalizationService (innkommende); ingen live kode konsumerer SafetyCheckResult/HealthTrendRecommendation/PitchAlert (grep = 0 utenfor egen fil). BEROERTE FILER: FemVoiceStudio/Services/TrendAlertService.cs. Ingen tester. Kan slettes direkte.

#### StrainMonitor (VoiceHealthModule)
- **Fil:** `FemVoiceStudio/Services/VoiceHealthModule/StrainMonitor.cs`
- **Begrunnelse:** Aldri instansiert: 0 produksjonskall (grep = kun selvreferanse), ikke DI-registrert. Mangler new StrainMonitor(...) + mating av VoiceMetrics per frame og konsum av StrainAction-resultat. Strain-deteksjon (jitter/shimmer/HNR-terskler, eskalering) dekkes naa funksjonelt av den aktive VocalHealthSupervisor. Forutsetter dessuten reelle jitter/shimmer-verdier som kun ville kommet fra det like dode VoiceStrainDetector (hardkodet 0). Hoy maintenanceCost: duplikat strain-domene gir falske grep-treff. Bekreftet DEAD i rapporten.
- **Detaljer:** DUPLIKAT/erstattet: VocalHealthSupervisor er den aktive strain-/fatigue-detektoren. ORIGINAL vs ERSTATNING: StrainMonitor (dod) -> VocalHealthSupervisor (aktiv). TRYGG SLETTESTI: ingen live konsument av StrainAction/StrainEventArgs (grep = 0 utenfor dode filer). BEROERTE FILER: FemVoiceStudio/Services/VoiceHealthModule/StrainMonitor.cs. Ingen tester. Slettes sammen med RestProtocolService; fjern tom VoiceHealthModule-mappe etterpaa.

#### VoiceStrainDetector
- **Fil:** `FemVoiceStudio/Audio/VoiceStrainDetector.cs`
- **Begrunnelse:** Effektivt DEAD: konstrueres kun i to dode verter — RealtimeAnalysisEngine (0 instansieringer i hele kodebasen) og AnalysisSubsystem (kun registrert i Infra/DependencyInjection.cs som ALDRI kalles fra App.xaml.cs; AnalyzeAsync har 0 kallere, SubsystemViewModelBase har 0 subklasser). I tillegg returnerer Analyze alltid JitterValue=0 og ShimmerValue=0 (hardkodet, :40-41), saa selv om den kjorte ville den ikke produsere klinisk gyldige jitter/shimmer. Bekreftet DEAD i rapporten.
- **Detaljer:** FORELDET/uten klinisk verdi: hardkodede jitter/shimmer=0 gjor utdataene verdilose; aktiv strain dekkes av VocalHealthSupervisor. TRYGG SLETTESTI: live kode bruker den ikke. Berorte (dode) verter: FemVoiceStudio/Audio/RealtimeAnalysisEngine.cs og FemVoiceStudio/Subsystems/Analysis/AnalysisSubsystem.cs refererer StrainAnalysis/StrainLevel — disse hostene er selv dode og bor vurderes for sletting i samme rydderunde (utenfor mitt strikte scope). BEROERTE FILER: FemVoiceStudio/Audio/VoiceStrainDetector.cs (+ de to dode hostene + Infra/DependencyInjection-registreringen for aa fjerne dangling typer). Ingen tester refererer VoiceStrainDetector.

#### AdaptiveDifficultyService
- **Fil:** `FemVoiceStudio/Services/AdaptiveDifficultyService.cs`
- **Begrunnelse:** Null prod-referanser (ingen new AdaptiveDifficultyService, ingen DI-registrering). Evaluate(...)/RecommendExercise(...) gir DifficultyRecommendation/ExerciseRecommendation som ingen konsumerer. Funksjonen (score-basert opp/ned-rykk) er overlappet av ProgressionService (autoritativ) og ProgressionOrchestrator (adaptiv profil). Foreldet duplikat. Bekreftet DEAD i Dead Systems Report.
- **Detaljer:** DELETE. Berorte filer: Services/AdaptiveDifficultyService.cs (inkl. typene SessionPerformance, DifficultyRecommendation, ExerciseRecommendation definert i samme fil). Ingen testfil refererer den. Trygg slettesti: verifiser at SessionPerformance/DifficultyRecommendation/ExerciseRecommendation ikke brukes andre steder (grep gir 0 utenfor filen) - slett hele filen. Vurder a fjerne tilhorende ubrukte RESX-nokler (AdaptiveDifficulty_*).

#### ProgressionRateCalculator
- **Fil:** `FemVoiceStudio/Services/SmartCoachModule/ProgressionRateCalculator.cs`
- **Begrunnelse:** Null prod-referanser. Inneholder bekreftet bug pa linje 77: '"medium".Equals(recommendation.Confidence = "medium");' - meningslost (setter felt, kaster Equals-resultat). Beregner Hz-progresjonsrater som ingen leser. Funksjonen overlappes av ProgressionOrchestrator/ComplexityEngine. Foreldet duplikat med kjent feil.
- **Detaljer:** DELETE. Berorte filer: Services/SmartCoachModule/ProgressionRateCalculator.cs (inkl. typene ProgressionRecommendation/TrainingFrequencyRecommendation/ProgressionSummary i samme fil). Ingen testfil. Trygg slettesti: grep bekrefter 0 eksterne referanser til klassen og dens supporting types - slett hele filen. Vurder ubrukte RESX-nokler (ProgressionRate_*).

#### WeeklyPlannerEngine
- **Fil:** `FemVoiceStudio/Services/Progression/WeeklyPlannerEngine.cs`
- **Begrunnelse:** Kun registrert i Infra/DependencyInjection.cs:29 (AddFemVoiceStudio, aldri kalt). Aldri instansiert i prod. Genererer ukeplaner (WeeklySchedule) som ingen UI viser. Konsumerer dode modeller (WeeklySchedule/TrainingLoad/UserProgressionProfile). Ingen klinisk konsument. DEAD.
- **Detaljer:** DELETE. Berorte filer: Services/Progression/WeeklyPlannerEngine.cs. Trygg slettesti: fjern registrering Infra/DependencyInjection.cs:29; slett filen; folg opp med sletting av Models/WeeklySchedule.cs + Models/TrainingLoad.cs (kun konsumert av denne motoren). Ingen testfil. Holder en ComplexityEngine?-referanse, men kun valgfri - ComplexityEngine beholdes.

#### Models: PeriodizationModels / TrainingLoad / WeeklySchedule / UserProgressionProfile
- **Fil:** `FemVoiceStudio/Models/PeriodizationModels.cs, FemVoiceStudio/Models/TrainingLoad.cs, FemVoiceStudio/Models/WeeklySchedule.cs, FemVoiceStudio/Models/Us`
- **Begrunnelse:** Selvstendig dod modell-oy. PeriodizationModels: kun PeriodizationService (dod). TrainingLoad: kun WeeklySchedule.cs. WeeklySchedule: kun WeeklyPlannerEngine (dod). UserProgressionProfile: kun ProgressionEngine/WeeklyPlannerEngine (dode). Ingen live konsument. Materialiseres aldri. DEAD.
- **Detaljer:** DELETE. Berorte filer: Models/PeriodizationModels.cs, Models/TrainingLoad.cs, Models/WeeklySchedule.cs, Models/UserProgressionProfile.cs. Trygg slettesti: ma slettes SAMMEN med sine motorer (PeriodizationService, ProgressionEngine, WeeklyPlannerEngine, ProgressionConfig). Rekkefolge: fjern motorene/config forst, sa modellene. NB: ProgressionDecision-enum (ProgressionEnums.cs) og ProgressionMode/PeriodizationCycle/ProgressionMilestone refereres ogsa av UserProgressionProfile - men disse enum-/Milestone-definisjonene ligger i EGNE filer (ProgressionEnums.cs, ProgressionSessionData.cs, ProgressionMilestone.cs) som ikke er i denne slette-batchen; verifiser at de blir genuint ubrukte for evt. videre opprydding. WeeklySchedule.cs inneholder ogsa ProgressionDecision-baerende TargetAdjustment/decision-typer - bekreft 0 live bruk for sletting.

#### SmartCoachExerciseAdapter
- **Fil:** `FemVoiceStudio/Services/SmartCoachExerciseAdapter.cs`
- **Begrunnelse:** Død. De eneste vertene som instansierer den — LiveFeedbackViewModel (linje 72/93) og ExerciseSummaryViewModel (linje 84) — instansieres selv aldri (LiveFeedbackView/ExerciseSummaryView hostes ikke i noen XAML). Default-ctor gir tom adapter, og CalculateUserLevel returnerer alltid Nybegynner fordi GetRecentSessions er en tom stub (returnerer alltid tom liste, linje 372-391). Erstattet i prod av ExerciseIntelligenceCoordinator + AdaptiveComfortZoneService. Høy maintenanceCost: gir falske grep-treff for nivåberegning/adaptasjon.
- **Detaljer:** Foreldet/erstattet-av: ExerciseIntelligenceCoordinator + AdaptiveComfortZoneService dekker live-feedback og adaptiv målsone i prod; adapterens GetRecentSessions er en ikke-implementert stub. Berørte filer: SmartCoachExerciseAdapter.cs (slett). Avhengig av sletting av LiveFeedbackViewModel og ExerciseSummaryViewModel (begge døde verter) — disse refererer adapteren. Ingen dedikert testfil for adapteren. Trygg slettesti: slett samtidig med de døde view-modellene (se ExerciseFeedbackEngine-rad), ellers vil de slutte å kompilere.

#### FeedbackRuleEngine (CompositeEvaluator + 4 IRuleEvaluator)
- **Fil:** `FemVoiceStudio/Services/FeedbackRuleEngine/`
- **Begrunnelse:** Død. CompositeEvaluator instansieres aldri (ingen new CompositeEvaluator() / .Evaluate() utenfor mappen). IRuleEvaluator-implementasjonene (Pitch/Resonance/Intonation/Breathing) refereres kun av CompositeEvaluator (selv død) og av tester. BreathingRuleEvaluator brukes i FeedbackSignalPolicyTests.cs (linje 105/127) for å verifisere signal-cue-tekst — men ingen prod-konsument. Erstattet av ExerciseIntelligenceCoordinator/InlineCoachFeedbackMapper-pipelinen.
- **Detaljer:** Foreldet/erstattet-av: live coaching-hints kommer nå fra ExerciseIntelligenceCoordinator via InlineCoachFeedbackMapper. Berørte filer: hele FeedbackRuleEngine/-mappen (CompositeEvaluator.cs, IRuleEvaluator.cs, PitchRuleEvaluator.cs, ResonanceRuleEvaluator.cs, IntonationRuleEvaluator.cs, BreathingRuleEvaluator.cs); FemVoiceStudio.Tests/FeedbackSignalPolicyTests.cs bruker BreathingRuleEvaluator (linje 105/127) — disse to test-metodene må fjernes, men filen tester også FeedbackService (linje 14-82) som beholdes, så slett kun de to BreathingRuleEvaluator-testene, ikke hele filen. Trygg slettesti: fjern mappen + de to nevnte testmetodene; resten kompilerer.

#### CoachMessageGenerator / CoachMessageFormatter
- **Fil:** `FemVoiceStudio/Services/CoachMessageGenerator.cs`
- **Begrunnelse:** Død. CoachMessageGenerator instansieres KUN i enhetstest (FemVoiceStudio/Tests/CoachMessageGeneratorTests.cs — merk: ligger INNE i hovedprosjektet og kompileres med appen via implicit globbing). Ingen prod-VM/-tjeneste kaller GenerateMessage. CoachMessageFormatter har ingen kaller overhodet. SmartCoach-meldinger genereres i stedet direkte i SmartCoachEngine. Høy maintenanceCost fordi en intern testfil maskerer at klassen er død.
- **Detaljer:** Foreldet/erstattet-av: meldingstekst bygges i SmartCoachEngine; formatering/ruting skjer via mapper+pipeline. Berørte filer: CoachMessageGenerator.cs, CoachMessageFormatter.cs; FemVoiceStudio/Tests/CoachMessageGeneratorTests.cs (intern testfil i hovedprosjektet — MÅ slettes sammen med klassen ellers brytes kompilering av appen). Trygg slettesti: slett begge service-filene + den interne testfilen samtidig.

#### SpeechRateAnalyzer
- **Fil:** `FemVoiceStudio/Audio/SpeechRateAnalyzer.cs`
- **Begrunnelse:** Null referanser i hele kodebasen (verken produksjon eller tester). Forutsetter innmating av VAD/voiced-segmenter som heller ikke finnes på live-sti. Taletempo måles ikke i produksjon — funksjonen er ikke wiret noe sted.
- **Detaljer:** Hvorfor foreldet: aldri kalt; ingen aktiv talehastighets-feature. Ingen erstatning (funksjon eksisterer ikke i produksjon). Trygg slettesti: slett filen frittstående; ingen referanser. Berørte filer: kun SpeechRateAnalyzer.cs. Ingen tester.

#### LiveFeedbackView + LiveFeedbackViewModel
- **Fil:** `FemVoiceStudio/Views/LiveFeedbackView.xaml.cs (+ LiveFeedbackView.xaml, LiveFeedbackViewModel.cs)`
- **Begrunnelse:** LiveFeedbackView forekommer kun i sin egen XAML (x:Class) - ingen vindu/UserControl hoster den i XAML, og den new'es aldri i kode. LiveFeedbackViewModel instansieres aldri. Klinisk-funksjonen (sanntids feedback) er allerede dekket av ExerciseWindows inline-panel (jf. baseline). Denne VMen er ogsaa den eneste produksjonskonsumenten av ExerciseFeedbackEngine + SmartCoachExerciseAdapter, saa den holder en hel doed feedback-kjede kunstig 'levende' for grep.
- **Detaljer:** DELETE: Erstattet-av inline-panelet i ExerciseWindow.xaml.cs. Trygg slettesti: slett LiveFeedbackView.xaml + .xaml.cs + LiveFeedbackViewModel.cs samlet. Verifisert ingen XAML-host og ingen 'new LiveFeedbackView/ViewModel' i prod. Ingen tester refererer LiveFeedbackViewModel. NB: naar denne + ExerciseSummaryViewModel slettes, blir ExerciseFeedbackEngine + SmartCoachExerciseAdapter foreldreloese (utenfor mitt omraade, men boer revurderes).

#### ExerciseSummaryView + ExerciseSummaryViewModel
- **Fil:** `FemVoiceStudio/Views/ExerciseSummaryView.xaml.cs (+ ExerciseSummaryView.xaml, ExerciseSummaryViewModel.cs)`
- **Begrunnelse:** ExerciseSummaryView forekommer kun i egen XAML (x:Class); ingen host. ExerciseSummaryViewModel refereres kun internt fra sin egen code-behind (DataContext-cast). Instansieres aldri i prod. Oktoppsummering-funksjonen dekkes na av oktslutt-floyten (FemVoiceScoreEngine + subjektiv selvrapport-panel + SessionAnalyticsStore). Holder ExerciseFeedbackEngine.LoadFromEngine og SmartCoachExerciseAdapter kunstig i live.
- **Detaljer:** DELETE: Erstattet-av den aktiverte oktslutt-floyten. Trygg slettesti: slett ExerciseSummaryView.xaml + .xaml.cs + ExerciseSummaryViewModel.cs. Kommentar-referanse i Models/ExerciseEvaluationResult.cs:28 er kun en kommentar (ingen kodeavhengighet). Ingen tester. Beroerer ikke kompilering av andre filer.

#### PitchChartViewModel
- **Fil:** `FemVoiceStudio/Views/PitchChartViewModel.cs`
- **Begrunnelse:** Grep gir kun definisjonen (class + ctor) i PitchChartViewModel.cs - null eksterne referanser i .cs eller .xaml. IKKE samme som den AKTIVE PitchChartAxisRangeCalculator (Services/) som har egen test og brukes av pitch-grafen. Navnelikheten gir hoy maintenanceCost: lett aa forveksle den doede VMen med den aktive akse-kalkulatoren.
- **Detaljer:** DELETE: Foreldet/erstattet av den inline pitch-graf-implementasjonen + PitchChartAxisRangeCalculator. Trygg slettesti: slett PitchChartViewModel.cs alene. Ingen XAML binder til den, ingen tester. Ingen kompileringsavhengigheter.

#### SmartCoachDashboardView
- **Fil:** `FemVoiceStudio/Views/SmartCoachDashboardView.xaml.cs (+ SmartCoachDashboardView.xaml)`
- **Begrunnelse:** SmartCoachDashboardView forekommer kun i sin egen XAML (x:Class) - hostes aldri i noe vindu og new'es aldri. Den aktive SmartCoach-verten er SmartCoachDetailWindow (i SmartCoachDetailView.xaml.cs) som bruker den aktive SmartCoachViewModel. Hoy maintenanceCost pga. navnelikhet med den aktive SmartCoach-UIen.
- **Detaljer:** DELETE: Erstattet-av SmartCoachDetailView/SmartCoachDetailWindow. Trygg slettesti: slett SmartCoachDashboardView.xaml + .xaml.cs. Ingen XAML-host, ingen kodereferanse, ingen test. Ingen kompileringsavhengigheter.

#### VoiceProfileExtensions (VoiceProfile/ExerciseEffectiveness/DailyProgress)
- **Fil:** `FemVoiceStudio/Services/VoiceProfileExtensions.cs`
- **Begrunnelse:** Ingen prod-referanser. Den ENESTE konsumenten er in-exe-testfilen FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs (som selv er en del av den feilplasserte test-i-exe-floken). Definerer en konkurrerende 'VoiceProfile'-type forskjellig fra Subsystems.SmartCoach.VoiceProfile - to ulike VoiceProfile-er forsterker forvirring. Den aktive personaliseringen kjorer via SmartCoachEngine/baseline, ikke via denne.
- **Detaljer:** DELETE: Foreldet personaliserings-skisse uten prod-kobling. Trygg slettesti: slett Services/VoiceProfileExtensions.cs SAMTIDIG som in-exe-testen FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs (ellers brytes kompileringen av WPF-exe-prosjektet siden testen ligger inne i hovedprosjektet). Beroerte filer/tester: VoiceProfileExtensions.cs + FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs.

#### Migration-SQL 001_exercise_feedback_system.sql
- **Fil:** `FemVoiceStudio/Data/migrations/001_exercise_feedback_system.sql`
- **Begrunnelse:** Lastes/kjores aldri av noen kode - grep paa '.sql'/'migrations'/'ReadAllText'/'ExecuteSqlFile' viser at hele skjemaet bygges i C# (DatabaseService.InitializeDatabase + RunMigrations). Fila bruker dessuten konstruksjoner som ikke matcher den faktiske SQLite-skjema-byggingen og er en parallell 'sannhet' om databasen som aldri eksekveres. Hoy maintenanceCost: en utvikler kan tro dette er den autoritative migrasjonen og endre den i tro paa at det paavirker databasen.
- **Detaljer:** DELETE: Foreldet/aldri-kjort. Trygg slettesti: slett 001_exercise_feedback_system.sql (og evt. den tomme migrations/-mappen). Ingen kode laster fila, ingen test. Null kompileringskonsekvens (ikke en .cs).

#### RestProtocolService (VoiceHealthModule)
- **Fil:** `FemVoiceStudio/Services/VoiceHealthModule/RestProtocolService.cs`
- **Begrunnelse:** Aldri instansiert: 0 produksjonskall (grep = kun selvreferanse), ikke DI-registrert. Mangler new RestProtocolService().StartSession() + periodiske oppdateringer fra lydloopen og abonnement paa RestRequired/Lockout-events. Hvile-/lockout-protokoll har ingen runtime-vert. Konseptuelt overlapper med den aktive pause-/helse-stien (subjektiv rapport + VocalHealthSupervisor), men ingen del er wiret. Bekreftet DEAD i rapporten.
- **Detaljer:** FORELDET/aldri integrert: hvile-/lockout-logikken er ikke koblet til noen lydloop eller UI; pauseanbefaling haandteres naa av den aktive helse-stien. TRYGG SLETTESTI: definerer egne typer (SessionWarningEventArgs deles med dode VoiceHealthService — slett begge sammen eller behold typen lokalt). Ingen live kode importerer den. BEROERTE FILER: FemVoiceStudio/Services/VoiceHealthModule/RestProtocolService.cs. Ingen tester. Slettes sammen med StrainMonitor og evt. tom VoiceHealthModule-mappe.

#### TrendAnalysisService
- **Fil:** `FemVoiceStudio/Services/TrendAnalysisService.cs`
- **Begrunnelse:** DEAD: utelukkende statiske utility-metoder (CalculateMovingAverage, AnalyzeTrend, CalculateConsistency, AnalyzeWeeklyProgress, AnalyzePitchPattern, GenerateCalendarHeatmap) med 0 produksjonskall (grep utenfor egen fil = 0). Maa ikke forveksles med TrendAlertService. Trend-/aggregeringsbehov dekkes i praksis av SessionAnalyticsStore-aggregater og VocalHealthTrendEngine. Hoy maintenanceCost pga navneforveksling med TrendAlertService og generisk gjenbrukbar fasade som lokker til falsk 'kanskje brukt'. Bekreftet DEAD i rapporten.
- **Detaljer:** FORELDET/ubrukt utility: ingen kaller noen av metodene. TRYGG SLETTESTI: rene statiske metoder uten innkommende avhengigheter; ingen live kode importerer typene (TrendResult/WeeklyProgress/PitchPatternResult/CalendarDay grep = 0 utenfor egen fil). BEROERTE FILER: FemVoiceStudio/Services/TrendAnalysisService.cs. Ingen tester. Kan slettes direkte; resten kompilerer uendret.

#### AdaptiveTargetZoneService
- **Fil:** `FemVoiceStudio/Services/SmartCoachModule/AdaptiveTargetZoneService.cs`
- **Begrunnelse:** Død. Ingen konsument: grep finner ingen new AdaptiveTargetZoneService eller metodekall utenfor egen fil (treffene på 'AdaptiveTargetZone' gjelder en helt annen klasse — modellen AdaptiveTargetZone i SmartCoachModels.cs brukt av RealtimeAnalysisEngine, IKKE tjenesten). Erstattet av AdaptiveComfortZoneService (aktiv via MainViewModel:197). MERK: ProgressionRateCalculator i samme SmartCoachModule-mappe har OGSÅ null konsumenter (grep tomt) — sannsynlig samme skjebne, men utenfor mitt tildelte inventar.
- **Detaljer:** Foreldet/erstattet-av: AdaptiveComfortZoneService dekker adaptiv målsone i prod. Pass på navnekollisjon: behold modellklassen AdaptiveTargetZone (Models/SmartCoachModels.cs) — den brukes aktivt av Audio/RealtimeAnalysisEngine.cs; slett kun tjenesten Services/SmartCoachModule/AdaptiveTargetZoneService.cs. Ingen dedikert testfil. Trygg slettesti: slett tjeneste-filen; ingen produksjonskode refererer den, så resten kompilerer uendret.

#### MockAudioAnalysisEngine (inni AudioAnalysisEngine.cs)
- **Fil:** `FemVoiceStudio/Audio/AudioAnalysisEngine.cs`
- **Begrunnelse:** Definert på linje 1264 i AudioAnalysisEngine.cs, men null `new MockAudioAnalysisEngine` noe sted (heller ikke i tester). Død mock-klasse som lever inni en ELLERS AKTIV fil — derav sletting på klasse-nivå, ikke fil-nivå.
- **Detaljer:** Hvorfor foreldet: aldri instansiert; mock uten kallere. Trygg slettesti: fjern KUN klassedeklarasjonen MockAudioAnalysisEngine (fra linje 1264 til klassens slutt) inne i AudioAnalysisEngine.cs — IKKE slett filen (resten er aktiv). Berørte filer: AudioAnalysisEngine.cs (delvis). Ingen tester refererer mocken.

#### ExerciseListViewModel
- **Fil:** `FemVoiceStudio/ViewModels/ExerciseListViewModel.cs`
- **Begrunnelse:** ExerciseListViewModel refereres ikke fra noen aktiv view eller kode (kun stale kommentarer i ExerciseDetailViewModel.cs:15/171). VIKTIG OPPDATERING AV BASELINE: RelayCommand ligger IKKE lenger i denne fila - den er flyttet til egen fil ViewModels/RelayCommand.cs (eneste 'class RelayCommand'-definisjon i hele repoet). Fila inneholder derfor KUN den doede ExerciseListViewModel-klassen. Slettestien trenger derfor IKKE bevare RelayCommand lenger.
- **Detaljer:** DELETE: Foreldet ovelseslistevisning som aldri hostes. Trygg slettesti: slett HELE ExerciseListViewModel.cs (i motsetning til baseline trenger man ikke ekstrahere RelayCommand - den bor allerede i RelayCommand.cs). Bonus-opprydding (ikke noedvendig for kompilering): de to stale kommentarene i ExerciseDetailViewModel.cs:15 og :171 som feilaktig sier 'RelayCommand defined in ExerciseListViewModel.cs (shared)' - de boer rettes til RelayCommand.cs. Ingen tester refererer ExerciseListViewModel (ExerciseDetailViewModelTests.cs noevner den ikke).

#### IComfortZoneRepository
- **Fil:** `FemVoiceStudio/Data/IComfortZoneRepository.cs`
- **Begrunnelse:** Grep gir KUN interface-definisjonen (linje 13). Ingen implementerende klasse, ingen DI-registrering, ingen konsument. ComfortZoneController bruker IUserRepository/IScoreRepository (ikke IComfortZoneRepository). Rent forvirrende artefakt: ser ut som komfortsone-persistens-kontrakten, men er aldri koblet til noe.
- **Detaljer:** DELETE: Uten klinisk verdi (tom kontrakt). Trygg slettesti: slett IComfortZoneRepository.cs alene. Ingen implementasjon, ingen injeksjon, ingen test refererer den - null kompileringskonsekvens for resten.

#### SmartCoachBaselines (plural) orphan-tabell
- **Fil:** `FemVoiceStudio/Data/DatabaseService.cs (CREATE TABLE l.260)`
- **Begrunnelse:** Tabellen SmartCoachBaselines (flertall) opprettes paa l.260 men spores ALDRI i noen query - alle Save/Get-metoder (SaveSmartCoachBaseline l.1653, GetSmartCoachBaseline l.1701) og indeksen (l.861) bruker entalls-varianten SmartCoachBaseline (l.395). Plural-tabellen er en ren orphan som bare staar og tar plass i skjemaet.
- **Detaljer:** DELETE: Duplikat-skjema (plural vs entall). Trygg slettesti: fjern CREATE TABLE IF NOT EXISTS SmartCoachBaselines-blokken (rundt l.260) i DatabaseService.cs. Pass paa at man fjerner FLERTALLS-blokken og IKKE entalls-tabellen (l.395) som faktisk brukes. Ingen kode leser/skriver flertallsvarianten, saa ingen runtime-konsekvens. Eksisterende databaser beholder den tomme tabellen, men det er harmloest.

#### AudioAnalysisEngine_new.cs (tom stub)
- **Fil:** `FemVoiceStudio/Audio/AudioAnalysisEngine_new.cs`
- **Begrunnelse:** Filen inneholder kun `using System;` (1 linje), ingen type. Etterlatenskap fra refaktorering. Forvirrer (navnekollisjon-følelse mot AudioAnalysisEngine.cs).
- **Detaljer:** Hvorfor foreldet: tom stub. Trygg slettesti: slett filen direkte; ingen typer, ingen referanser, kompilerer uendret. Berørte filer: kun AudioAnalysisEngine_new.cs.

#### part2.cs (tomt namespace)
- **Fil:** `FemVoiceStudio/Audio/part2.cs`
- **Begrunnelse:** Inneholder kun usings + tomt `namespace FemVoiceStudio.Audio {}` (10 linjer, ingen typer). Refaktorerings-etterlatenskap.
- **Detaljer:** Hvorfor foreldet: tomt namespace uten innhold. Trygg slettesti: slett filen direkte; ingen typer/referanser. Berørte filer: kun part2.cs.

#### generate_comfort.py (stub)
- **Fil:** `FemVoiceStudio/Services/generate_comfort.py`
- **Begrunnelse:** Tom Python-stub (0 linjer, 23 bytes) i et C#/WPF-prosjekt. Ingen kobling til byggesystem eller runtime. Passer ikke arkitekturen (Python i WPF-Services-mappe).
- **Detaljer:** DELETE: Passer ikke arkitekturen / null innhold. Trygg slettesti: slett generate_comfort.py. Ikke en kompileringsenhet, ingen referanse - null konsekvens.


---

## Seksjon 8 – Repository Health Score (post-aktivering)

| Område | Score | Kommentar |
|---|---|---|
| Health Layer | **8/10** | Hele den aktive kjeden (supervisor→recorder→analytics→gates) kjører; 5 døde parallellsystemer venter på sletting |
| Progression Layer | **8/10** | Full adaptiv kjede aktiv (orchestrator+overrides+mastery+gates+complexity); 10+ døde motorer venter på sletting |
| Analytics Layer | **9/10** | Øvelses- OG sesjonsnivå skrives og leses; kun TrendAnalysisService død |
| Coaching Layer | **7/10** | Inline + helse + hydrering + progresjon når brukeren; SmartCoach strain-analyse blokkert av IsRead-buggen (FIX #1) |
| Runtime Coverage | **8/10** | Av flatene som beholdes (KEEP+FIX = 43) er 35 fullt aktive |
| Technical Debt | **4/10** | 47 slettekandidater + 14 .old-filer + in-exe-testmappe + xunit i produksjons-exe — lav score til ryddesjauen er tatt |

---

## Seksjon 9 – Final Classification Table

| System | Klassifisering |
|---|---|
| .old/.old2-filer i hele treet | DELETE |
| AdaptiveDifficultyService | DELETE |
| AdaptivePitchDetector (inkl. RollingStatistics) | DELETE |
| AdaptiveTargetZoneService | DELETE |
| AnalysisSubsystem (implementasjonsklassen, IKKE typene i IAnalysisSubs | DELETE |
| AsyncAudioPipeline | DELETE |
| AudioAnalysisEngine_new.cs (tom stub) | DELETE |
| CoachMessageGenerator / CoachMessageFormatter | DELETE |
| DataSubsystem (backup/eksport/RestoreBackup-bug) | DELETE |
| ExerciseFeedbackEngine | DELETE |
| ExerciseListViewModel | DELETE |
| ExerciseSummaryView + ExerciseSummaryViewModel | DELETE |
| FeedbackRuleEngine (CompositeEvaluator + 4 IRuleEvaluator) | DELETE |
| GamificationService | DELETE |
| IComfortZoneRepository | DELETE |
| In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert inn i WPF-exe) | DELETE |
| Infra/DependencyInjection.cs (AddFemVoiceStudio + extensions + Analysi | DELETE |
| LiveFeedbackView + LiveFeedbackViewModel | DELETE |
| Migration-SQL 001_exercise_feedback_system.sql | DELETE |
| MockAudioAnalysisEngine (inni AudioAnalysisEngine.cs) | DELETE |
| Models: PeriodizationModels / TrainingLoad / WeeklySchedule / UserProg | DELETE |
| PeriodizationService | DELETE |
| PitchChartViewModel | DELETE |
| ProgressionConfig | DELETE |
| ProgressionDashboardViewModel (ViewModels-namespace) | DELETE |
| ProgressionEngine | DELETE |
| ProgressionRateCalculator | DELETE |
| RealtimeAnalysisEngine (inkl. RollingBuffer<T> og SignalSmoothing) | DELETE |
| RestProtocolService (VoiceHealthModule) | DELETE |
| SmartCoachBaselines (plural) orphan-tabell | DELETE |
| SmartCoachDashboardView | DELETE |
| SmartCoachExerciseAdapter | DELETE |
| SpeechRateAnalyzer | DELETE |
| StrainMonitor (VoiceHealthModule) | DELETE |
| TrendAlertService | DELETE |
| TrendAnalysisService | DELETE |
| ViewModelBase / SubsystemViewModelBase | DELETE |
| VoiceActivityDetector (inkl. avhengighet av RollingStatistics) | DELETE |
| VoiceFeminizationExerciseService + ResonanceModuleDocumentation | DELETE |
| VoiceHealthService (sesjons-/pause-timer) | DELETE |
| VoiceMetricsCalculator | DELETE |
| VoiceProfileExtensions (VoiceProfile/ExerciseEffectiveness/DailyProgre | DELETE |
| VoiceStrainDetector | DELETE |
| VoiceStrainDetector (inkl. StrainAnalysis/StrainLevel) | DELETE |
| WeeklyPlannerEngine | DELETE |
| generate_comfort.py (stub) | DELETE |
| part2.cs (tomt namespace) | DELETE |
| FeedbackService | FIX |
| InMemoryExerciseRepository (IUserRepository + IScoreRepository) | FIX |
| LevelClassificationSystem | FIX |
| SmartCoachEngine | FIX |
| SmartCoachFeedbackMapper | FIX |
| SmartCoachHealthMonitoring IsRead-kolonne (skjema/lese-mismatch) | FIX |
| Subsystems/ (Audio/Analysis/Data/Progression/SmartCoach + I*-interface | FIX |
| TrainingFrequencyService | FIX |
| AudioAnalysisEngine | KEEP |
| AudioAnalyzerService | KEEP |
| AudioCaptureService | KEEP |
| ClinicalSessionScore | KEEP |
| ComfortZoneController | KEEP |
| ComfortZoneState / ZoneConfiguration | KEEP |
| ComplexityEngine | KEEP |
| DirectionAnalyzer | KEEP |
| ExerciseSessionRecorder | KEEP |
| ExerciseTextService | KEEP |
| FeedbackConsistencyGuard | KEEP |
| FeedbackPipeline | KEEP |
| FormantDetectionService | KEEP |
| HydrationAdvisor (+HydrationAdvisorOptions) | KEEP |
| HydrationFeedbackMapper | KEEP |
| IExerciseProfileFactory -> ExerciseProfileFactory | KEEP |
| IExerciseProfileStore -> SqliteExerciseProfileStore | KEEP |
| InlineCoachFeedbackMapper | KEEP |
| LiveMetricsService | KEEP |
| MasteryEvaluator | KEEP |
| MicrophoneCalibrationService | KEEP |
| PitchDetectionService | KEEP |
| ProgressionFeedbackMapper | KEEP |
| ProgressionOrchestrator | KEEP |
| ProgressionSafetyGate | KEEP |
| ProgressionService | KEEP |
| ResonanceProxyEngine | KEEP |
| ResonansScoringService | KEEP |
| SessionAnalyticsStore | KEEP |
| SpectrogramResonanceMapper | KEEP |
| Subjektiv rapport-kjeden | KEEP |
| VocalHealthBaselineProvider | KEEP |
| VocalHealthFeedbackMapper | KEEP |
| VocalHealthSupervisor (+VocalHealthTrendEngine) | KEEP |
| VoiceGoalProfile-systemet | KEEP |

---

## Anbefalt rekkefølge (når dere er klare)

1. **FIX først, én er viktig:** IsRead-kolonnen i SmartCoachHealthMonitoring (+ parameterrekkefølge-
   mismatchen) — eneste FIX som blokkerer klinisk funksjonalitet (SmartCoach strain-analyse).
2. **Deretter ryddesjau i klynger** (hver klynge er selvinneholdt og kompilerer etter sletting):
   a) Tekstlige artefakter (.old-filer, stubber, orphan-SQL/-tabell) — null risiko.
   b) In-exe-testmappen + xunit-referansene ut av FemVoiceStudio.csproj.
   c) Audio-klyngen (RealtimeAnalysisEngine, AsyncAudioPipeline, AdaptivePitchDetector,
      VoiceActivityDetector, VoiceStrainDetector, VoiceMetricsCalculator, SpeechRateAnalyzer, Mock).
   d) Helse-klyngen (VoiceHealthService, TrendAlertService, VoiceHealthModule/).
   e) Progresjons-klyngen (ProgressionEngine, WeeklyPlanner, ProgressionConfig, Periodization*,
      AdaptiveDifficulty, GamificationService, ProgressionRateCalculator + døde modeller).
   f) UI-klyngen (LiveFeedbackView, ExerciseSummaryView, PitchChartViewModel, døde VM-er,
      SmartCoachDashboardView).
   g) **Til slutt** Subsystems/+Infra/ — krever først uttrekk av de levende typene
      (VoiceMetrics/ResonanceCategory fra IAnalysisSubsystem.cs til Models/).
3. FIX-resten etter behov (SmartCoach motivational/strain-wiring, SQLite-backing for
   InMemoryExerciseRepository, FeedbackService/TrainingFrequencyService-beslutning).
