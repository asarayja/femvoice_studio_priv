# Protected Systems Reclassification Report

Status: 2026-06-07

Formål: reklassifisere tidligere DELETE-kandidater fra `FEMVOICE – Repository Cleanup Execution Plan.md` uten å anta at gammel DELETE-status er korrekt.

Regel brukt: ingen sletting anbefales for beskyttede systemer uten eksplisitt bevis på null runtime, null DI, null events, null views, null tester, null dokumentert rolle, null klinisk betydning og null roadmap-kobling.

## Kilder

Nyere dokumentasjon prioritert over gamle cleanup-rapporter:

- `FemVoice_Architecture_Documentation.md`
- `FEMVOICE – Health Intelligence Layer.md`
- `FEMVOICE – ProgressionOrchestrator.md`
- `FEMVOICE – SessionAnalyticsStore.md`
- `FEMVOICE – FeedbackConsistencyGuard.md`
- `new/FEMVOICE 1 – Guidance System Architecture.md`
- `new/FEMVOICE 4 – Guidance Content Library.md`
- `new2/FEMVOICE 1 - Clinical Product Direction.md`
- `new2/FEMVOICE 4 - Personalization and Progression Roadmap.md`
- `new2/FEMVOICE 5 - QA, Validation, and Clinical Review Plan.md`
- `new/FEMVOICE 6 – Release Readiness Checklist.md`
- `new/FEMVOICE 7 – Implementation Verification Matrix.md`

## Hovedkonklusjon

Den gamle cleanup-planen er for aggressiv for systemer som ligger nær klinisk logikk, SmartCoach, Exercise UX, health, progression, analytics og feedback.

Ny status:

| Ny kategori | Antall |
| ----------- | ------ |
| KEEP | 11 |
| FIX | 10 |
| MERGE | 21 |
| DELETE | 20 |

Dette erstatter ikke kode. Det erstatter DELETE-listen som beslutningsgrunnlag før opprydding.

## Beskyttede Systemer Som Er Bekreftet Aktive

Disse var ikke legitime DELETE-kandidater og skal ikke inngå i cleanup-bølger:

| System | Ny vurdering | Begrunnelse | Konfidens |
| ------ | ------------ | ----------- | --------- |
| `ExerciseIntelligenceCoordinator` | KEEP | DI-registrert i `App.ConfigureServices`; runtime-bro til `ExerciseDetailViewModel`; tester; dokumentert i arkitektur, guidance, health/progression flows. | Høy |
| `ExerciseLiveState` | KEEP | Kjernemodell for exercise, health, analytics og UI; brukt av coordinator, VM, recorder og health. | Høy |
| `VocalHealthSupervisor` | KEEP | DI-registrert; brukt av `ExerciseSessionRecorder`; tester; dokumentert Health Intelligence Layer. | Høy |
| `HydrationAdvisor` | KEEP | DI-registrert; koblet til health/feedback pipeline; tester; dokumentert physiology support. | Høy |
| `SessionAnalyticsStore` | KEEP | DI-registrert; brukes av recorder, orchestrator, mastery, safety gate; tester; dokumentert analytics foundation. | Høy |
| `ProgressionOrchestrator` | KEEP | DI-registrert; `ExerciseWindow` bruker den etter økt; tester; dokumentert progresjonsmotor. | Høy |
| `FeedbackPipeline` | KEEP | DI-registrert; brukes av Exercise VM, SmartCoach, health/hydration/progression mappers; tester. | Høy |
| `FeedbackConsistencyGuard` | KEEP | DI-registrert; tester; dokumentert feedback safety policy. | Høy |
| `VoiceGoalProfile` / `LocalVoiceGoalProfileStore` | KEEP | DI-registrert via `IVoiceGoalProfileProvider`; Settings/SmartCoach-retning dokumentert; tester. | Høy |
| Guidance-systemet | KEEP/FIX | Runtime-binding finnes: `ExerciseTargetProfile` -> `ExerciseDetailViewModel.GuidanceItems` -> `ExerciseWindow.xaml`; dokumentert implementert/testet. Runtime-avvik skal håndteres som FIX, aldri DELETE. | Høy |

## Reklassifisering Av Tidligere DELETE-Kandidater

### Artefakter og rene stubs

System: `.old` / `.old2`-filer  
Kategori: Artefakter  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen
- DI: ingen
- Dokumentasjon: arkitekturdokumentet sier gamle backupfiler skaper falske grep-treff
- Klinisk rolle: ingen aktiv rolle  
Konfidens: Høy

