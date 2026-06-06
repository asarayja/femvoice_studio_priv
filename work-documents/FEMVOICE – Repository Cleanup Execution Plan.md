# FEMVOICE – Repository Cleanup Execution Plan

**Dato:** 2026-06-07 · **Mot:** HEAD `c176121` · **Bygger på:** Cleanup & Activation Decision Report,
Dead Systems Classification Report (testavhengighets-/risikokart adversarialt verifisert).
**Omfang:** 47 DELETE + 2 MERGE. KEEP ignoreres; FIX er eksplisitt UTENFOR denne planen
(egen runde — IsRead-skjemaet m.m.). Ingen arkitektur-redesign, ingen nye systemer.
**Plattform:** Hver bølge kompileringssjekkes på Linux (`-p:EnableWindowsTargeting=true`);
testkjøring og runtime-røyk gjøres på Windows før bølgen pushes.

---

## Seksjon 1 – Cleanup Scope

| Kategori | Antall | Handling i denne planen |
|---|---|---|
| DELETE | 47 systemer/artefakter | Fjernes i 6 bølger |
| MERGE | 2 | Konsolideres (Merge 1 i Sprint 2, Merge 2 = del av Bølge 6) |
| KEEP | 33 | Ignoreres — røres ikke |
| FIX | 8 | **Ekskludert** — egen reparasjonsrunde etter opprydding |

---

## Seksjon 2 – Cleanup Waves

### Bølge 1 – Artefakter *(risiko: LAV — ingenting kompileres)*
- 14 `.old`/`.old2`-filer (liste i avhengighetskartet)
- `Audio/AudioAnalysisEngine_new.cs` (1 linje), `Audio/part2.cs` (tomt namespace)
- `Services/generate_comfort.py` (tom stub)
- `Data/migrations/001_exercise_feedback_system.sql` (aldri kjørt, ugyldig SQLite-syntaks) + tom mappe
- `SmartCoachBaselines`-CREATE-blokken i DatabaseService.cs (~l.260) — **flertallsvarianten**, ikke entall l.395
- `MockAudioAnalysisEngine`-klassen (kun klassen, fra AudioAnalysisEngine.cs:1264 — filen ellers er KEEP)
- `Data/IComfortZoneRepository.cs` (tom kontrakt)
- *Valgfritt, fra tidligere ryddeliste (utenfor klassifiseringen — bekreft først):* tomme rotfiler
  (`analyse.txt`, `info.txt`, `hva skal gj`), `install.ps1` (Blackbox-installer), `.blackboxcli/`

### Bølge 2 – Døde tester + testinfrastruktur *(risiko: LAV-MIDDELS — krever portering FØRST)*
1. **Porter FØRST** (taper ellers unik dekning av levende kode):
   - `FemVoiceStudio/Tests/DirectionAnalyzerTests.cs` → `FemVoiceStudio.Tests/` (DirectionAnalyzer er KEEP)
   - `FemVoiceStudio/Tests/LevelClassificationSystemTests.cs` → `FemVoiceStudio.Tests/` —
     **kun display-metode-testene** (Classify-testene dør med Merge 1)
2. Slett hele `FemVoiceStudio/Tests/`-mappen (5 filer — trygt: tester refererer prod, aldri omvendt)
3. Fjern `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` PackageReferences fra
   `FemVoiceStudio.csproj` (testrammeverk slutter å lekke inn i produksjons-exe-en)

### Bølge 3 – Død UI *(risiko: MIDDELS — XAML/x:Class + koblet engine)*
- `Views/LiveFeedbackView.xaml(+.cs)` + `Views/LiveFeedbackViewModel.cs`
- `Views/ExerciseSummaryView.xaml(+.cs)` + `Views/ExerciseSummaryViewModel.cs`
- `Services/SmartCoachExerciseAdapter.cs` (kun konsumert av VM-ene over)
- `Services/ExerciseFeedbackEngine.cs` **+ hele `FemVoiceStudio.Tests/ExerciseFeedbackEngineTests.cs`**
  (eneste prod-konsumenter er VM-ene over — må gå i lås)
