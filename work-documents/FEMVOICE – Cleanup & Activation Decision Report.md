# FEMVOICE – Cleanup & Activation Decision Report

**Dato:** 2026-06-06 · **Mot:** HEAD `d33169e` · **Bygger på:** Dead Systems Classification Report
(90 systemer), Dead Systems Integration Report, FemVoice_Architecture_Documentation.md
**Ny analyse i denne rapporten:** MERGE-vurdering av 8 overlappende systempar (kodelest, ikke antatt)
og testavhengighets-/risikokart for alle slettekandidater (19 LAV / 9 MIDDELS / 3 HØY).

## Endelig disposisjon

| Beslutning | Antall |
|---|---|
| KEEP | 35 |
| ACTIVATE | 0 — *aktiveringspotensialet ble uttømt i aktiveringsrunden (`6b7f152..fcc4f70`)* |
| FIX | 7 |
| MERGE | 2 |
| DELETE | 46 |

---

## Seksjon 1 – Final System Decisions

| System | Klassifisering (audit) | Endelig beslutning |
|---|---|---|
| .old/.old2-filer i hele treet | DELETE | **DELETE** |
| AdaptiveDifficultyService | DELETE | **DELETE** |
| AdaptivePitchDetector (inkl. RollingStatistics) | DELETE | **DELETE** |
| AdaptiveTargetZoneService | DELETE | **DELETE** |
| AnalysisSubsystem (implementasjonsklassen, IKKE typene i IAnal | DELETE | **DELETE** |
| AsyncAudioPipeline | DELETE | **DELETE** |
| AudioAnalysisEngine_new.cs (tom stub) | DELETE | **DELETE** |
| CoachMessageGenerator / CoachMessageFormatter | DELETE | **DELETE** |
| DataSubsystem (backup/eksport/RestoreBackup-bug) | DELETE | **DELETE** |
| ExerciseFeedbackEngine | DELETE | **DELETE** |
| ExerciseListViewModel | DELETE | **DELETE** |
| ExerciseSummaryView + ExerciseSummaryViewModel | DELETE | **DELETE** |
| FeedbackRuleEngine (CompositeEvaluator + 4 IRuleEvaluator) | DELETE | **DELETE** |
| GamificationService | DELETE | **DELETE** |
| IComfortZoneRepository | DELETE | **DELETE** |
| In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert inn i  | DELETE | **DELETE** |
| LiveFeedbackView + LiveFeedbackViewModel | DELETE | **DELETE** |
| Migration-SQL 001_exercise_feedback_system.sql | DELETE | **DELETE** |
| MockAudioAnalysisEngine (inni AudioAnalysisEngine.cs) | DELETE | **DELETE** |
| Models: PeriodizationModels / TrainingLoad / WeeklySchedule /  | DELETE | **DELETE** |
| PeriodizationService | DELETE | **DELETE** |
| PitchChartViewModel | DELETE | **DELETE** |
| ProgressionConfig | DELETE | **DELETE** |
| ProgressionDashboardViewModel (ViewModels-namespace) | DELETE | **DELETE** |
| ProgressionEngine | DELETE | **DELETE** |
| ProgressionRateCalculator | DELETE | **DELETE** |
| RealtimeAnalysisEngine (inkl. RollingBuffer<T> og SignalSmooth | DELETE | **DELETE** |
| RestProtocolService (VoiceHealthModule) | DELETE | **DELETE** |
| SmartCoachBaselines (plural) orphan-tabell | DELETE | **DELETE** |
| SmartCoachDashboardView | DELETE | **DELETE** |
| SmartCoachExerciseAdapter | DELETE | **DELETE** |
| SpeechRateAnalyzer | DELETE | **DELETE** |
| StrainMonitor (VoiceHealthModule) | DELETE | **DELETE** |
| TrendAlertService | DELETE | **DELETE** |
| TrendAnalysisService | DELETE | **DELETE** |
| ViewModelBase / SubsystemViewModelBase | DELETE | **DELETE** |
| VoiceActivityDetector (inkl. avhengighet av RollingStatistics) | DELETE | **DELETE** |
| VoiceFeminizationExerciseService + ResonanceModuleDocumentatio | DELETE | **DELETE** |
| VoiceHealthService (sesjons-/pause-timer) | DELETE | **DELETE** |
| VoiceMetricsCalculator | DELETE | **DELETE** |
| VoiceProfileExtensions (VoiceProfile/ExerciseEffectiveness/Dai | DELETE | **DELETE** |
| VoiceStrainDetector | DELETE | **DELETE** |
| VoiceStrainDetector (inkl. StrainAnalysis/StrainLevel) | DELETE | **DELETE** |
| WeeklyPlannerEngine | DELETE | **DELETE** |
| generate_comfort.py (stub) | DELETE | **DELETE** |
| part2.cs (tomt namespace) | DELETE | **DELETE** |
| FeedbackService | FIX | **FIX** |
| InMemoryExerciseRepository (IUserRepository + IScoreRepository | FIX | **FIX** |
| SmartCoachEngine | FIX | **FIX** |
| SmartCoachFeedbackMapper | FIX | **FIX** |
| SmartCoachHealthMonitoring IsRead-kolonne (skjema/lese-mismatc | FIX | **FIX** |
| Subsystems/ (Audio/Analysis/Data/Progression/SmartCoach + I*-i | FIX | **FIX** |
| TrainingFrequencyService | FIX | **FIX** |
| Infra/DependencyInjection.cs (AddFemVoiceStudio + extensions + | DELETE | **MERGE** |
| LevelClassificationSystem | FIX | **MERGE** |
| AudioAnalysisEngine | KEEP | **KEEP** |
| AudioAnalyzerService | KEEP | **KEEP** |
| AudioCaptureService | KEEP | **KEEP** |
| ClinicalSessionScore | KEEP | **KEEP** |
| ComfortZoneController | KEEP | **KEEP** |
| ComfortZoneState / ZoneConfiguration | KEEP | **KEEP** |
| ComplexityEngine | KEEP | **KEEP** |
| DirectionAnalyzer | KEEP | **KEEP** |
| ExerciseSessionRecorder | KEEP | **KEEP** |
| ExerciseTextService | KEEP | **KEEP** |
| FeedbackConsistencyGuard | KEEP | **KEEP** |
| FeedbackPipeline | KEEP | **KEEP** |
| FormantDetectionService | KEEP | **KEEP** |
| HydrationAdvisor (+HydrationAdvisorOptions) | KEEP | **KEEP** |
| HydrationFeedbackMapper | KEEP | **KEEP** |
| IExerciseProfileFactory -> ExerciseProfileFactory | KEEP | **KEEP** |
| IExerciseProfileStore -> SqliteExerciseProfileStore | KEEP | **KEEP** |
| InlineCoachFeedbackMapper | KEEP | **KEEP** |
| LiveMetricsService | KEEP | **KEEP** |
| MasteryEvaluator | KEEP | **KEEP** |
| MicrophoneCalibrationService | KEEP | **KEEP** |
| PitchDetectionService | KEEP | **KEEP** |
| ProgressionFeedbackMapper | KEEP | **KEEP** |
| ProgressionOrchestrator | KEEP | **KEEP** |
| ProgressionSafetyGate | KEEP | **KEEP** |
| ProgressionService | KEEP | **KEEP** |
| ResonanceProxyEngine | KEEP | **KEEP** |
| ResonansScoringService | KEEP | **KEEP** |
| SessionAnalyticsStore | KEEP | **KEEP** |
| SpectrogramResonanceMapper | KEEP | **KEEP** |
| Subjektiv rapport-kjeden | KEEP | **KEEP** |
| VocalHealthBaselineProvider | KEEP | **KEEP** |
| VocalHealthFeedbackMapper | KEEP | **KEEP** |
| VocalHealthSupervisor (+VocalHealthTrendEngine) | KEEP | **KEEP** |
| VoiceGoalProfile-systemet | KEEP | **KEEP** |

---

## Seksjon 2 – Delete Candidates: testavhengighet og risiko

Risikonivå: **LAV** = null referanser utenfor egen død klynge · **MIDDELS** = tester/kommentarer/
csproj må ryddes i samme operasjon · **HØY** = levende kode deler typer — krever uttrekk først.

| Slettekandidat/klynge | Erstattet av | Testavhengighet | Risiko |
|---|---|---|---|
| AnalysisSubsystem (impl-klassen, Subsystems/Analysis/Analy | se klassifiseringsrapporten | INGEN. Ingen test refererer klassen direkte (grep 'AnalysisSubsystem' i tes | **HØY** |
| DataSubsystem (Subsystems/Data/DataSubsystem.cs + IDataSub | se klassifiseringsrapporten | INGEN. Grep 'DataSubsystem'/'IDataSubsystem' i begge testmapper = 0. | **HØY** |
| Infra/DependencyInjection.cs (AddFemVoiceStudio + extensio | se klassifiseringsrapporten | INGEN ekte. Grep 'AddFemVoiceStudio'/'FemVoiceStudio.Infra' i tester = 0. T | **HØY** |
| Progresjons-død-klyngen: ProgressionEngine.cs (+egen Progr | se klassifiseringsrapporten | MIDDELS: PeriodizationServiceTests-KLASSEN (ikke hele filen) i FemVoiceStud | **MIDDELS** |
| ExerciseFeedbackEngine (Services/ExerciseFeedbackEngine.cs | se klassifiseringsrapporten | MIDDELS/HØY: FemVoiceStudio.Tests/ExerciseFeedbackEngineTests.cs er DEDIKER | **MIDDELS** |
| UI-død-klyngen: LiveFeedbackView(.xaml+.xaml.cs) + LiveFee | se klassifiseringsrapporten | INGEN. Grep 'LiveFeedbackView(Model)'/'ExerciseSummaryView(Model)'/'SmartCo | **MIDDELS** |
| ExerciseListViewModel (ViewModels/ExerciseListViewModel.cs | se klassifiseringsrapporten | INGEN. Grep 'ExerciseListViewModel' i begge testmapper = 0 (ExerciseDetailV | **MIDDELS** |
| CoachMessageGenerator / CoachMessageFormatter (Services/Co | se klassifiseringsrapporten | MIDDELS/KRITISK: FemVoiceStudio/Tests/CoachMessageGeneratorTests.cs (IN-EXE | **MIDDELS** |
| FeedbackRuleEngine-mappen (Services/FeedbackRuleEngine/: C | se klassifiseringsrapporten | MIDDELS: FemVoiceStudio.Tests/FeedbackSignalPolicyTests.cs bruker 'new Brea | **MIDDELS** |
| VoiceProfileExtensions (Services/VoiceProfileExtensions.cs | se klassifiseringsrapporten | HØY-koblet (men trygg): FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs | **MIDDELS** |
| In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert in | se klassifiseringsrapporten | Dette ER testfiler — alle 5 slettes. Dekningsnyanse: (a) FemVoiceScoreTests | **MIDDELS** |
| ViewModelBase / SubsystemViewModelBase (ViewModels/ViewMod | se klassifiseringsrapporten | INGEN. Grep ': ViewModelBase'/'SubsystemViewModelBase' i begge testmapper = | **MIDDELS** |
| Audio-død-klyngen: RealtimeAnalysisEngine (+RollingBuffer< | se klassifiseringsrapporten | INGEN ekte testavhengighet. Grep i FemVoiceStudio.Tests/ og FemVoiceStudio/ | **LAV** |
| MockAudioAnalysisEngine (klasse inni Audio/AudioAnalysisEn | se klassifiseringsrapporten | INGEN. Grep 'MockAudioAnalysisEngine' i begge testmappene = 0 treff. | **LAV** |
| AudioAnalysisEngine_new.cs (tom stub) + part2.cs (tomt nam | se klassifiseringsrapporten | INGEN. Ingen typer, ingen referanser, ingen testtreff. | **LAV** |
| .old/.old2-filer i hele treet (14 filer: App.xaml.cs.old,  | se klassifiseringsrapporten | INGEN. .old-endelsen plukkes ikke opp av default Compile-glob; ingen test r | **LAV** |
| Migration-SQL Data/migrations/001_exercise_feedback_system | se klassifiseringsrapporten | INGEN. Ingen test laster eller refererer fila. | **LAV** |
| SmartCoachBaselines (plural) orphan-tabell i Data/Database | se klassifiseringsrapporten | INGEN. Grep 'SmartCoachBaselines' (plural) i tester = 0. (Tester bruker ent | **LAV** |
| IComfortZoneRepository (Data/IComfortZoneRepository.cs) | se klassifiseringsrapporten | INGEN. Grep 'IComfortZoneRepository' i begge testmapper = 0. | **LAV** |
| ProgressionDashboardViewModel (ViewModels-namespacet, View | se klassifiseringsrapporten | INGEN. Grep i begge testmapper = 0. | **LAV** |
| PitchChartViewModel (Views/PitchChartViewModel.cs) | se klassifiseringsrapporten | INGEN. Grep i begge testmapper = 0. (Den aktive PitchChartAxisRangeCalculat | **LAV** |
| SmartCoachDashboardView (Views/SmartCoachDashboardView.xam | se klassifiseringsrapporten | INGEN. Grep i begge testmapper = 0. | **LAV** |
| VoiceHealthService (Services/VoiceHealthService.cs) + Sess | se klassifiseringsrapporten | INGEN. Grep 'VoiceHealthService' / 'SessionWarningEventArgs' i begge testma | **LAV** |
| TrendAlertService (Services/TrendAlertService.cs) + Safety | se klassifiseringsrapporten | INGEN. Grep i begge testmapper = 0. | **LAV** |
| TrendAnalysisService (Services/TrendAnalysisService.cs) +  | se klassifiseringsrapporten | INGEN ekte. Grep 'TrendAnalysisService'/'TrendResult'/'PitchPatternResult'/ | **LAV** |
| VoiceHealthModule: StrainMonitor.cs + RestProtocolService. | se klassifiseringsrapporten | INGEN. Grep 'StrainMonitor'/'RestProtocolService'/'StrainAction'/'StrainEve | **LAV** |
| GamificationService (Services/GamificationService.cs) + Se | se klassifiseringsrapporten | INGEN. Grep i begge testmapper = 0. | **LAV** |
| AdaptiveDifficultyService (Services/AdaptiveDifficultyServ | se klassifiseringsrapporten | INGEN. Grep i begge testmapper = 0. | **LAV** |
| ProgressionRateCalculator (Services/SmartCoachModule/Progr | se klassifiseringsrapporten | INGEN. Grep i begge testmapper = 0. | **LAV** |
| AdaptiveTargetZoneService (Services/SmartCoachModule/Adapt | se klassifiseringsrapporten | INGEN. Grep 'AdaptiveTargetZoneService' i begge testmapper = 0. | **LAV** |
| VoiceFeminizationExerciseService + ResonanceModuleDocument | se klassifiseringsrapporten | INGEN. Grep 'VoiceFeminizationExerciseService'/'ResonanceModuleDocumentatio | **LAV** |

### Detaljer for MIDDELS/HØY-kandidatene

#### AnalysisSubsystem (impl-klassen, Subsystems/Analysis/AnalysisSubsystem.cs — IKKE typene i IAnalysisSubsystem.cs) — risiko HØY
- **Tester:** INGEN. Ingen test refererer klassen direkte (grep 'AnalysisSubsystem' i tester = 0). ADVARSEL: ExerciseFeedbackEngineTests.cs:4 har 'using FemVoiceStudio.Subsystems.Analysis' — men det er for TYPENE i IAnalysisSubsystem.cs (VoiceMetrics), ikke for impl-klassen AnalysisSubsystem.
- **Kompile-avhengigheter:** Død impl, men kompile-koblet til Subsystems-FIX: registreres i Infra/DependencyInjection.cs:41 + opprettes i AnalysisSubsystemFactory.Create() l.116 (begge i samme døde DI-fil). New-er VoiceActivityDetector+VoiceStrainDetector (AnalysisSubsystem.cs:31-32) — deler eierskap med Audio-død-klyngen. KRITISK: IAnalysisSubsystem.cs (samme mappe) definerer de AKTIVE typene VoiceMetrics/ResonanceCategory brukt av live kode (TrainingSession.cs:2, Feedback.cs:3, FeedbackService.cs:358-361, DatabaseService.cs:1351/1407, ResonanceWindow.xaml.cs:321, ExerciseFeedbackEngine.cs:7) — MÅ BEHOLDES.
- **Fjerningsnotat:** Slett KUN AnalysisSubsystem.cs (impl), ikke IAnalysisSubsystem.cs. Krever uttrekk/bevaring av de live typene i Subsystems-FIX-runden FØRST. Fjern AddTransient<IAnalysisSubsystem,AnalysisSubsystem> + AnalysisSubsystemFactory.Create() i Infra/DependencyInjection.cs. Slett i samme klynge som Audio-detektorene (felles eierskap til VoiceActivityDetector/VoiceStrainDetector).

#### DataSubsystem (Subsystems/Data/DataSubsystem.cs + IDataSubsystem.cs) — risiko HØY
- **Tester:** INGEN. Grep 'DataSubsystem'/'IDataSubsystem' i begge testmapper = 0.
- **Kompile-avhengigheter:** Død, men inngår i Subsystems-FIX-kompileringsnett: eneste konsumenter er IDataSubsystem injisert i SubsystemViewModelBase (død) + Infra/DependencyInjection.cs:32 + interne døde subsystemer (ProgressionSubsystem/SmartCoachSubsystem). DataSubsystem.cs importerer Subsystems.SmartCoach (VoiceProfile-varianten) — må slettes samlet med SmartCoach-subsystemet. RestoreBackupAsync har en Dispose-bug, men irrelevant siden klassen er død.
- **Fjerningsnotat:** Slett DataSubsystem.cs + IDataSubsystem.cs i SAMME rydderunde som hele Subsystems-mappen + Infra/DependencyInjection.cs + ViewModelBase.cs (de holder hverandre kompilerende). Kan ikke slettes isolert — derav HØY (krever koordinert Subsystems-uttrekk av live typer først).

#### Infra/DependencyInjection.cs (AddFemVoiceStudio + extensions + AnalysisSubsystemFactory) — risiko HØY
- **Tester:** INGEN ekte. Grep 'AddFemVoiceStudio'/'FemVoiceStudio.Infra' i tester = 0. Treffet 'DependencyInjection' i FemVoiceStudio.Tests/ReleaseReadinessSmokeTests.cs er FALSK POSITIV: det er 'Microsoft.Extensions.DependencyInjection' + testmetodenavn 'AppDependencyInjection_...'. Den testen bygger DI via reflektert App.ConfigureServices (l.43-46) med ValidateOnBuild=true — IKKE via Infra/DependencyInjection.cs. (Bekreftet: ingen av de 47 DELETE-kandidatene er registrert i App.ConfigureServices, så denne smoke-testen brytes ikke av noen sletting.)
- **Kompile-avhengigheter:** Død gen-2 DI-graf. Refererer alle de døde Subsystem-interfacene + impl + ProgressionEngine:28 + WeeklyPlannerEngine:29 + DataSubsystem:32 osv. Er én av to gjenværende konsumenter (med ViewModelBase) som holder Subsystem-interfacene kompilerende. AddFemVoiceStudio kalles ALDRI (prod bruker App.ConfigureServices).
- **Fjerningsnotat:** Slett hele Infra/DependencyInjection.cs i SAMME runde som Subsystems-mappen + ViewModelBase.cs (de tre holder hverandre kompilerende). Hvis Subsystems slettes separat først, kompilerer ikke DependencyInjection.cs (refererer DataSubsystem/AnalysisSubsystem osv.). Krever Subsystems-FIX (uttrekk av live VoiceMetrics/ResonanceCategory) gjennomført først — derav HØY.

#### Progresjons-død-klyngen: ProgressionEngine.cs (+egen ProgressionGateStatus), WeeklyPlannerEngine.cs, ProgressionConfig.cs (+ProgressionEvaluationResult), PeriodizationService.cs, + døde modeller Models/PeriodizationModels.cs, Models/TrainingLoad.cs, Models/WeeklySchedule.cs, Models/UserProgressionProfile.cs — risiko MIDDELS
- **Tester:** MIDDELS: PeriodizationServiceTests-KLASSEN (ikke hele filen) i FemVoiceStudio.Tests/SafetyLockTests.cs:333-slutt (felt _periodizationService l.336, ctor l.338, new PeriodizationService l.341) MÅ fjernes samtidig — ellers brytes kompileringen av testprosjektet. Resten av SafetyLockTests.cs (l.12 SafetyLockTests, l.213 ExternalSafetyBlockTests) tester aktiv SafetyLock og BEHOLDES. ProgressionEngine/WeeklyPlannerEngine/ProgressionConfig/PeriodizationModels/TrainingLoad/WeeklySchedule/UserProgressionProfile har 0 testtreff.
- **Kompile-avhengigheter:** INGEN levende prod-kode. ProgressionEngine/WeeklyPlannerEngine registreres KUN i den døde Infra/DependencyInjection.cs:28-29 (AddFemVoiceStudio kalles aldri). Modellene konsumeres kun av disse døde motorene + hverandre (TrainingLoad↔WeeklySchedule.cs, WeeklySchedule↔WeeklyPlannerEngine, UserProgressionProfile↔ProgressionEngine/WeeklyPlannerEngine). PASS PÅ delte enum/typer som IKKE skal slettes: SessionType (ScoreSnapshot.cs), ProgressionMode/ProgressionMilestone/PeriodizationCycle (egne filer), ProgressionDecision (ProgressionEnums.cs), ProgressionGateStatus (Models/ProgressionSessionData.cs har en KONKURRERENDE) — kun referansene FRA de døde filene forsvinner, ikke definisjonene. ProgressionEngine holder en ComplexityEngine-referanse (aktiv KEEP — behold, kun referansen fjernes).
- **Fjerningsnotat:** Slett rekkefølge: (1) fjern PeriodizationServiceTests-klassen i SafetyLockTests.cs; (2) fjern registreringene i Infra/DependencyInjection.cs:28-29 (helst slett hele DI-fila i Subsystems-runden); (3) slett motorene+config; (4) slett modellene til slutt. Verifiser at PeriodizationCycle/ProgressionEvaluationResult blir genuint ubrukt etterpå for evt. videre opprydding.

#### ExerciseFeedbackEngine (Services/ExerciseFeedbackEngine.cs) — risiko MIDDELS
- **Tester:** MIDDELS/HØY: FemVoiceStudio.Tests/ExerciseFeedbackEngineTests.cs er DEDIKERT testfil (16 'new ExerciseFeedbackEngine' over 13 testtilfeller) — hele filen må SLETTES ved sletting av engine. Den er også den ENESTE testfilen med 'using FemVoiceStudio.Subsystems.Analysis' (l.4), så den binder seg til Subsystems-FIX-arbeidet (men bruker Models.VoiceMetrics via alias l.10, ikke Subsystems-VoiceMetrics direkte i instansieringen).
- **Kompile-avhengigheter:** Død i prod, men kompile-koblet: ExerciseFeedbackEngine.cs:7 har 'using VoiceAnalysisMetrics = FemVoiceStudio.Subsystems.Analysis.VoiceMetrics' (avhenger av Subsystems.Analysis-typen som er FIX-host). Prod-konsumenter er KUN de døde Views/LiveFeedbackViewModel.cs:17/69/85 og Views/ExerciseSummaryViewModel.cs:140 (begge i UI-død-klyngen). Models/ExerciseEvaluationResult.cs:28 har kun en dokkommentar-referanse (ingen kodeavhengighet — behold).
- **Fjerningsnotat:** Slett ExerciseFeedbackEngine.cs + hele ExerciseFeedbackEngineTests.cs samtidig med UI-død-klyngen (LiveFeedbackViewModel/ExerciseSummaryViewModel). Siden engine er eneste prod-bruker av Subsystems.Analysis.VoiceMetrics, kan VoiceMetrics-varianten ryke i Subsystems-FIX-runden etter dette. Slett aldri ExerciseEvaluationResult.cs (kun kommentar).

#### UI-død-klyngen: LiveFeedbackView(.xaml+.xaml.cs) + LiveFeedbackViewModel.cs, ExerciseSummaryView(.xaml+.xaml.cs) + ExerciseSummaryViewModel.cs, SmartCoachExerciseAdapter.cs — risiko MIDDELS
- **Tester:** INGEN. Grep 'LiveFeedbackView(Model)'/'ExerciseSummaryView(Model)'/'SmartCoachExerciseAdapter' i begge testmapper = 0.
- **Kompile-avhengigheter:** Selvinneholdt død klynge. Views forekommer kun i egen XAML x:Class (LiveFeedbackView.xaml:1, ExerciseSummaryView.xaml:1) — bekreftet INGEN ekstern XAML-host. SmartCoachExerciseAdapter instansieres KUN av LiveFeedbackViewModel.cs:72/93 + ExerciseSummaryViewModel.cs:84 (samme klynge). VM-ene er eneste prod-konsumenter av ExerciseFeedbackEngine — slettes derfor i lås med den raden.
- **Fjerningsnotat:** Slett alle 6 view/VM-filer + SmartCoachExerciseAdapter.cs samlet med ExerciseFeedbackEngine-raden. Kommentar-referanse i Models/ExerciseEvaluationResult.cs:28 (kun kommentar) ignoreres. Klyngen kompilerer etter samlet sletting.

#### ExerciseListViewModel (ViewModels/ExerciseListViewModel.cs) — risiko MIDDELS
- **Tester:** INGEN. Grep 'ExerciseListViewModel' i begge testmapper = 0 (ExerciseDetailViewModelTests.cs nevner den ikke).
- **Kompile-avhengigheter:** INGEN ekte. Refereres ikke av aktiv view/kode. RelayCommand ligger nå i egen fil ViewModels/RelayCommand.cs (bekreftet eneste 'class RelayCommand'), så fila inneholder KUN den døde klassen. Eneste gjenværende treff er to STALE KOMMENTARER i ExerciseDetailViewModel.cs:15 og :171 ('RelayCommand defined in ExerciseListViewModel.cs (shared)') — feilaktige, ingen kodeavhengighet.
- **Fjerningsnotat:** Slett HELE ExerciseListViewModel.cs (trenger IKKE ekstrahere RelayCommand). Rydd samtidig de to stale kommentarene i ExerciseDetailViewModel.cs:15/171 (bør peke på RelayCommand.cs) — derav MIDDELS pga. samtidig kommentaropprydding, ikke kompileringsbrudd.

#### CoachMessageGenerator / CoachMessageFormatter (Services/CoachMessageGenerator.cs) — risiko MIDDELS
- **Tester:** MIDDELS/KRITISK: FemVoiceStudio/Tests/CoachMessageGeneratorTests.cs (IN-EXE testfil, l.13/17 new CoachMessageGenerator) MÅ slettes SAMTIDIG — den kompileres inn i WPF-exe-en, så hvis service-fila slettes uten testfila, BRYTES kompilering av selve hovedappen. Ingen test i FemVoiceStudio.Tests/.
- **Kompile-avhengigheter:** INGEN prod-kode. CoachMessageGenerator instansieres kun i nevnte in-exe-test. CoachMessageFormatter har 0 kallere. SmartCoach-meldinger genereres direkte i SmartCoachEngine.
- **Fjerningsnotat:** Slett CoachMessageGenerator.cs + CoachMessageFormatter (samme fil) + FemVoiceStudio/Tests/CoachMessageGeneratorTests.cs samtidig (inngår i in-exe-test-raden). Hovedappens kompilering avhenger av at testfila ryker samtidig.

#### FeedbackRuleEngine-mappen (Services/FeedbackRuleEngine/: CompositeEvaluator, IRuleEvaluator, Pitch/Resonance/Intonation/BreathingRuleEvaluator) — risiko MIDDELS
- **Tester:** MIDDELS: FemVoiceStudio.Tests/FeedbackSignalPolicyTests.cs bruker 'new BreathingRuleEvaluator()' på l.105 og l.127 (using FeedbackRuleEngine på l.3). Kun disse TO testmetodene må fjernes — resten av fila tester FeedbackService (l.14/35/60/82, FIX/beholdes via FeedbackService) og må BEHOLDES. Slett ikke hele filen.
- **Kompile-avhengigheter:** INGEN prod-kode. Grep 'FeedbackRuleEngine'/'CompositeEvaluator' i prod (utenfor mappen+tester) = 0. CompositeEvaluator instansieres aldri; IRuleEvaluator-impl refereres kun av CompositeEvaluator (død) + de to testmetodene. Erstattet av ExerciseIntelligenceCoordinator/InlineCoachFeedbackMapper.
- **Fjerningsnotat:** Slett hele FeedbackRuleEngine/-mappen + fjern de to BreathingRuleEvaluator-testmetodene (og 'using ...FeedbackRuleEngine' l.3) i FeedbackSignalPolicyTests.cs. Resten av testfila kompilerer.

#### VoiceProfileExtensions (Services/VoiceProfileExtensions.cs) + VoiceProfile/ExerciseEffectiveness/DailyProgress — risiko MIDDELS
- **Tester:** HØY-koblet (men trygg): FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs (IN-EXE testfil, l.13/15/19 new VoiceProfileExtensions) er ENESTE konsument og MÅ slettes samtidig — siden testen kompileres inn i WPF-exe-en, brytes hovedappens kompilering hvis service-fila slettes uten testfila.
- **Kompile-avhengigheter:** INGEN prod-kode. Eneste referanse i hele repoet er nevnte in-exe-test. Definerer en konkurrerende 'VoiceProfile'-type (forskjellig fra Subsystems.SmartCoach.VoiceProfile).
- **Fjerningsnotat:** Slett Services/VoiceProfileExtensions.cs + FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs samtidig (del av in-exe-test-raden). Ellers WPF-exe-kompileringsbrudd.

#### In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert inn i WPF-exe): CoachMessageGeneratorTests, DirectionAnalyzerTests, FemVoiceScoreTests, LevelClassificationSystemTests, VoiceProfileExtensionsTests + xunit/Test.Sdk PackageReferences i FemVoiceStudio.csproj — risiko MIDDELS
- **Tester:** Dette ER testfiler — alle 5 slettes. Dekningsnyanse: (a) FemVoiceScoreTests.cs er DUPLIKAT av FemVoiceStudio.Tests/FemVoiceScoreTests.cs (dekning bevart). (b) CoachMessageGeneratorTests + VoiceProfileExtensionsTests dør med sine DELETE-mål. (c) DirectionAnalyzerTests + LevelClassificationSystemTests tester LEVENDE klasser (DirectionAnalyzer=KEEP, LevelClassificationSystem=FIX) som IKKE har ekvivalent dekning i FemVoiceStudio.Tests/ (grep bekreftet: NONE) — sletting taper unik testdekning for live kode.
- **Kompile-avhengigheter:** Disse 5 filene kompileres INN i WinExe-en (default Compile-glob uten Remove + xunit/Microsoft.NET.Test.Sdk i FemVoiceStudio.csproj l.17/20/21). DirectionAnalyzer/FemVoiceScore/LevelClassificationSystem er live prod-klasser (KEEP/FIX) — å slette KUN testfilene er kompileringssikkert for hovedappen. CoachMessageGenerator+VoiceProfileExtensions må slettes i lås med sine produksjonsfiler.
- **Fjerningsnotat:** Slett hele FemVoiceStudio/Tests/-mappen (alle 5). Fjern deretter xunit + xunit.runner.visualstudio + Microsoft.NET.Test.Sdk PackageReferences fra FemVoiceStudio.csproj (l.17/20/21) så testrammeverk ikke lekker inn i exe. ANBEFALING: før sletting, port DirectionAnalyzerTests + (om Classify beholdes) LevelClassificationSystemTests til FemVoiceStudio.Tests/ for å bevare dekning av live kode.

#### ViewModelBase / SubsystemViewModelBase (ViewModels/ViewModelBase.cs) — risiko MIDDELS
- **Tester:** INGEN. Grep ': ViewModelBase'/'SubsystemViewModelBase' i begge testmapper = 0.
- **Kompile-avhengigheter:** INGEN levende kode arver dem. MEN ViewModelBase.cs:7 har 'using FemVoiceStudio.Subsystems.Analysis' og er én av tre gjenværende konsumenter (sammen med Infra/DependencyInjection.cs og selve Subsystems-mappen) som holder de døde Subsystem-interfacene kompilerende. Aktive VM-er arver ObservableObject (CommunityToolkit.Mvvm).
- **Fjerningsnotat:** Slett ViewModelBase.cs sammen med Subsystems-/Infra-runden (FIX-raden). Fjerner én av de tre tingene som holder Subsystem-interfacene 'i bruk'. Ingen test berøres.


**Viktige presiseringer fra analysen:**
- **In-exe-testene:** `DirectionAnalyzerTests` og `LevelClassificationSystemTests` tester LEVENDE
  klasser og har INGEN ekvivalent i FemVoiceStudio.Tests/ — de bør **porteres** dit før mappen slettes.
- **CoachMessageGeneratorTests/VoiceProfileExtensionsTests** kompileres inn i WPF-exe-en: sletter man
  produksjonsklassene uten testfilene, **brekker hovedappens kompilering** — må slettes i lås.
- `FeedbackSignalPolicyTests.cs`: kun de to BreathingRuleEvaluator-testmetodene fjernes — resten
  tester aktiv FeedbackService og beholdes.
- `SafetyLockTests.cs`: kun PeriodizationServiceTests-klassen fjernes — SafetyLockTests +
  ExternalSafetyBlockTests beholdes (tester aktiv kode).
- To falske delte-type-feller avkreftet: `SessionWarningEventArgs` finnes som to UAVHENGIGE klasser
  i hver sin fil/namespace, og testenes `StrainLevel`-treff er en double-property på Models-typer,
  ikke enumen i VoiceStrainDetector.

---

## Seksjon 3 – Merge Candidates (8 par kodelest)

| Primært system | Sekundært system | Verdikt | Innsats | Anbefalt mål |
|---|---|---|---|---|
| ComfortZoneController (ExerciseWindow/clinica | AdaptiveComfortZoneService (MainViewModel/Mai | **BEHOLD-BEGGE** | 7 | Behold ComfortZoneController som eneste sone-AUTORITET. Behold AdaptiveComfortZoneService kun som SessionType/forklaring |
| VocalHealthSupervisor (ExerciseWindow/clinica | LiveMetricsService.CalculateHealth (MainWindo | **BEHOLD-BEGGE** | 8 | Behold begge. Kun hvis MainWindow-stien noensinne skal vise klinisk strain/fatigue/lås (ikke bare en HealthState-farge): |
| FemVoiceScoreEngine (adaptiv, øktslutt-sti) | FemVoiceScore (synkron, live-score-sti) | **BEHOLD-BEGGE** | 8 | Behold begge som lag. Konsolider i stedet vektsett-KONSTANTENE: la FemVoiceScore.ResonanceWeight/PitchWeight/... og FemV |
| ResonansScoringService (0-100, ResonanceWindo | ResonanceProxyEngine (0-1, ExerciseWindow/Ana | **BEHOLD-BEGGE** | 7 | Behold begge. Reduser overlapp ved å samkjøre måltallene (TargetF2Optimal/Centroid) i én delt konfig slik at de to ikke  |
| ProgressionService (global nivå-promotering) | ProgressionOrchestrator (per-øvelse profiljus | **BEHOLD-BEGGE** | 9 | Behold begge. Ingen merge. (Den reelle progresjons-konsolideringen ligger i DELETE-listen: ProgressionEngine/AdaptiveDif |
| ProgressionService (eier av CurrentDifficulty | LevelClassificationSystem (display + død Clas | **MERGE** | 3 | ProgressionService eier nivå-BESLUTNINGEN. Slett LevelClassificationSystem.Classify + instans-tilstanden (_database, l.9 |
| PerformanceQualityExtended (5-trinns display- | PerformanceQuality (4-trinns base-enum, Exerc | **BEHOLD-BEGGE** | 4 | Behold begge enum-ene (clinical maskin-grad vs UI-display-grad er en gyldig separasjon). Men FJERN den villedende døde k |
| App.ConfigureServices (App.xaml.cs) | Infra/DependencyInjection.cs (AddFemVoiceStud | **MERGE** | 3 | App.ConfigureServices er eneste DI-rot. Slett Infra/DependencyInjection.cs sammen med Subsystems/-mappen og ViewModelBas |

**Konklusjon:** Kun **2 reelle merges** (LevelClassificationSystem-display → ProgressionService-stien;
DI-rot-konsolidering). De 6 andre parene er **legitim arbeidsdeling** mellom MainWindow- og
ExerciseWindow-stiene eller komplementære lag (rå-beregning vs. adaptiv normalisering) — en merge
ville omskrevet fungerende kliniske stier. Den reelle opprydningen for disse er i stedet
**konstant-konsolidering**: vektsettene (FemVoiceScore/FemVoiceScoreEngine) og resonans-måltallene
(TargetF2Optimal 2200 vs 2300 Hz!) bør peke på én delt konfig så de ikke driver fra hverandre.

---

## Seksjon 4 – Activation ROI

**Tom kategori.** Alle ACTIVATE-kvalifiserte systemer ble aktivert i forrige runde. Det nærmeste
denne kategorien kommer er «FIX → deretter aktiver»-paret i seksjon 5 (SmartCoach strain-analyse:
klinisk verdi 8, innsats lav — høyest ROI av alt gjenstående).

---

## Seksjon 5 – Fix Priority

**Critical (safety/helse/progresjon)** — blokkerer eller risikerer klinisk funksjonalitet
- SmartCoachEngine (klinisk 8, kost 4): Brutt logikk/skjemabug: SmartCoachHealthMonitoring-tabellen (DatabaseService.cs:324) mangler IsRead-kolonne, men SaveHealthMonitoring (2084-2086) INSERTer IsRead og GetRecentHealthIssues (2132-2133) leser kolonneindeks 8
- SmartCoachHealthMonitoring IsRead-kolonne (skjema/lese-mismatch) (klinisk 7, kost 8): FIX: Brutt logikk/manglende kolonne. Manglende data: legg IsRead INTEGER DEFAULT 0 til i CREATE TABLE SmartCoachHealthMonitoring (l.324-334) OG som AddColumnIfNotExists(connection, 'SmartCoachHealthMonitoring', 'IsRead',

**Important (coaching/analytics)** — begrenser coaching-/analyseverdi
- SmartCoachFeedbackMapper (klinisk 5, kost 4): Brutt logikk/avhengighet: mapperens eneste prod-sti (SmartCoachEngine.SaveCoachMessageThroughPipeline) nås kun fra AnalyzeSessionForStrain (krasjer på IsRead-skjemabug i SaveHealthMonitoring) og GenerateMotivationalMessa
- FeedbackService (klinisk 6, kost 3): Manglende kall: GetProgressiveTip og AddResonanceFeedback/GenerateResonanceFeedback har ingen prod-konsument. ResonansSessionResult produseres av Audio/ResonansScoringService og brukes i ResonanceWindow — aktiveringspunk
- TrainingFrequencyService (klinisk 5, kost 4): Manglende kall: ingen av tjenestens metoder kalles fra ExerciseWindow etter instansiering. Aktiveringspunkt: ExerciseWindow.UpdateTodaysStatus (Views/ExerciseWindow.xaml.cs:571) — vis GetTodaysStatus/GetWeeklyProgress ve
- InMemoryExerciseRepository (IUserRepository + IScoreRepository) (klinisk 6, kost 5): FIX: Manglende persistens. Brutt logikk: alle Save*Async-metoder skriver kun til in-memory-strukturer som forsvinner ved app-avslutning; Get*Async returnerer defaults paa neste oppstart. Manglende data/avhengighet: en SQ

**Enhancement (UI/statistikk)** — kvalitetsforbedring uten klinisk risiko
- Subsystems/ (Audio/Analysis/Data/Progression/SmartCoach + I*-interfaces og *Subsystem-klasser) (klinisk 2, kost 9): FIX (klargjoer for sletting): Brutt logikk/manglende kobling: subsystem-interfacene har null aktive konsumenter, men IAnalysisSubsystem.cs er feilaktig samlokalisert med to live typer (ResonanceCategory, VoiceMetrics). A

---

## Seksjon 6 – Repository Simplification Opportunities

1. **Tre parallellgenerasjoner fjernes** (Subsystems-laget, RealtimeAnalysisEngine-stakken,
   VoiceHealthModule) — dupliserer aktive systemer 1:1.
2. **DI-rot-konsolidering** — Infra/DependencyInjection.cs er et «løgnaktig registreringsunivers».
3. **Vektsett-/måltall-drift** — minst 4 scoringsvektsett og 2 resonans-måltall: konsolider konstanter.
4. **Navnekollisjoner som fjernes ved sletting:** ProgressionDashboardViewModel ×2, VoiceProfile ×2,
   SessionWarningEventArgs ×2, StrainLevel (enum vs property), TrendAlert- vs TrendAnalysisService.
5. **Foreldede prototyper:** VoiceFeminizationExerciseService (mojibake), FeedbackRuleEngine,
   CoachMessageGenerator/Formatter, GamificationService — alle erstattet av aktive systemer.
6. **xunit ut av produksjons-exe-en** (in-exe-testmappen + PackageReferences).

---

## Seksjon 7 – Recommended Repository State (mål-arkitektur)

| System/lag | Mål-tilstand |
|---|---|
| Helse-laget (Supervisor→Recorder→Analytics→Gates) | Aktiv (er aktiv) |
| Session Analytics (økt- + sesjonsnivå) | Aktiv (er aktiv) |
| ProgressionOrchestrator + profil-overrides | Aktiv (er aktiv — fikset + aktivert i forrige runde) |
| ComfortZoneController (adaptiv sone) | Aktiv (er aktiv) |
| SmartCoach strain-analyse + motivasjonsmeldinger | Fikses (IsRead-skjema) → deretter aktiveres |
| IUserRepository/IScoreRepository-persistens | Fikses: SQLite-backing erstatter in-memory |
| LevelClassificationSystem | Merges: display → ProgressionService-stien; Classify fjernes |
| DI-rot | Én rot: App.ConfigureServices (Infra/DependencyInjection fjernes) |
| Vektsett/måltall (scoring + resonans) | Konsolideres i delt konfig (ScoringConfiguration) — fjerner drift-risiko |
| Subsystems/ + Infra/ + ViewModelBase | Fjernet (etter uttrekk av levende typer VoiceMetrics/ResonanceCategory til Models/) |
| Audio-parallellstakken (RealtimeAnalysisEngine m.fl.) | Fjernet |
| VoiceHealthModule + VoiceHealthService + TrendAlert/TrendAnalysis | Fjernet |
| Progresjons-parallellmotorene (Engine/Planner/Config/Periodization/Gamification m.fl.) | Fjernet |
| Død UI (LiveFeedbackView, ExerciseSummaryView, PitchChartVM, døde VM-er, SmartCoachDashboardView) | Fjernet |
| Artefakter (.old, stubber, orphan-SQL/-tabell, in-exe-tester, xunit i exe) | Fjernet |

---

## Seksjon 8 – Final Recommendation

**Aktiveres umiddelbart:** Ingenting — alt aktiverbart kjører allerede.

**Repareres (i prioritert rekkefølge):**
1. 🔴 IsRead-skjemaet i SmartCoachHealthMonitoring + parameterrekkefølge-mismatch → lås opp
   SmartCoach strain-analyse (eneste FIX som blokkerer klinisk funksjonalitet)
2. SQLite-backing for IUserRepository/IScoreRepository (komfortsone/baseline overlever ikke restart)
3. SmartCoach motivasjonsmeldinger-wiring (etter 1)
4. FeedbackService/TrainingFrequencyService: koble inn eller trimme døde metoder
5. Subsystems-typeuttrekk (VoiceMetrics/ResonanceCategory → Models/) — forutsetning for sletting

**Merges:** LevelClassificationSystem (display → ProgressionService-stien, Classify slettes);
DI-rot (App.ConfigureServices alene). Pluss konstant-konsolidering for vektsett/måltall.

**Fjernes:** 45 systemer/artefakter i 7 selvinneholdte klynger (rekkefølge i
klassifiseringsrapporten): tekst-artefakter → in-exe-tester (med portering av
DirectionAnalyzer-/LevelClassification-testene først) → audio-klyngen → helse-klyngen →
progresjons-klyngen → UI-klyngen → Subsystems/Infra (sist, etter typeuttrekk).