System: `AudioAnalysisEngine_new.cs` / `part2.cs`  
Kategori: Artefakter  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen typer eller tom namespace/stub
- DI: ingen
- Dokumentasjon: ingen aktiv rolle
- Klinisk rolle: ingen  
Konfidens: Høy

System: `generate_comfort.py`  
Kategori: Artefakt  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen
- DI: ingen
- Dokumentasjon: ingen aktiv rolle
- Klinisk rolle: ingen  
Konfidens: Høy

System: `Data/migrations/001_exercise_feedback_system.sql`  
Kategori: Artefakt  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen kode laster migration-filen
- DI: ingen
- Dokumentasjon: aktiv database init skjer via `DatabaseService`, ikke denne SQL-filen
- Klinisk rolle: ingen direkte rolle  
Konfidens: Høy

System: `SmartCoachBaselines` plural CREATE-blokk  
Kategori: Schema artefakt  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: plural-tabellen leses/skrives ikke
- DI: ingen
- Dokumentasjon: aktiv baseline bruker entalls-/SmartCoachBaseline-sti
- Klinisk rolle: ingen; behold aktiv baseline, fjern bare orphan plural-blokk  
Konfidens: Høy

System: `IComfortZoneRepository`  
Kategori: Tom kontrakt  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen implementasjon/kall
- DI: ingen
- Dokumentasjon: aktiv comfort-zone styres av `ComfortZoneController`, `PitchTargetZonePolicy`, `ExerciseProfileStore`
- Klinisk rolle: ingen aktiv rolle  
Konfidens: Høy

System: `MockAudioAnalysisEngine` inne i `AudioAnalysisEngine.cs`  
Kategori: Test-/mock-klasse  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen referanser
- DI: ingen
- Dokumentasjon: aktiv audio går via `AudioAnalysisEngine`, ikke mock-klassen
- Klinisk rolle: ingen  
Konfidens: Høy

## SmartCoach-Kandidater

System: `SmartCoachDashboardView`  
Kategori: SmartCoach UI  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen host funnet; kun egen XAML/x:Class
- DI: ingen
- Dokumentasjon: SmartCoach er aktiv produktretning; `SmartCoachDetailWindow/ViewModel` er aktiv vert
- Klinisk rolle: SmartCoach-støtte for trygg trening er klinisk relevant  
Konfidens: Middels  
Anbefaling: ikke slett direkte. Sammenlign UI/innhold med aktiv `SmartCoachDetailWindow`; flytt eventuelle nyttige dashboard-elementer eller dokumenter at det er erstattet før DELETE.

System: `SmartCoachExerciseAdapter`  
Kategori: SmartCoach / Exercise bridge  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: brukt av `LiveFeedbackViewModel` og `ExerciseSummaryViewModel`; disse er ikke hostet, men representerer exercise UX
- DI: ingen
- Dokumentasjon: SmartCoach, exercise recommendation og post-session feedback er aktiv produktretning
- Klinisk rolle: kan inneholde nyttig øvelsesprioritering/session summary logic  
Konfidens: Middels  
Anbefaling: merge nyttig adapterlogikk inn i aktiv `SmartCoachEngine`, `ExerciseSessionRecorder`, `ProgressionOrchestrator` eller feedback mappers før eventuell sletting.

System: `CoachMessageGenerator`  
Kategori: Coaching copy  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet; in-exe tester finnes
- DI: ingen
- Dokumentasjon: SmartCoach/coaching er aktivt; hardcoded text-plan nevner denne komponenten
- Klinisk rolle: coaching språk kan være klinisk sensitivt  
Konfidens: Middels  
Anbefaling: port testverdi/safe-copy forventninger til aktiv feedback pipeline eller SmartCoach mapper før sletting.

System: `CoachMessageFormatter`  
Kategori: Coaching copy formatter  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen
- Dokumentasjon: nevnt i hardcoded text-plan; coaching format er klinisk relevant
- Klinisk rolle: kan inneholde What/Why/How-struktur som bør bevares i aktiv pipeline  
Konfidens: Middels

System: `AdaptiveTargetZoneService`  
Kategori: SmartCoach target-zone service  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen ekstern konsument funnet; intern bruker `SmartCoachEngine`
- DI: ingen
- Dokumentasjon: personalisering og trygg pitch comfort progression er aktiv roadmap
- Klinisk rolle: target-zone adaptation er klinisk relevant, men aktiv sti ser ut til å være `AdaptiveComfortZoneService` og `PitchTargetZonePolicy`  
Konfidens: Middels  
Anbefaling: sammenlign algoritmer mot aktiv target-zone policy; bevar klinisk sikre terskler før sletting.