- `Views/PitchChartViewModel.cs` (≠ den aktive PitchChartAxisRangeCalculator!)
- `ViewModels/ProgressionDashboardViewModel.cs` (den døde navnekollisjonen; nested i Views/ er aktiv)
- `ViewModels/ExerciseListViewModel.cs` + rydd de to stale kommentarene i
  `ExerciseDetailViewModel.cs:15/171` (RelayCommand bor i `ViewModels/RelayCommand.cs` — verifisert)
- `Views/SmartCoachDashboardView.xaml(+.cs)` (hostes aldri; SmartCoachDetailWindow er aktiv vert)

### Bølge 4 – Døde tjenester *(risiko: LAV-MIDDELS)* **+ Merge 1**
- Helse: `VoiceHealthService.cs`, `TrendAlertService.cs`, `Services/VoiceHealthModule/` (RestProtocolService + StrainMonitor + tom mappe)
- Analyse: `TrendAnalysisService.cs` (≠ TrendAlertService — begge ryker, men de er ulike filer)
- Coaching: `CoachMessageGenerator.cs` (inkl. Formatter, samme fil), `VoiceFeminizationExerciseService.cs` + `ResonanceModuleDocumentation.cs` (slettes sammen), `Services/FeedbackRuleEngine/` (hele mappen) **+ fjern de to BreathingRuleEvaluator-testmetodene og using-en i `FeedbackSignalPolicyTests.cs`** (resten av fila beholdes!)
- Progresjon/diverse: `GamificationService.cs`, `AdaptiveDifficultyService.cs`, `Services/SmartCoachModule/AdaptiveTargetZoneService.cs` (**kun tjenesten — modellen `AdaptiveTargetZone` i Models/SmartCoachModels.cs beholdes**), `Services/SmartCoachModule/ProgressionRateCalculator.cs`
- **Merge 1 — LevelClassificationSystem → ProgressionService-stien** (se Seksjon 4)
- *Valgfritt:* rydd ubrukte RESX-nøkler (AdaptiveDifficulty_*, ProgressionRate_*) — kosmetisk

### Bølge 5 – Død progresjons-stack + frittstående audio *(risiko: MIDDELS — koblede edits)*
- **Progresjon:** `PeriodizationService.cs` (**+ fjern PeriodizationServiceTests-KLASSEN i
  `SafetyLockTests.cs:333+` — resten av fila tester aktiv kode og beholdes**),
  `Services/Progression/ProgressionEngine.cs`, `Services/Progression/WeeklyPlannerEngine.cs`,
  `Services/ProgressionConfig.cs`, og deretter modellene `Models/PeriodizationModels.cs`,
  `Models/TrainingLoad.cs`, `Models/WeeklySchedule.cs`, `Models/UserProgressionProfile.cs`
  — **BLOKKER:** ProgressionEngine/WeeklyPlannerEngine er registrert i Infra/DependencyInjection.cs:28-29;
  fjern de to registreringslinjene i samme commit (fila som helhet ryker i Bølge 6)
  — **PASS PÅ delte typer som IKKE skal slettes:** SessionType (ScoreSnapshot.cs),
  ProgressionDecision (ProgressionEnums.cs), ProgressionGateStatus-varianten i ProgressionSessionData.cs
- **Audio (kun de frittstående):** `RealtimeAnalysisEngine.cs` (inkl. RollingBuffer/SignalSmoothing —
  selvinneholdt, verifisert), `AsyncAudioPipeline.cs`, `VoiceMetricsCalculator.cs`, `SpeechRateAnalyzer.cs`
  — **BLOKKER:** `AdaptivePitchDetector`/`VoiceActivityDetector`/`VoiceStrainDetector` kan IKKE
  slettes her — AnalysisSubsystem.cs (Bølge 6) new-er dem fortsatt. De flyttes til Bølge 6.
- `Services/VoiceProfileExtensions.cs` (testen ryker allerede i Bølge 2)