System: `ProgressionRateCalculator`  
Kategori: SmartCoach/progression helper  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen ekstern konsument funnet
- DI: ingen
- Dokumentasjon: progression rate/frequency er del av personalization roadmap
- Klinisk rolle: treningsfrekvens og sikker progresjon er klinisk relevant  
Konfidens: Middels  
Anbefaling: flytt eventuell nyttig rate/frequency-logikk til `ProgressionOrchestrator`, `ProgressionSafetyGate` eller SmartCoach før sletting.

## VoiceHealth / VocalHealth-Kandidater

System: `VoiceHealthService`  
Kategori: Legacy voice health  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen
- Dokumentasjon: nyere arkitektur bruker `VocalHealthSupervisor`; voice health er beskyttet domene
- Klinisk rolle: pause/session warning-logikk er klinisk relevant selv om implementasjonen er legacy  
Konfidens: Middels  
Anbefaling: verifiser at alle varsler/pausekonsepter er dekket av `VocalHealthSupervisor`, `HydrationAdvisor`, `ExerciseSessionRecorder` og `FeedbackPipeline` før sletting.

System: `VoiceHealthModule/RestProtocolService`  
Kategori: Legacy rest protocol  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen
- Dokumentasjon: recovery/rest er aktiv health roadmap
- Klinisk rolle: hvileprotokoller er sikkerhetskritiske  
Konfidens: Middels  
Anbefaling: sammenlign med `VocalHealthSupervisor` recovery/de-escalation og `ProgressionOrchestrator` recovery profile før sletting.

System: `VoiceHealthModule/StrainMonitor`  
Kategori: Legacy strain monitor  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen
- Dokumentasjon: strain detection er aktiv i `VocalHealthSupervisor`
- Klinisk rolle: strain detection er sikkerhetskritisk  
Konfidens: Middels  
Anbefaling: bevar terskel-/testinnsikt hvis den avviker fra `VocalHealthSupervisor`.

System: `TrendAlertService`  
Kategori: Trend/health alert  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-kall funnet
- DI: ingen
- Dokumentasjon: trendbasert health/progression er aktivt via `SessionAnalyticsStore`, `VocalHealthTrendEngine`, `ProgressionOrchestrator`
- Klinisk rolle: trendvarsler kan være safety-relevant  
Konfidens: Middels  
Anbefaling: sammenlign alert-regler mot aktive trendmotorer før sletting.

System: `TrendAnalysisService`  
Kategori: Analytics utility  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-kall funnet
- DI: ingen
- Dokumentasjon: trend analytics er aktivt i `SessionAnalyticsStore`/dashboards
- Klinisk rolle: trendtolkning er relevant, men denne statiske utilityen ser erstattet ut  
Konfidens: Middels  
Anbefaling: bevar bare unike algoritmer/testcases; ellers slett etter merge-audit.

## Exercise UX / Guidance-Kandidater

System: `ExerciseFeedbackEngine`  
Kategori: Legacy exercise evaluator  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: konsumert av unhosted `LiveFeedbackViewModel`/`ExerciseSummaryViewModel`; testfil finnes, men ekskludert fra testprosjekt via csproj
- DI: ingen aktiv App DI
- Dokumentasjon: active exercise flow er nå `ExerciseIntelligenceCoordinator` -> `ExerciseLiveState` -> `ExerciseDetailViewModel`
- Klinisk rolle: øvelsesevaluering er kjernefunksjon; ikke slett uten å sikre at test-/health-regler er overført  
Konfidens: Middels  
Anbefaling: merge eventuelle health/parameter-regler til aktiv coordinator/tests før sletting.

System: `LiveFeedbackView` + `LiveFeedbackViewModel`  
Kategori: Legacy exercise UI  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen host funnet; egen XAML/x:Class
- DI: ingen
- Dokumentasjon: live feedback er aktiv og produksjonskritisk, men aktiv UI er i `ExerciseWindow` / `ExerciseDetailViewModel`
- Klinisk rolle: live feedback språk og states er klinisk sensitivt  
Konfidens: Middels  
Anbefaling: sammenlign med aktiv Exercise Guide live feedback før sletting.