### Bølge 6 – Død infrastruktur *(risiko: HØY — typeuttrekk først)* **+ Merge 2**
1. **Typeuttrekk FØRST:** flytt `VoiceMetrics` + `ResonanceCategory` (+evt. VoiceParameter/TargetZone/
   HealthIndicators) fra `Subsystems/Analysis/IAnalysisSubsystem.cs` til `Models/` og oppdater
   namespace-referansene i LEVENDE kode: `TrainingSession.cs`, `Feedback.cs`, `FeedbackService.cs`,
   `DatabaseService.cs:1351/1407`, `ResonanceWindow.xaml.cs:321` (+ evt. test-usings)
2. Slett deretter i ÉN samlet commit (holder hverandre kompilerende):
   hele `Subsystems/`-mappen (10 filer: alle I*Subsystem + impl, inkl. DataSubsystem/IDataSubsystem),
   `Infra/DependencyInjection.cs` (**= Merge 2**), `ViewModels/ViewModelBase.cs`,
   og de tre audio-detektorene `AdaptivePitchDetector.cs` (inkl. RollingStatistics),
   `VoiceActivityDetector.cs`, `VoiceStrainDetector.cs`

---

## Seksjon 3 – Dependency Removal Map

| System/klynge | Kompile-avhengigheter (levende) | Tester som må håndteres | Risiko | Fjerningsnotat |
|---|---|---|---|---|
| AnalysisSubsystem (impl-klassen, Subsystems/Analysis/An | Død impl, men kompile-koblet til Subsystems-FIX: registreres i Infra/DependencyInjection.c | INGEN. Ingen test refererer klassen direkte (grep 'AnalysisSubsystem' i tester = | HØY | Slett KUN AnalysisSubsystem.cs (impl), ikke IAnalysisSubsystem.cs. Krever uttrekk/bevaring av de live typene i |
| DataSubsystem (Subsystems/Data/DataSubsystem.cs + IData | Død, men inngår i Subsystems-FIX-kompileringsnett: eneste konsumenter er IDataSubsystem in | INGEN. Grep 'DataSubsystem'/'IDataSubsystem' i begge testmapper = 0. | HØY | Slett DataSubsystem.cs + IDataSubsystem.cs i SAMME rydderunde som hele Subsystems-mappen + Infra/DependencyInj |
| Infra/DependencyInjection.cs (AddFemVoiceStudio + exten | Død gen-2 DI-graf. Refererer alle de døde Subsystem-interfacene + impl + ProgressionEngine | INGEN ekte. Grep 'AddFemVoiceStudio'/'FemVoiceStudio.Infra' i tester = 0. Treffe | HØY | Slett hele Infra/DependencyInjection.cs i SAMME runde som Subsystems-mappen + ViewModelBase.cs (de tre holder  |
| Progresjons-død-klyngen: ProgressionEngine.cs (+egen Pr | INGEN levende prod-kode. ProgressionEngine/WeeklyPlannerEngine registreres KUN i den døde  | MIDDELS: PeriodizationServiceTests-KLASSEN (ikke hele filen) i FemVoiceStudio.Te | MIDDELS | Slett rekkefølge: (1) fjern PeriodizationServiceTests-klassen i SafetyLockTests.cs; (2) fjern registreringene  |
| ExerciseFeedbackEngine (Services/ExerciseFeedbackEngine | Død i prod, men kompile-koblet: ExerciseFeedbackEngine.cs:7 har 'using VoiceAnalysisMetric | MIDDELS/HØY: FemVoiceStudio.Tests/ExerciseFeedbackEngineTests.cs er DEDIKERT tes | MIDDELS | Slett ExerciseFeedbackEngine.cs + hele ExerciseFeedbackEngineTests.cs samtidig med UI-død-klyngen (LiveFeedbac |
| UI-død-klyngen: LiveFeedbackView(.xaml+.xaml.cs) + Live | Selvinneholdt død klynge. Views forekommer kun i egen XAML x:Class (LiveFeedbackView.xaml: | INGEN. Grep 'LiveFeedbackView(Model)'/'ExerciseSummaryView(Model)'/'SmartCoachEx | MIDDELS | Slett alle 6 view/VM-filer + SmartCoachExerciseAdapter.cs samlet med ExerciseFeedbackEngine-raden. Kommentar-r |
| ExerciseListViewModel (ViewModels/ExerciseListViewModel | INGEN ekte. Refereres ikke av aktiv view/kode. RelayCommand ligger nå i egen fil ViewModel | INGEN. Grep 'ExerciseListViewModel' i begge testmapper = 0 (ExerciseDetailViewMo | MIDDELS | Slett HELE ExerciseListViewModel.cs (trenger IKKE ekstrahere RelayCommand). Rydd samtidig de to stale kommenta |
| CoachMessageGenerator / CoachMessageFormatter (Services | INGEN prod-kode. CoachMessageGenerator instansieres kun i nevnte in-exe-test. CoachMessage | MIDDELS/KRITISK: FemVoiceStudio/Tests/CoachMessageGeneratorTests.cs (IN-EXE test | MIDDELS | Slett CoachMessageGenerator.cs + CoachMessageFormatter (samme fil) + FemVoiceStudio/Tests/CoachMessageGenerato |
| FeedbackRuleEngine-mappen (Services/FeedbackRuleEngine/ | INGEN prod-kode. Grep 'FeedbackRuleEngine'/'CompositeEvaluator' i prod (utenfor mappen+tes | MIDDELS: FemVoiceStudio.Tests/FeedbackSignalPolicyTests.cs bruker 'new Breathing | MIDDELS | Slett hele FeedbackRuleEngine/-mappen + fjern de to BreathingRuleEvaluator-testmetodene (og 'using ...Feedback |
| VoiceProfileExtensions (Services/VoiceProfileExtensions | INGEN prod-kode. Eneste referanse i hele repoet er nevnte in-exe-test. Definerer en konkur | HØY-koblet (men trygg): FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs (IN- | MIDDELS | Slett Services/VoiceProfileExtensions.cs + FemVoiceStudio/Tests/VoiceProfileExtensionsTests.cs samtidig (del a |
| In-exe testmappe FemVoiceStudio/Tests/ (xunit kompilert | Disse 5 filene kompileres INN i WinExe-en (default Compile-glob uten Remove + xunit/Micros | Dette ER testfiler — alle 5 slettes. Dekningsnyanse: (a) FemVoiceScoreTests.cs e | MIDDELS | Slett hele FemVoiceStudio/Tests/-mappen (alle 5). Fjern deretter xunit + xunit.runner.visualstudio + Microsoft |
| ViewModelBase / SubsystemViewModelBase (ViewModels/View | INGEN levende kode arver dem. MEN ViewModelBase.cs:7 har 'using FemVoiceStudio.Subsystems. | INGEN. Grep ': ViewModelBase'/'SubsystemViewModelBase' i begge testmapper = 0. | MIDDELS | Slett ViewModelBase.cs sammen med Subsystems-/Infra-runden (FIX-raden). Fjerner én av de tre tingene som holde |
| Audio-død-klyngen: RealtimeAnalysisEngine (+RollingBuff | Selvinneholdt klynge — null levende konsumenter utenfor klyngen. Intern eierskapskjede: As | INGEN ekte testavhengighet. Grep i FemVoiceStudio.Tests/ og FemVoiceStudio/Tests | LAV | Slett som én klynge (inkl. AnalysisSubsystem.cs fra Subsystems-raden i samme runde, siden den deler eierskap t |
| MockAudioAnalysisEngine (klasse inni Audio/AudioAnalysi | INGEN levende kode. Eneste forekomst i hele repoet er klassedeklarasjonen Audio/AudioAnaly | INGEN. Grep 'MockAudioAnalysisEngine' i begge testmappene = 0 treff. | LAV | Fjern KUN klassedeklarasjonen (fra l.1264 til klassens slutt) i AudioAnalysisEngine.cs — IKKE slett filen (res |
| AudioAnalysisEngine_new.cs (tom stub) + part2.cs (tomt  | INGEN. AudioAnalysisEngine_new.cs = 1 linje (kun 'using System;'), part2.cs = 10 linjer to | INGEN. Ingen typer, ingen referanser, ingen testtreff. | LAV | Slett alle tre filene direkte. Null kompileringskonsekvens. |
| .old/.old2-filer i hele treet (14 filer: App.xaml.cs.ol | INGEN. Bekreftet ingen .old-referanse i FemVoiceStudio.csproj (grep '\.old' = ingen treff) | INGEN. .old-endelsen plukkes ikke opp av default Compile-glob; ingen test refere | LAV | Slett alle 14 filene direkte; null kompileringskonsekvens. Eneste 'risiko' er falske grep-treff de skaper i da |
| Migration-SQL Data/migrations/001_exercise_feedback_sys | INGEN. Grep i produksjonskode etter '001_exercise_feedback'/'migrations'/ReadAllText for s | INGEN. Ingen test laster eller refererer fila. | LAV | Slett .sql-fila (og evt. tom migrations/-mappe). Null kompilerings- eller runtime-konsekvens. |
| SmartCoachBaselines (plural) orphan-tabell i Data/Datab | INGEN levende kode leser/skriver plural-tabellen. Eneste forekomst i hele repoet er CREATE | INGEN. Grep 'SmartCoachBaselines' (plural) i tester = 0. (Tester bruker entalls- | LAV | Fjern CREATE TABLE IF NOT EXISTS SmartCoachBaselines-blokken (~l.260). Pass på å fjerne FLERTALLS-blokken, ikk |
| IComfortZoneRepository (Data/IComfortZoneRepository.cs) | INGEN. Grep gir kun interface-definisjonen (IComfortZoneRepository.cs:13). Ingen implement | INGEN. Grep 'IComfortZoneRepository' i begge testmapper = 0. | LAV | Slett IComfortZoneRepository.cs alene. Null kompileringskonsekvens. |
| ProgressionDashboardViewModel (ViewModels-namespacet, V | INGEN levende kode. Navnekollisjon: alle prod-treff på ProgressionDashboardViewModel peker | INGEN. Grep i begge testmapper = 0. | LAV | Slett ViewModels/ProgressionDashboardViewModel.cs (ViewModels-ns-varianten). Behold den nestede Views-variante |
| PitchChartViewModel (Views/PitchChartViewModel.cs) | INGEN. Grep gir kun definisjonen (class+ctor). Ingen XAML binder til den, ingen .cs-refera | INGEN. Grep i begge testmapper = 0. (Den aktive PitchChartAxisRangeCalculator ha | LAV | Slett PitchChartViewModel.cs alene. Ikke forveksle med den aktive PitchChartAxisRangeCalculator (Services/, KE |
| SmartCoachDashboardView (Views/SmartCoachDashboardView. | INGEN. Forekommer kun i egen XAML x:Class (SmartCoachDashboardView.xaml:1). Ingen ekstern  | INGEN. Grep i begge testmapper = 0. | LAV | Slett SmartCoachDashboardView.xaml + .xaml.cs. Ingen kompileringsavhengighet. |
| VoiceHealthService (Services/VoiceHealthService.cs) + S | INGEN levende kode. 0 prod-konsumenter (grep utenfor egen fil = 0), ikke DI-registrert. Se | INGEN. Grep 'VoiceHealthService' / 'SessionWarningEventArgs' i begge testmapper  | LAV | Slett VoiceHealthService.cs direkte (sin egen SessionWarningEventArgs ryker med). Resten kompilerer uendret —  |
| TrendAlertService (Services/TrendAlertService.cs) + Saf | INGEN. 0 prod-kall, ikke DI-registrert, ingen 'new'. Ingen live kode konsumerer SafetyChec | INGEN. Grep i begge testmapper = 0. | LAV | Slett TrendAlertService.cs direkte. Avhenger kun innkommende av DatabaseService/ILocalizationService. Ikke for |
| TrendAnalysisService (Services/TrendAnalysisService.cs) | INGEN. Rene statiske utility-metoder, 0 kall utenfor egen fil. Ingen live kode importerer  | INGEN ekte. Grep 'TrendAnalysisService'/'TrendResult'/'PitchPatternResult'/'Cale | LAV | Slett TrendAnalysisService.cs direkte. Ikke forveksle med TrendAlertService eller med SmartCoachWeeklyProgress |
| VoiceHealthModule: StrainMonitor.cs + RestProtocolServi | INGEN levende kode. Begge har 0 prod-kall, ikke DI-registrert. StrainMonitor forutsetter j | INGEN. Grep 'StrainMonitor'/'RestProtocolService'/'StrainAction'/'StrainEventArg | LAV | Slett begge filene sammen; fjern den tomme VoiceHealthModule-mappen etterpå. Klyngen er selvinneholdt. |
| GamificationService (Services/GamificationService.cs) + | INGEN. 0 prod-referanser ('new GamificationService' = 0). Achievements-PERSISTENS i Databa | INGEN. Grep i begge testmapper = 0. | LAV | Slett GamificationService.cs etter å ha bekreftet at dens egne event-/reward-typer ikke brukes annensteds (ver |
| AdaptiveDifficultyService (Services/AdaptiveDifficultyS | INGEN. Null prod-referanser, ingen DI. Supporting-typene defineres i samme fil og brukes i | INGEN. Grep i begge testmapper = 0. | LAV | Slett hele filen. Vurder å rydde ubrukte RESX-nøkler (AdaptiveDifficulty_*) — kosmetisk, ikke kompileringskrit |
| ProgressionRateCalculator (Services/SmartCoachModule/Pr | INGEN. 0 eksterne referanser. Inneholder bekreftet bug l.77. Supporting-typer i samme fil, | INGEN. Grep i begge testmapper = 0. | LAV | Slett hele filen. Vurder ubrukte RESX-nøkler (ProgressionRate_*). |
| AdaptiveTargetZoneService (Services/SmartCoachModule/Ad | INGEN. Ingen 'new AdaptiveTargetZoneService' eller metodekall utenfor egen fil. Erstattet  | INGEN. Grep 'AdaptiveTargetZoneService' i begge testmapper = 0. | LAV | Slett kun tjeneste-filen; ikke modellklassen AdaptiveTargetZone. Resten kompilerer uendret. |
| VoiceFeminizationExerciseService + ResonanceModuleDocum | INGEN levende kode. VoiceFeminizationExerciseService refereres kun som parameter i Resonan | INGEN. Grep 'VoiceFeminizationExerciseService'/'ResonanceModuleDocumentation'/'E | LAV | Slett begge filene samtidig (ResonanceModuleDocumentation refererer VoiceFeminizationExerciseService/EnhancedE |

**Identifiserte blokkere (oppsummert):**
1. Audio-detektorene ← AnalysisSubsystem ← Infra-DI/ViewModelBase → **må gå i Bølge 6, ikke 5**
2. ProgressionEngine/WeeklyPlanner ← Infra-DI-registreringer → **2-linjers DI-edit i Bølge 5**
3. CoachMessageGenerator/VoiceProfileExtensions ← in-exe-tester i PRODUKSJONS-exe-en →
   **Bølge 2 (testsletting) må komme før Bølge 4/5 (klassene)** — ellers brekker exe-kompileringen omvendt vei
4. Subsystems-typene VoiceMetrics/ResonanceCategory ← 5+ levende filer → **typeuttrekk før sletting**

---

## Seksjon 4 – Merge Execution

### Merge 1: LevelClassificationSystem → ProgressionService-stien *(innsats 3/10, Bølge 4)*
- **Berørte filer:** `Services/LevelClassificationSystem.cs`,
  `Views/ProgressionDashboard.xaml.cs:49/55/233-235`, `ViewModels/ProgressionDashboardViewModel.cs:203`
  (sistnevnte slettes uansett i Bølge 3 — da gjenstår kun ProgressionDashboard.xaml.cs som call-site),
  portert testfil fra Bølge 2
- **Berørte metoder:** SLETT `Classify()` + instans-tilstand (`_database`, begge instanskonstruktører);
  BEHOLD de statiske `GetLevelName`/`GetLevelEmoji`/`GetLevelFocus` (aktive call-sites over)
- **Migrasjonssekvens:** (1) Bølge 2 porterer display-testene; (2) Bølge 3 fjerner den døde VM-call-siten;
  (3) Bølge 4: slett Classify+state, valgfritt omdøp klassen til `LevelDisplay` (fjerner
  «Classification»-villedningen — krever oppdatering av 4 call-sites + testfil); (4) kompiler + test
- **Resultat:** nivå-BESLUTNING bor utelukkende i ProgressionService; LevelClassificationSystem
  reduseres til ren display-mapper uten DB-avhengighet

### Merge 2: Infra/DependencyInjection → App.ConfigureServices *(innsats 3/10, = Bølge 6)*
- **Registreringer som fjernes:** samtlige i AddFemVoiceStudio (alle peker på døde systemer:
  5 subsystem-par, ProgressionEngine, WeeklyPlannerEngine, AnalysisSubsystemFactory,
  dobbeltregistrert ExerciseIntelligenceCoordinator)
- **Registreringer som bevares:** ingen migreres — App.ConfigureServices inneholder allerede alt
  som kjører (verifisert i integrasjonsauditen; ReleaseReadinessSmokeTests bygger containeren
  med ValidateOnBuild og er upåvirket — den reflekterer App.ConfigureServices, ikke Infra-fila)
- **Endelig DI-rot:** `App.xaml.cs ConfigureServices` — alene

---

## Seksjon 5 – Test Impact per bølge

| Bølge | Testpåvirkning | Handling |
|---|---|---|
| 1 | Ingen | — |
| 2 | **Krever migrering**: DirectionAnalyzerTests + LevelClassificationSystemTests (display-delen) har unik dekning av levende kode | Porter til FemVoiceStudio.Tests/ FØR sletting; FemVoiceScoreTests er duplikat (dekning bevart); CoachMessageGenerator-/VoiceProfileExtensionsTests dør med sine mål |
| 3 | **Krever testfjerning**: ExerciseFeedbackEngineTests.cs (hele fila, 13 tester på død motor) | Slett sammen med motoren |
| 4 | **Krever delvis testfjerning**: 2 BreathingRuleEvaluator-metoder i FeedbackSignalPolicyTests.cs | Fjern kun de to metodene + using; resten beholdes. Merge 1: portert testfil mister Classify-testene |
| 5 | **Krever delvis testfjerning**: PeriodizationServiceTests-klassen i SafetyLockTests.cs | Fjern kun den klassen; SafetyLockTests + ExternalSafetyBlockTests beholdes |
| 6 | Ingen direkte; evt. test-usings av Subsystems.Analysis oppdateres ved typeuttrekk | Kompileringssjekk fanger det |

---

## Seksjon 6 – Build Verification (sjekkpunkt etter HVER bølge)

1. **Bygg (Linux):** `dotnet build FemVoiceStudio -p:EnableWindowsTargeting=true` +
   `dotnet build FemVoiceStudio.Tests -p:EnableWindowsTargeting=true` → 0 errors/0 warnings
2. **Tester (Windows):** `dotnet test FemVoiceStudio.Tests` → alle grønne
   (antallet SYNKER planlagt: −13 ExerciseFeedbackEngine, −2 Breathing, −PeriodizationService-klassen;
   og ØKER med de porterte DirectionAnalyzer-/LevelDisplay-testene)
3. **Runtime-røyk (Windows):** MainWindow-opptak start/stopp · ExerciseWindow-økt ende-til-ende
   (live feedback + selvrapport-panel + mastery-badge) · SmartCoachDetailWindow åpner med data ·
   ProgressionWindow/dashboard viser nivå · ResonanceWindow-analyse · tema-bytte + språkbytte
4. **Git:** én commit per bølge, push etter grønt sjekkpunkt → enkel bisect ved regresjon

---

## Seksjon 7 – Risk Assessment

| Bølge | Risiko | Regresjonsrisiko | Hvorfor |
|---|---|---|---|
| 1 – Artefakter | Low | Low | Ingenting kompileres/kjøres |
| 2 – Tester | Low | **Medium** | Tap av unik dekning hvis porteringen hoppes over; csproj-endring |
| 3 – Død UI | Medium | Low | XAML/x:Class-koblinger; klynge må gå samlet |
| 4 – Tjenester + Merge 1 | Medium | Low-Medium | Delvise test-edits; Merge 1 endrer levende call-sites |
| 5 – Progresjon + audio | Medium | Low | Koblede DI-/test-edits; delte typer må bevares |
| 6 – Infrastruktur + Merge 2 | **High** | Medium | Typeuttrekk berører 5+ levende filer; alt-eller-ingenting-commit |

---

## Seksjon 8 – Expected Repository State

| Område | Før | Etter (estimat) |
|---|---|---|
| Services/ (.cs) | 75 | ~52 (−23: døde tjenester, motorer, config, ruleengine-mappe) |
| ViewModels/ (.cs) | 12 | 9 (−ExerciseListVM, −død ProgressionDashboardVM, −ViewModelBase) |
| Views/ (.cs / .xaml) | 21 / 17 | ~15 / 14 (−LiveFeedback, −ExerciseSummary, −SmartCoachDashboard, −PitchChartVM) |
| Audio/ (.cs) | 18 | 9 (−7 døde + 2 stubber) |
| Models/ (.cs) | 30 | ~28 (−4 døde, +1-2 uttrukne typer fra Subsystems) |
| Subsystems/ + Infra/ | 10 + 1 | **0 + 0** |
| In-exe-tester (FemVoiceStudio/Tests/) | 5 | **0** (xunit ute av exe-en) |
| .old-filer | 14 | **0** |
| DI-røtter | 2 (én død) | **1** |
| Estimert total reduksjon | | ~70 filer / grovt 15–20 k linjer |

---

## Seksjon 9 – Cleanup Execution Order (sprinter)

| Sprint | Innhold | Filer (ca.) | Innsats | Forventet gevinst |
|---|---|---|---|---|
| **1** | Bølge 1 + Bølge 2 (porter 2 testfiler først) | ~22 | Lav (timer) | Falske grep-treff borte; xunit ut av exe; ren csproj |
| **2** | Bølge 3 + Merge 1 | ~12 | Lav-middels | Død UI borte; én nivå-beslutningseier; navnekollisjon ProgressionDashboardVM løst |
| **3** | Bølge 4 + Bølge 5 | ~25 | Middels | Tre døde tjenestelag borte; progresjons-/audio-parallellstakkene borte |
| **4** | Bølge 6 + Merge 2 + sluttverifisering + oppdater arkitekturdokumentasjonen | ~14 | Middels-høy | Subsystems/Infra borte; én DI-rot; typene hjem i Models/ |

Hver sprint = egen commit+push etter grønt sjekkpunkt (Seksjon 6). Sprintene er uavhengig
shippbare; en pause mellom dem er trygg.

---

## Seksjon 10 – Exit Criteria

Oppryddingen er fullført når:
- [ ] Alle 47 DELETE-kandidater er fjernet (45 system + artefaktklynger)
- [ ] Begge MERGE-kandidater er gjennomført (LevelClassification-display konsolidert; én DI-rot)
- [ ] Løsningen bygger med 0 errors/0 warnings (begge prosjekter, EnableWindowsTargeting)
- [ ] Alle aktive tester passerer på Windows (inkl. de to porterte testfilene)
- [ ] Runtime-røyk grønn: MainWindow, ExerciseWindow, SmartCoach, Progression, Resonance
- [ ] Ingen død infrastruktur: `Subsystems/` og `Infra/` finnes ikke; ingen klasse uten produksjonssti
      gjenstår fra DELETE-listen
- [ ] Én DI-rot: kun `App.ConfigureServices`
- [ ] xunit/Test.Sdk er ute av FemVoiceStudio.csproj
- [ ] Arkitekturdokumentasjonen (FemVoice_Architecture_Documentation.md) oppdatert til ny tilstand