System: `ExerciseSummaryView` + `ExerciseSummaryViewModel`  
Kategori: Legacy post-session UI  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen host funnet
- DI: ingen
- Dokumentasjon: post-session subjective report/progression er aktivt
- Klinisk rolle: session summary/coaching er klinisk relevant  
Konfidens: Middels  
Anbefaling: merge eventuell summary logic med `SubjectiveReportPanel`, `ExerciseSessionRecorder`, `ProgressionOrchestrator`.

System: `ExerciseListViewModel`  
Kategori: Legacy exercise list VM  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen aktiv view/konsument
- DI: ingen
- Dokumentasjon: active Exercise Guide bruker annen list/detail-flow; stale kommentarer i `ExerciseDetailViewModel`
- Klinisk rolle: ingen unik rolle funnet  
Konfidens: Høy

System: Guidance system (`GuidanceItem`, `GuidanceItems`, profile keys, RESX keys)  
Kategori: Guidance  
Tidligere: ikke legitim DELETE  
Ny vurdering: FIX  
Begrunnelse:
- Runtime: `ExerciseDetailViewModel.RebuildGuidanceItems()` og `ExerciseWindow.xaml` ItemsControl-binding er aktiv
- DI: VM er DI-registrert
- Dokumentasjon: Guidance er implementert/integrert/testet/produksjonsklart
- Klinisk rolle: safety, mistakes, physical focus og clinical purpose er direkte klinisk UX  
Konfidens: Høy  
Anbefaling: runtime-avvik skal feilsøkes som FIX, ikke cleanup.

## Feedback-Kandidater

System: `FeedbackRuleEngine` mappe  
Kategori: Legacy feedback evaluators  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-kall funnet; testreferanse i `FeedbackSignalPolicyTests`
- DI: ingen
- Dokumentasjon: aktiv feedback policy går gjennom `FeedbackService`, `FeedbackPipeline`, `FeedbackConsistencyGuard`, mappers
- Klinisk rolle: feedback-regler er sikkerhetskritiske og har testverdi  
Konfidens: Middels  
Anbefaling: flytt unike pust/intensitet/regelfunn til aktiv feedback policy-testdekning før sletting.

## Progression-Kandidater

System: `ProgressionEngine`  
Kategori: Legacy progression engine  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: registrert bare i `Infra/DependencyInjection.cs`; `AddFemVoiceStudio` ser ikke brukt av `App.ConfigureServices`
- DI: død DI-rot, men registrert i den
- Dokumentasjon: aktiv progression er `ProgressionOrchestrator`, `ProgressionService`, `ProgressionSafetyGate`, `MasteryEvaluator`
- Klinisk rolle: progression er beskyttet; legacy engine kan inneholde regler som bør sammenlignes  
Konfidens: Middels  
Anbefaling: merge/review før sletting, spesielt safety gates og target adjustment.

System: `WeeklyPlannerEngine`  
Kategori: Legacy weekly planner  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: registrert bare i død `Infra/DependencyInjection.cs`
- DI: ikke aktiv App DI
- Dokumentasjon: treningsfrekvens/recovery er roadmap
- Klinisk rolle: weekly load planning kan være safety-relevant  
Konfidens: Middels

System: `PeriodizationService`  
Kategori: Legacy periodization  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-kall funnet
- DI: ingen aktiv App DI
- Tester: `SafetyLockTests` har `PeriodizationServiceTests`
- Dokumentasjon: periodisering/recovery/progression er klinisk relevant  
Konfidens: Middels  
Anbefaling: port relevant test/logic til `ProgressionOrchestrator`/`ProgressionSafetyGate` før sletting.

System: `ProgressionConfig`  
Kategori: Legacy progression config  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: brukt av legacy `ProgressionEngine`, `WeeklyPlannerEngine`, `PeriodizationService`
- DI: ikke aktiv App DI
- Dokumentasjon: progression thresholds er klinisk relevante
- Klinisk rolle: config-verdier kan ha safety-verdi  
Konfidens: Middels

System: `PeriodizationModels` / `TrainingLoad` / `WeeklySchedule` / `UserProgressionProfile`  
Kategori: Legacy progression models  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: brukt av legacy progression/weekly planner stack
- DI: indirekte via dead `Infra`
- Dokumentasjon: load/recovery/frequency roadmap aktiv
- Klinisk rolle: load/recovery models er relevante selv om denne stacken ikke er aktiv runtime  
Konfidens: Middels  
Anbefaling: ikke slett før nytt/aktivt load-model ansvar er bekreftet.

System: `ProgressionDashboardViewModel` i `ViewModels` namespace  
Kategori: Duplicate progression dashboard VM  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: aktiv `Views/ProgressionDashboard.xaml.cs` har nested VM med samme navn; ViewModels-varianten ser uhostet ut
- DI: ingen
- Dokumentasjon: progression dashboard er aktivt
- Klinisk rolle: dashboard/progression copy og display kan ha brukerbetydning  
Konfidens: Middels  
Anbefaling: sammenlign ViewModels-varianten mot aktiv nested VM før sletting for å unngå tapt UI-logikk.

System: `AdaptiveDifficultyService`  
Kategori: Legacy difficulty/progression service  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen
- Dokumentasjon: adaptive progression aktiv via `ProgressionOrchestrator`
- Klinisk rolle: difficulty scaling kan være safety-relevant  
Konfidens: Middels

System: `GamificationService`  
Kategori: Gamification/progression  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen
- Dokumentasjon: nyere clinical docs vektlegger trygg progresjon, ikke gamification
- Klinisk rolle: kan øke score-chasing; ingen beskyttet aktiv rolle funnet  
Konfidens: Middels

## Audio / Analysis-Kandidater

System: `RealtimeAnalysisEngine`  
Kategori: Legacy realtime audio engine  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen aktiv App DI
- Dokumentasjon: realtime audio/biofeedback er aktivt; nåværende runtime bruker `AudioAnalysisEngine`, `AudioAnalyzerService`, `ResonanceProxyEngine`
- Klinisk rolle: kan inneholde strain/intensity logic  
Konfidens: Middels  
Anbefaling: compare/merge unique strain or signal logic before delete.

System: `AsyncAudioPipeline`  
Kategori: Legacy audio pipeline  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: ingen aktiv prod-konsument funnet
- DI: ingen
- Dokumentasjon: active capture/analyzer integration uses other services
- Klinisk rolle: no unique documented role found  
Konfidens: Middels

System: `AdaptivePitchDetector`  
Kategori: Legacy pitch detector  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: used internally by `AsyncAudioPipeline`; not active app runtime
- DI: none
- Dokumentasjon: mic calibration / low-output handling is active clinical concern
- Klinisk rolle: detector thresholds may contain useful mic robustness logic  
Konfidens: Middels

System: `VoiceActivityDetector`  
Kategori: Legacy VAD  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: used by `AsyncAudioPipeline`/`AnalysisSubsystem`, not active App DI
- DI: only through dead `Infra`/Subsystem path
- Dokumentasjon: low-signal/no-voice handling is active
- Klinisk rolle: technical signal gating affects clinical feedback safety  
Konfidens: Middels

System: `VoiceStrainDetector`  
Kategori: Strain detector  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: used by legacy realtime/analysis subsystems
- DI: dead Infra path
- Dokumentasjon: strain detection is protected health logic
- Klinisk rolle: strain detection is safety-critical  
Konfidens: Middels  
Anbefaling: compare with `VocalHealthSupervisor` before delete.

System: `VoiceMetricsCalculator`  
Kategori: Voice metrics  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: no active host found
- DI: none
- Dokumentasjon: Measurement & Biofeedback roadmap and clinical alignment identify vocal weight/quality as future need
- Klinisk rolle: jitter/shimmer/HNR/intensity may be useful for future vocal weight/strain dimension  
Konfidens: Middels

System: `SpeechRateAnalyzer`  
Kategori: Speech analysis  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: no active host found
- DI: none
- Dokumentasjon: clinical product direction includes speech rate/prosody/communication dimensions
- Klinisk rolle: not core release blocker, but possible future communication metric  
Konfidens: Lav-Middels

## Subsystems / Infra

System: `Subsystems/*`  
Kategori: Legacy subsystem architecture  
Tidligere: FIX/DELETE wave  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: not called by App DI; some live type definitions (`VoiceMetrics`, `ResonanceCategory`) are referenced by active code
- DI: registered only in `Infra/DependencyInjection.cs`, not active `App.ConfigureServices`
- Dokumentasjon: current architecture uses Services/ViewModels, not subsystem layer
- Klinisk rolle: analysis types and any useful signal logic must be preserved  
Konfidens: Høy  
Anbefaling: extract live types and merge any useful logic before deleting implementation layer.

System: `Infra/DependencyInjection.cs`  
Kategori: Legacy DI root  
Tidligere: MERGE/DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: `App.ConfigureServices` is active DI root
- DI: `AddFemVoiceStudio` contains registrations for legacy subsystem/progression stack
- Dokumentasjon: architecture says App DI/runtime is current; old DI can mislead
- Klinisk rolle: none directly, but unsafe to delete until subsystem type extraction is complete  
Konfidens: Høy

System: `ViewModelBase` / `SubsystemViewModelBase`  
Kategori: Legacy base classes  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: no active derived classes found
- DI: none
- Dokumentasjon: tied to legacy Subsystems layer
- Klinisk rolle: none directly, but compile-coupled to subsystem extraction  
Konfidens: Middels

## Other Candidates

System: `PitchChartViewModel`  
Kategori: Unused graph VM  
Tidligere: DELETE  
Ny vurdering: DELETE  
Begrunnelse:
- Runtime: no XAML/CS host found
- DI: none
- Dokumentasjon: active chart uses `MainWindow.xaml.cs`, `PitchTraceStabilizer`, `PitchChartAxisRangeCalculator`
- Klinisk rolle: graph role is covered by active services; no test value found  
Konfidens: Høy

System: `VoiceFeminizationExerciseService` + `ResonanceModuleDocumentation`  
Kategori: Legacy exercise catalog/docs service  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: no active prod-konsument found
- DI: none
- Dokumentasjon: exercise/guidance content is active and clinically important
- Klinisk rolle: exercise content/resonance docs may contain reusable clinical text/structure  
Konfidens: Middels  
Anbefaling: compare content against active `ExerciseTargetProfile`, RESX guidance library and Exercise Guide before deleting.

System: `VoiceProfileExtensions`  
Kategori: Legacy voice profile/personalization  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: no active prod-konsument found; in-exe tests reference it
- DI: none
- Dokumentasjon: Voice Goal Profile/personalization is active product direction
- Klinisk rolle: personal goals and non-binary-safe voice profile logic are clinically relevant  
Konfidens: Middels  
Anbefaling: preserve unique tests/ideas in `VoiceGoalProfile`/SmartCoach path before deleting.

System: `In-exe FemVoiceStudio/Tests`  
Kategori: Misplaced tests  
Tidligere: DELETE  
Ny vurdering: FIX  
Begrunnelse:
- Runtime: should not compile into WPF exe
- DI: none
- Dokumentasjon: test coverage is required for release readiness
- Klinisk rolle: some tests cover active/safety logic and must be ported, not deleted blind  
Konfidens: Høy  
Anbefaling: port useful tests to `FemVoiceStudio.Tests`, then remove xUnit from production project.

System: `AnalysisSubsystem` implementation  
Kategori: Legacy analysis subsystem  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: only legacy Infra DI
- DI: not active App DI
- Dokumentasjon: active analysis is Services/Audio/Analyzer; some subsystem model types are live
- Klinisk rolle: analysis/strain logic may have reusable clinical metrics  
Konfidens: Middels

System: `DataSubsystem` / `IDataSubsystem`  
Kategori: Legacy data subsystem  
Tidligere: DELETE  
Ny vurdering: MERGE  
Begrunnelse:
- Runtime: only legacy subsystem graph
- DI: dead Infra
- Dokumentasjon: active data layer is `DatabaseService` plus `SessionAnalyticsStore`
- Klinisk rolle: data export/backup concepts may have future clinician-export relevance  
Konfidens: Middels

## Endelig Cleanup Gate

Før noe slettes:

1. Kjør grep etter eksterne referanser uten `.old`, `bin`, `obj`.
2. Sjekk `App.ConfigureServices` og ikke bare `Infra/DependencyInjection.cs`.
3. Sjekk XAML host og code-behind constructors.
4. Sjekk `FemVoiceStudio.Tests` og in-exe `FemVoiceStudio/Tests`.
5. For SmartCoach/VoiceHealth/VocalHealth/Progression/Exercise/Guidance/Feedback: utfør merge-audit før sletting.
6. Port testverdi først.
7. Oppdater arkitekturdokumentasjon etter hver faktisk cleanup-bølge.

## Ny Beslutning

Den eksisterende Repository Cleanup Execution Plan skal ikke kjøres som sletteliste.

Ny regel:

- Artefakter/stubber uten klinisk rolle kan slettes.
- Uhostede UI-er i beskyttede domener skal MERGE-audites.
- Legacy health/progression/audio/feedback engines skal MERGE-audites.
- Guidance skal KEEP/FIX.
- Aktive DI-/runtime-kjerner skal KEEP.

