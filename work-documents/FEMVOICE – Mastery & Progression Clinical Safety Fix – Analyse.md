# FEMVOICE – Mastery & Progression Clinical Safety Fix – Analyse

**Status:** Analysefase — ingen kode endret ennå.
**Dato:** 2026-06-06
**Metode:** Full kodelesing av de 6 utpekte filene + ExerciseDataService/ProgressionService/ExerciseWindow, deretter parallell sweep av samtlige 18 nivå-/mastery-/progresjonsbeslutningspunkter i kodebasen, og adversarial verifisering av 8 nøkkelpåstander (hver påstand forsøkt motbevist av uavhengig agent med fil:linje-bevis). 7 av 8 bekreftet, 1 presisert.

---

## 1. Full analyse av dagens logikk

### 1.1 Mastery-kjeden (aktiv i prod)

```
ExerciseWindow.OnStopClick (ExerciseWindow.xaml.cs:354)
  └─ CalculateScore() (ExerciseWindow.xaml.cs:608-615)
       score = min(100, elapsed/target*100) + min(20, TotalSessions*2)   ← KUN TID + OPPMØTE
  └─ ExerciseDataService.CompleteSession (ExerciseDataService.cs:172)
       └─ UpdateExerciseProgress (ExerciseDataService.cs:744-801)
            AverageScore = (AverageScore*TotalSessions + score)/(TotalSessions+1)
  ⇣ (neste gang øvelsen åpnes)
ExerciseDetailViewModel.ApplyProfile (ExerciseDetailViewModel.cs:216-227)
  └─ ComputeMasteryFromProgress (ExerciseDetailViewModel.cs:562-569)
       < 3 økter → Beginner | avg < 60 → Developing | avg < 80 → Stable | ellers → Mastered
  └─ MasteryLabelKey → XAML-badge (ExerciseWindow.xaml:859 — ENESTE binding; MasteryProgressPercent/
     CompletedSessionCount/AverageQualityLabelKey er IKKE bundet noe sted — verifisert)
```

**Kritisk realitet (verifisert):** Scoren som driver Mastery inneholder **ingen stemmemetrikker overhodet**. `CalculateScore()` er ren tid + oppmøtebonus. Stemmedataene som faktisk beregnes live (resonans/pitch/stabilitet i `OnExerciseAudioDataAvailable`, ExerciseWindow.xaml.cs:714-741) går kun til UI-visning og når aldri den lagrede scoren. **«Mastered» oppnås i dag ved å la timeren gå i nok økter — uten å si et ord.** Dette er verre enn prompt-beskrivelsen antyder: høy score krever ikke engang høye tall, bare tilstedeværelse.

### 1.2 Sikkerhetssystemene — fire lag, alle frakoblet i øvelsesløkka (verifisert)

| Lag | Designet rolle | Faktisk prod-status |
|---|---|---|
| `health`-parameter til koordinatoren | `< 70` → safety-lock (Coordinator.cs:468) | **Hardkodet 100** i eneste kallsti (ExerciseWindow.xaml.cs:736-740) — gaten kan aldri utløses |
| `ComfortZoneController` | Adaptiv pitch-sone + `IsSafetyLocked` via `ZoneUpdated` | DI-registrert (App.xaml.cs:106), koordinatoren abonnerer (Coordinator.cs:99) — men `UpdateZoneAsync`/`RecordStrainIncidentAsync`/`InitializeAsync` **kalles aldri i prod** → `ZoneUpdated` fyres aldri. `IsInComfortZone` beregnes i praksis kun mot statiske profilgrenser seedet i `UpdateMetrics` (Coordinator.cs:282-285) |
| `VoiceHealthMonitor` (legacy-bridge) | `HealthWarning/Critical/Lockout` → lock via `VocalHealthLegacyBridge` | Events reises kun inne i `Analyze(VoiceMetrics)` (VoiceHealthMonitor.cs:116) — **som ingen prod-kode kaller** |
| `VocalHealthSupervisor` | Strain-/fatigue-/hydreringstrender fra `ExerciseLiveState`, eskalering Normal→Caution→Restrict→Lock | DI-registrert (App.xaml.cs:146) — **`Evaluate()` har null prod-kallsteder**, ingen prod-abonnenter på noen av de 7 eventene |

**Konsekvens:** `IsSafetyLocked` kan i praksis **aldri bli true** i den faktiske øvelses-lydstien. Alle koordinatorens gjennomarbeidede safety-mekanismer (frys av HoldProgress, Quality=Poor, undertrykt positiv coaching) er testdekket men reelt utilgjengelige.

### 1.3 Progresjonskjeden (forsiden, aktiv i prod)

```
MainViewModel.StopRecording (MainViewModel.cs:374-392)
  session.OverallScore = analysis.OverallScore   (:384)
       ← AudioAnalyzerService.CalculateSessionAnalysis (AudioAnalyzerService.cs:113-167)
         tilordner ALDRI OverallScore → alltid 0.0
  └─ ProgressionService.EvaluateProgression(session)   (:392 — IKKE EvaluateProgressionWithSafety)
       promotering krever OverallScore >= 75 (ProgressionService.cs:97, konstant :20)
       safety-gate (:87) sjekker _safetyLockActive — som KUN settes av RecordStrainIncident (:392→:430)
       — og RecordStrainIncident kalles aldri i prod (kun SafetyLockTests)
```

**Promoteringsstien er dobbelt brutt (verifisert):**
1. `session.OverallScore` er **alltid 0** (0 ≥ 75 alltid usant) → promotering skjer aldri — men *degradering* kan skje (snitt < 50 siste 7 dager, :126-150), også den basert på samme alltid-0-score → **enhver bruker over Nybegynner degraderes** ved 3+ økter på 7 dager.
2. Safety-låsen er død: `_safetyLockActive` alltid false, `EvaluateProgressionWithSafety` (:468) har null prod-kallere, ingen abonnerer på `SafetyLockEngaged/Released`. `TrainingSession.VoiceHealthScore`/`StrainLevel` (TrainingSession.cs:27,30) finnes men settes aldri.

### 1.4 Den «riktige» arkitekturen finnes allerede — den er bare aldri koblet til

Sweepen fant at kodebasen **allerede inneholder** nesten alt prompten ber om, fullt testdekket, men dødt:

- **`SessionAnalyticsStore`** (SessionAnalyticsStore.cs:566) med `ExercisePerformanceSummary` som har *nøyaktig* de riktige feltene: `ResonanceQualityIndex`, `StabilityConsistency`, `HoldCompletionRate`, `SafetyEventsCount`, `FatigueIndicators` (:41-54). SQLite-skjema med indekser (:292-349). **Ingen `Record*`-metode kalles i prod** → tabellene er alltid tomme.
- **`ProgressionOrchestrator`** (ProgressionOrchestrator.cs:99-242) implementerer *eksakt* promptens stoppregler: `SAFETY_EVENTS`-regresjon (:146), `FATIGUE_RISING`-pause (:162), `PERFORMANCE_REGRESSION` (:178), subjektiv helsebekymring → pause (:285), og «resonans før pitch»-rekkefølge (:340-384). DI-registrert (App.xaml.cs:137), **null konsumenter**, og datakilden (analytics) er tom.
- **`VocalHealthSupervisor`** (VocalHealthSupervisor.cs:77) gjør micro/meso-trendanalyse av `ExerciseLiveState` med strain/fatigue-scoring og komfortbrudd-telling (:151) — designet for å mates per tick. Aldri kalt.

**Hovedstrategien i fiksen er derfor *wiring og gating* — ikke nybygging.**

### 1.5 Komplett kart: alle 18 beslutningspunkter (fra sweep)

**Aktive (4 reelle motorer):**
| # | Beslutning | Fil:linje | Gater i dag |
|---|---|---|---|
| 1 | `ProgressionService.EvaluateProgression` — DifficultyLevel-promotering/degradering | ProgressionService.cs:70 | Ingen reelle (safety-gren død, score alltid 0) |
| 2 | `ExerciseDetailViewModel.ComputeMasteryFromProgress` — Mastery-badge | ExerciseDetailViewModel.cs:562 | Ingen |
| 3 | `ExerciseIntelligenceCoordinator` — live Quality/hold/lock | ExerciseIntelligenceCoordinator.cs:445 | Sterke gater, men alle inputkilder døde (§1.2) |
| 4 | `FemVoiceScore.Calculate` + `AdaptiveComfortZoneService` — live-score forsiden + SessionType | FemVoiceScore.cs:93, AdaptiveComfortZoneService.cs:154 | Har «resonans før pitch»-straff og helsegate — eneste fungerende kliniske gate i appen |

**Døde med relevant gate-logikk:** `ProgressionService.EvaluateProgressionWithSafety` (:468), `ProgressionOrchestrator` (:99), `LevelClassificationSystem.Classify` (:225 — har strain-gate + 14-dagers sperre, kalles aldri), `ComplexityEngine.TryAdvanceLevelAsync` (kun lese-stien er aktiv via SmartCoachDetailWindow), `ProgressionEngine`/`WeeklyPlannerEngine`/`ProgressionSubsystem` (hele `AddFemVoiceStudio`-laget kalles aldri), `SmartCoachExerciseAdapter.CalculateUserLevel` (død + ctor-stub gjør den alltid-Nybegynner), `GamificationService`, `PeriodizationService` (eget, separat safety-lock-system, dødt), `SmartCoachEngine.AnalyzeSessionForStrain` (ville dessuten krasje pga. kjent IsRead-skjemabug).

---

## 2. Liste over alle kliniske svakheter

**Kategori A — Mastery belønner feil ting:**
- **A1.** Øktscore er ren tid+oppmøte (`ExerciseWindow.xaml.cs:608-615`); ingen stemmemetrikk inngår. Mastered = nok økter med timer på.
- **A2.** `experienceBonus` (+2 per økt, maks 20) gjør at score *stiger automatisk* med antall økter — systematisk inflasjon av AverageScore uavhengig av ferdighet.
- **A3.** Mastery (`ExerciseDetailViewModel.cs:562-569`) sjekker hverken resonanskonsistens, stabilitet, komfortsone-historikk, safety-lock-historikk eller fatigue — i strid med prompt-kravene og klinisk praksis.
- **A4.** Ingen demotion/karantene: en bruker med ferske safety-hendelser beholder «Mastered».

**Kategori B — Sikkerhetskjeden er fysisk frakoblet:**
- **B1.** `health` hardkodet 100 (`ExerciseWindow.xaml.cs:736-740`) → `<70`-gaten (Coordinator.cs:468) død.
- **B2.** `ComfortZoneController` aldri initialisert/fôret → adaptiv komfortsone og dens safety-lock død; sone = statiske profilgrenser.
- **B3.** `VoiceHealthMonitor.Analyze` aldri kalt → legacy-helseevents aldri fyrt.
- **B4.** `VocalHealthSupervisor.Evaluate` aldri kalt → strain-/fatigue-/hydreringsdeteksjon død.
- **B5.** Ingen persistens av komfortbrudd, safety-locks eller strain per økt/sesjon (verifisert: `ExerciseSessions`-tabellen har kun Score/Duration/Completed/Notes; `TrainingSessions`-INSERT mangler helsefelt) → «no recent safety locks»-gater har ingen datakilde.

**Kategori C — Progresjon ugatet og delvis ødelagt:**
- **C1.** `MainViewModel.cs:392` bruker usikret `EvaluateProgression`; `WithSafety`-varianten har null kallere.
- **C2.** `RecordStrainIncident` aldri kalt → `_safetyLockActive` alltid false → safety-grenen (:87) død.
- **C3.** `session.OverallScore` alltid 0 (`MainViewModel.cs:384` ← `CalculateSessionAnalysis` som aldri setter feltet) → promotering umulig OG degradering feilutløses.
- **C4.** `TrainingSession.VoiceHealthScore`/`StrainLevel` settes aldri → selv en reparert gate ville manglet input.
- **C5.** Safety-lock-tilstand er in-memory (kommentar `ProgressionService.cs:32` innrømmer det) → overlever ikke restart; «repeated safety locks» kan per definisjon ikke spores.
- **C6.** `ProgressionOrchestrator.BuildProgressionDecision:340`: `avgResonance < min || profile.UsesResonance` — `||` skal være `&&`; enhver resonansprofil tar alltid RESONANCE_FIRST-grenen (relevant straks orkestratoren aktiveres).
- **C7.** Subjektiv rapport (`ExerciseWindow.xaml.cs:629`, `_lastSubjectiveReport`) samles inn men **leses aldri** — brukerens egen strain-/komfortrapport ignoreres.

**Kategori D — Pitch-prioritering (klinisk regel «pitch alene skal aldri…»):**
- **D1.** `DisplayQuality`-composite (`ExerciseDetailViewModel.cs:837-848`) = `0.65*primary + 0.35*stability`. For pitch-primære profiler er `primary` = normalisert pitch-*posisjon* (Coordinator.cs:482) → **høyere pitch i sonen gir høyere kvalitet**. (Samme funn som «Quality Formula Findings»-auditen, fortsatt ufikset.)
- **D2.** I dag kan pitch riktignok ikke låse opp Mastery (fordi ingenting kan — A1), men uten eksplisitt gate vil en naiv «koble stemmescore til mastery»-fix gjøre D1 til en mastery-driver. Fiksen må derfor bygge inn pitch-nøytralitet eksplisitt.

---

## 3. Ny arkitektur — designprinsipp

**Gjenbruk de døde, testdekkede komponentene; bygg minst mulig nytt:**

```
                       (NY) ExerciseSessionRecorder  ── per-økt-aggregering
ExerciseIntelligence ──► abonnerer ExerciseUpdated ──► VocalHealthSupervisor.Evaluate(state)  [VEKKES]
Coordinator                │                                │ strain/fatigue/state
                           │ akkumulerer:                   ▼
                           │ resonans/stabilitet/komfort/  health-score → ExerciseWindow
                           │ lock-episoder/holdProgress     (erstatter hardkodet 100)
                           ▼
              ExerciseSessionOutcome ──► ClinicalSessionScore (NY, erstatter CalculateScore)
                           │
                           └─► SessionAnalyticsStore.RecordExercisePerformanceAsync /
                               RecordHealthEventAsync   [VEKKES — persistens av safety/komfort/fatigue]
                                        │
              ┌─────────────────────────┴──────────────────────────┐
              ▼                                                    ▼
   (NY) MasteryEvaluator                              (NY) ProgressionSafetyGate
   konsistens + komfort + safety + fatigue + antall   leser HealthEvents/summaries siste 7-14 d
              │                                                    │
              ▼                                                    ▼
   ExerciseDetailViewModel (badge)                    MainViewModel → ProgressionService
                                                      (WithSafety + RecordStrainIncident-wiring)
```

Arkitekturkravene fra Systemkontekst.md respekteres: koordinatoren røres ikke (forblir UI-agnostisk), all konfig er datastyrt via profil, ingen per-øvelse-logikk, ingen rå tall i UI (Mastery-badge bruker samme RESX-nøkler), presentasjon blir i ViewModel.

**Gate-regler (harde gater, ikke vekter):**

| Nivå | Krav |
|---|---|
| Beginner | `< 3` økter (uendret) |
| Developing | default |
| Stable | `≥ 8` økter **og** snittresonans(siste 10) ≥ `profile.TargetResonanceMin` **og** snittstabilitet ≥ `profile.StabilityThreshold` **og** 0 safety-locks siste 7 d **og** komfort-compliance ≥ 0,70 |
| Mastered | `≥ 20` økter **og** resonans-/stabilitetskrav som Stable **og** 0 safety-locks siste 14 d **og** maks 2 av siste 10 økter med komfortbrudd **og** fatigue-trend ikke stigende (uke-over-uke) |
| Demotion | Safety-lock siste 7 d → maks `Developing` uansett score |
| Datamangel | Uten analytics-historikk (eldre data) → maks `Stable`, aldri `Mastered` (konservativ default) |

**Progresjon stoppes** (før promotering i `ProgressionService`) hvis siste 7 d har: ≥ 2 SafetyFreeze-events, **eller** stigende StrainPeriod-trend, **eller** stigende FatigueIndicator-trend, **eller** ≥ 3 økter med komfortbrudd — uavhengig av score.

**Pitch-nøytralitet (eksplisitt):** pitch inngår i øktscore og mastery **kun** som komfort-compliance (andel tid *innenfor* sonen, 0–1) — aldri som posisjon/høyde. D1 fikses ved at composite for pitch-primære profiler bruker sone-adherence i stedet for `primary`.

---

## 4. Eksakte filer og linjer som må endres

| # | Fil | Linjer | Endring |
|---|---|---|---|
| 1 | `FemVoiceStudio/Services/ExerciseSessionRecorder.cs` | **NY** | Per-økt-aggregator; vekker supervisor + analytics |
| 2 | `FemVoiceStudio/Services/ClinicalSessionScore.cs` | **NY** | Klinisk øktscore med harde gater |
| 3 | `FemVoiceStudio/Services/MasteryEvaluator.cs` | **NY** | Gated mastery-beregning (ren, testbar) |
| 4 | `FemVoiceStudio/Services/ProgressionSafetyGate.cs` | **NY** | Stateless gate over persistert helsehistorikk |
| 5 | `FemVoiceStudio/Models/MasteryLevel.cs` | **NY** | Enum flyttes hit fra VM (kun VM-intern i dag — verifisert trygt) |
| 6 | `FemVoiceStudio/App.xaml.cs` | ~137–146 | DI: recorder, evaluator, gate (singletons) |
| 7 | `FemVoiceStudio/Views/ExerciseWindow.xaml.cs` | 326, 354–358, 375–389, 608–615, 736–740 | Start/stop → recorder; `CalculateScore` erstattes; health fra recorder; subjektiv rapport → recorder |
| 8 | `FemVoiceStudio/ViewModels/ExerciseDetailViewModel.cs` | 96, 122–124, 216–227, 562–569 | Mastery via `MasteryEvaluator` (async), enum-flytt |
| 9 | `FemVoiceStudio/ViewModels/MainViewModel.cs` | 384, 392 | Reell `OverallScore`; gate + `EvaluateProgressionWithSafety` + `RecordStrainIncident`-wiring |
| 10 | `FemVoiceStudio/Services/ProgressionService.cs` | 430 (ny offentlig metode ved siden av) | `ApplyExternalSafetyBlock(reason, days)` — offentlig inngang til eksisterende lås |
| 11 | `FemVoiceStudio/Services/ProgressionOrchestrator.cs` | 340 | Bugfix `\|\|` → `&&` |
| 12 | `FemVoiceStudio.Tests/` | nye filer + SafetyLockTests | Se regresjonssjekkliste |

Bevisst **ikke** rørt: `ExerciseIntelligenceCoordinator.cs` (kun konsument av eksisterende events), `ExerciseLiveState.cs` (har alt vi trenger), `SessionAnalyticsStore.cs`, `VocalHealthSupervisor.cs` (vekkes som de er), RESX (gjenbruker eksisterende nøkler).

---

## 5. Patch-forslag

### Patch 1 — `ExerciseSessionRecorder.cs` (NY)

```csharp
namespace FemVoiceStudio.Services
{
    /// <summary>Per-økt-aggregering av ExerciseLiveState → klinisk øktresultat + analytics-persistens.
    /// Vekker VocalHealthSupervisor (strain/fatigue) og SessionAnalyticsStore (historikk-gater).</summary>
    public sealed record ExerciseSessionOutcome
    {
        public double AverageResonance { get; init; }
        public double AverageStability { get; init; }
        public double ComfortCompliance { get; init; }   // andel ticks i sone, 0–1
        public double HoldCompletion { get; init; }       // maks HoldProgress
        public int    SafetyLockEpisodes { get; init; }   // false→true-transisjoner
        public int    ComfortBreachEpisodes { get; init; }
        public int    FatigueIndicators { get; init; }
        public int    StrainDetections { get; init; }
        public int    EvaluatedTicks { get; init; }
    }

    public sealed class ExerciseSessionRecorder : IDisposable
    {
        private readonly ExerciseIntelligenceCoordinator _coordinator;
        private readonly VocalHealthSupervisor _healthSupervisor;
        private readonly SessionAnalyticsStore _analyticsStore;
        private readonly object _lock = new();

        private bool _recording;
        private int _exerciseId, _sessionId, _userId = 1;
        private DateTime _startedAt;
        private double _resonanceSum, _stabilitySum, _maxHold;
        private int _ticks, _ticksInZone, _lockEpisodes, _breachEpisodes, _fatigue, _strain;
        private bool _wasLocked, _wasInZone = true;
        private double _currentHealthScore = 100;

        public ExerciseSessionRecorder(
            ExerciseIntelligenceCoordinator coordinator,
            VocalHealthSupervisor healthSupervisor,
            SessionAnalyticsStore analyticsStore)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _healthSupervisor = healthSupervisor ?? throw new ArgumentNullException(nameof(healthSupervisor));
            _analyticsStore = analyticsStore ?? throw new ArgumentNullException(nameof(analyticsStore));
            _coordinator.ExerciseUpdated += OnExerciseUpdated;
        }

        /// <summary>Health 0–100 avledet av supervisor-tilstand. Kun Lock går under
        /// koordinatorens 70-terskel — Caution/Restrict advarer uten å låse.</summary>
        public double CurrentHealthScore { get { lock (_lock) return _currentHealthScore; } }

        public void BeginSession(int exerciseId, int sessionId, int userId = 1)
        {
            lock (_lock)
            {
                _exerciseId = exerciseId; _sessionId = sessionId; _userId = userId;
                _startedAt = DateTime.Now;
                _resonanceSum = _stabilitySum = _maxHold = 0;
                _ticks = _ticksInZone = _lockEpisodes = _breachEpisodes = _fatigue = _strain = 0;
                _wasLocked = false; _wasInZone = true;
                _currentHealthScore = 100;
                _healthSupervisor.Reset();          // ny økt = friskt trend-vindu
                _recording = true;
            }
        }

        private void OnExerciseUpdated(ExerciseLiveState state)
        {
            VocalHealthDecision decision;
            lock (_lock)
            {
                if (!_recording) return;
                _ticks++;
                _resonanceSum += state.PrimaryMetricScore;
                _stabilitySum += state.StabilityScore;
                _maxHold = Math.Max(_maxHold, state.HoldProgress);
                if (state.IsInComfortZone) _ticksInZone++;
                if (state.IsSafetyLocked && !_wasLocked) _lockEpisodes++;
                if (!state.IsInComfortZone && _wasInZone) _breachEpisodes++;
                _wasLocked = state.IsSafetyLocked;
                _wasInZone = state.IsInComfortZone;
            }

            decision = _healthSupervisor.Evaluate(state);   // utenfor _lock — supervisor har egen lås

            lock (_lock)
            {
                if (decision.FatigueDetected) _fatigue++;
                if (decision.StrainDetected) _strain++;
                _currentHealthScore = decision.State switch
                {
                    HealthSafetyState.Lock     => 40,   // → koordinator-lås (<70)
                    HealthSafetyState.Restrict => 72,   // advarsel, IKKE lås (se §7-merknad)
                    HealthSafetyState.Caution  => 85,
                    _                          => 100
                };
            }
        }

        public ExerciseSessionOutcome CompleteSession()
        {
            ExerciseSessionOutcome outcome;
            int sessionId, exerciseId, userId, strain, locks;
            DateTime startedAt;
            lock (_lock)
            {
                _recording = false;
                outcome = new ExerciseSessionOutcome
                {
                    AverageResonance     = _ticks > 0 ? _resonanceSum / _ticks : 0,
                    AverageStability     = _ticks > 0 ? _stabilitySum / _ticks : 0,
                    ComfortCompliance    = _ticks > 0 ? (double)_ticksInZone / _ticks : 0,
                    HoldCompletion       = _maxHold,
                    SafetyLockEpisodes   = _lockEpisodes,
                    ComfortBreachEpisodes= _breachEpisodes,
                    FatigueIndicators    = _fatigue,
                    StrainDetections     = _strain,
                    EvaluatedTicks       = _ticks
                };
                (sessionId, exerciseId, userId, strain, locks, startedAt) =
                    (_sessionId, _exerciseId, _userId, _strain, _lockEpisodes, _startedAt);
            }

            _ = PersistAsync(outcome, sessionId, exerciseId, userId, strain, locks, startedAt);
            return outcome;
        }

        public void AbortSession() { lock (_lock) _recording = false; }

        private async Task PersistAsync(ExerciseSessionOutcome o, int sessionId, int exerciseId,
            int userId, int strain, int locks, DateTime startedAt)
        {
            try
            {
                await _analyticsStore.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId = sessionId, UserId = userId, ExerciseId = exerciseId,
                    StartedAt = startedAt, EndedAt = DateTime.Now,
                    HoldCompletionRate = o.HoldCompletion,
                    ResonanceQualityIndex = o.AverageResonance,
                    StabilityConsistency = o.AverageStability,
                    SafetyEventsCount = locks,
                    FatigueIndicators = o.FatigueIndicators
                });
                for (var i = 0; i < locks; i++)
                    await _analyticsStore.RecordHealthEventAsync(new HealthAnalyticsEvent
                    {
                        SessionId = sessionId, UserId = userId,
                        EventType = HealthAnalyticsEventType.SafetyFreeze,
                        OccurredAt = DateTime.Now, Severity = 1.0, ReasonCode = "EXERCISE_SAFETY_LOCK"
                    });
                if (strain > 0)
                    await _analyticsStore.RecordHealthEventAsync(new HealthAnalyticsEvent
                    {
                        SessionId = sessionId, UserId = userId,
                        EventType = HealthAnalyticsEventType.StrainPeriod,
                        OccurredAt = DateTime.Now,
                        Severity = Math.Clamp(strain / 10.0, 0, 1), ReasonCode = "STRAIN_DETECTED"
                    });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Analytics persist failed: {ex.Message}"); }
        }

        public void Dispose() => _coordinator.ExerciseUpdated -= OnExerciseUpdated;
    }
}
```

### Patch 2 — `ClinicalSessionScore.cs` (NY)

```csharp
namespace FemVoiceStudio.Services
{
    /// <summary>Klinisk øktscore (0–100). Tid er gulvkrav, ikke poengkilde.
    /// Pitch inngår KUN via komfort-compliance — aldri via pitchhøyde.</summary>
    public static class ClinicalSessionScore
    {
        public static double Calculate(
            ExerciseSessionOutcome outcome,
            ExerciseTargetProfile profile,
            int elapsedSeconds,
            int targetSeconds)
        {
            if (outcome.EvaluatedTicks == 0) return 0;   // ingen stemmedata = ingen score

            double weighted = 0, weight = 0;
            if (profile.UsesResonance) { weighted += outcome.AverageResonance  * 0.45; weight += 0.45; }
            if (profile.UsesStability) { weighted += outcome.AverageStability  * 0.25; weight += 0.25; }
            if (profile.UsesPitch)     { weighted += outcome.ComfortCompliance * 0.20; weight += 0.20; }
            if (profile.RequiredHoldSeconds > 0) { weighted += outcome.HoldCompletion * 0.10; weight += 0.10; }
            var score = weight > 0 ? (weighted / weight) * 100 : 0;

            // Harde kliniske gater — caps, ikke fratrekk:
            if (outcome.SafetyLockEpisodes > 0)       score = Math.Min(score, 40);
            if (outcome.ComfortCompliance < 0.5 && profile.UsesPitch)
                                                      score = Math.Min(score, 55);
            if (targetSeconds > 0 && elapsedSeconds < targetSeconds / 2)
                                                      score = Math.Min(score, 30);   // tid som gulv
            return Math.Clamp(score, 0, 100);
        }
    }
}
```

### Patch 3 — `ExerciseWindow.xaml.cs`

```csharp
// :326  OnStartClick — etter StartSession:
_currentSessionId = _exerciseService.StartSession(_currentExercise.ExerciseId);
_sessionRecorder?.BeginSession(_currentExercise.ExerciseId, _currentSessionId);

// :354-355  OnStopClick — erstatt CalculateScore():
var outcome = _sessionRecorder?.CompleteSession();
var score = outcome != null && _activeProfile != null
    ? ClinicalSessionScore.Calculate(outcome, _activeProfile, _elapsedSeconds,
        _currentExercise.DurationMinutes * 60)
    : 0;
_exerciseService.CompleteSession(_currentSessionId, _elapsedSeconds, score, "");

// :382  StopInternalExercise — legg til:
_sessionRecorder?.AbortSession();

// :608-615  CalculateScore() — SLETTES (erstattet av ClinicalSessionScore)

// :736-740  health 100 → reell helse:
_viewModel.UpdateLiveMetrics(
    resonance,
    pitch.IsVoiced ? pitch.Pitch : 0,
    stability,
    _sessionRecorder?.CurrentHealthScore ?? 100);
```
(`_sessionRecorder` hentes fra `App.Services` ved init, samme mønster som `ResonanceProxyEngine` på :667.)

### Patch 4 — `MasteryEvaluator.cs` (NY)

```csharp
namespace FemVoiceStudio.Services
{
    public sealed record MasteryEvaluation
    {
        public Models.MasteryLevel Level { get; init; }
        public string ReasonCode { get; init; } = "";   // f.eks. "GATE_SAFETY_RECENT_LOCK" — til logging, ikke UI
    }

    public sealed class MasteryEvaluator
    {
        private readonly SessionAnalyticsStore _analytics;
        public MasteryEvaluator(SessionAnalyticsStore analytics)
            => _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));

        public async Task<MasteryEvaluation> EvaluateAsync(
            int exerciseId, int totalSessions, ExerciseTargetProfile profile,
            DateTime now, CancellationToken ct = default)
        {
            if (totalSessions < 3)
                return new() { Level = Models.MasteryLevel.Beginner, ReasonCode = "SESSION_COUNT" };

            var trend = await _analytics.GetExerciseTrendAsync(exerciseId, now.AddDays(-28), now, 1, ct);
            var recent = trend.TakeLast(10).ToList();
            var locks7d  = trend.Where(s => s.StartedAt >= now.AddDays(-7)).Sum(s => s.SafetyEventsCount);
            var locks14d = trend.Where(s => s.StartedAt >= now.AddDays(-14)).Sum(s => s.SafetyEventsCount);

            // Demotion-gate: fersk safety-lock overstyrer ALT.
            if (locks7d > 0)
                return new() { Level = Models.MasteryLevel.Developing, ReasonCode = "GATE_SAFETY_RECENT_LOCK" };

            // Konservativ default ved datamangel: aldri Mastered uten verifisert konsistens.
            if (recent.Count < 5)
                return new() { Level = Models.MasteryLevel.Developing, ReasonCode = "INSUFFICIENT_HISTORY" };

            var avgRes  = recent.Average(s => s.ResonanceQualityIndex);
            var avgStab = recent.Average(s => s.StabilityConsistency);
            var resOk   = !profile.UsesResonance || avgRes  >= profile.TargetResonanceMin;
            var stabOk  = !profile.UsesStability || avgStab >= profile.StabilityThreshold;

            var firstHalf  = recent.Take(recent.Count / 2).Sum(s => s.FatigueIndicators);
            var secondHalf = recent.Skip(recent.Count / 2).Sum(s => s.FatigueIndicators);
            var fatigueRising = secondHalf > firstHalf && secondHalf >= 3;
            var breachSessions = recent.Count(s => s.SafetyEventsCount > 0);

            if (totalSessions >= 20 && resOk && stabOk
                && locks14d == 0 && breachSessions <= 2 && !fatigueRising)
                return new() { Level = Models.MasteryLevel.Mastered, ReasonCode = "ALL_GATES_PASSED" };

            if (totalSessions >= 8 && resOk && stabOk)
                return new() { Level = Models.MasteryLevel.Stable, ReasonCode = "CONSISTENT" };

            return new() { Level = Models.MasteryLevel.Developing, ReasonCode = "BUILDING" };
        }
    }
}
```

### Patch 5 — `ExerciseDetailViewModel.cs`

```csharp
// :96  enum flyttes til Models/MasteryLevel.cs (identisk innhold; ingen XAML-endring nødvendig
//      — kun MasteryLabelKey er bundet, verifisert mot alle .xaml-filer)

// :216-227  ApplyProfile — erstatt synkron mastery-load:
if (exerciseId.HasValue)
{
    EnsureProgressService();
    var id = exerciseId.Value;
    _ = LoadMasteryAsync(id);   // async — blokkerer ikke UI-tråden slik dagens sync DB-kall gjør
}

// Ny privat metode (erstatter ComputeMasteryFromProgress :562-569):
private async Task LoadMasteryAsync(int exerciseId)
{
    try
    {
        var prog = _progressService?.GetExerciseProgress(exerciseId);
        _completedSessions   = prog?.TotalSessions ?? 0;
        _averageSessionScore = prog?.AverageScore ?? 0;
        var eval = _masteryEvaluator != null
            ? await _masteryEvaluator.EvaluateAsync(exerciseId, _completedSessions, _activeProfile, DateTime.Now)
            : new MasteryEvaluation { Level = Models.MasteryLevel.Beginner };
        void Apply()
        {
            _mastery = eval.Level;
            OnPropertyChanged(nameof(MasteryLabelKey));
            OnPropertyChanged(nameof(MasteryProgressPercent));
            OnPropertyChanged(nameof(CompletedSessionCount));
            OnPropertyChanged(nameof(AverageQualityLabelKey));
        }
        if (Application.Current?.Dispatcher.CheckAccess() == false)
            Application.Current.Dispatcher.BeginInvoke((Action)Apply);
        else Apply();
    }
    catch { /* behold forrige mastery ved feil — aldri oppgrader på feil */ }
}
```
(`_masteryEvaluator` injiseres som valgfri ctor-param, samme mønster som `_feedbackPipeline` på :104-105/148-149 — eksisterende tester med 2-args-ctor kompilerer uendret.)

### Patch 6 — `MainViewModel.cs` + `ProgressionService.cs`

```csharp
// MainViewModel :384 — reell score i stedet for alltid-0:
OverallScore = feedbackCollection.OverallScore,   // FemVoiceScore-basert (samme verdi som UI viser, :371)

// MainViewModel :392 — gate før evaluering:
var gate = await _progressionSafetyGate.EvaluateAsync(DateTime.Now);   // leser HealthEvents siste 7-14 d
if (gate.IsBlocked)
    _progressionService.ApplyExternalSafetyBlock(gate.ReasonCode, gate.RecommendedRestDays);
if (_lastScoreResult?.WarningFlags?.Any(f => f.Contains("STRAIN")) == true)
    _progressionService.RecordStrainIncident((int)_lastScoreResult.StrainLevel);
var progressionResult = _progressionService.EvaluateProgressionWithSafety(session);

// ProgressionService — ny offentlig metode (gjenbruker eksisterende private ApplySafetyLock :430):
public void ApplyExternalSafetyBlock(string reason, int days = 2)
{
    _safetyLockExpires = DateTime.Now.AddDays(days);
    ApplySafetyLock(reason);
}
```

`ProgressionSafetyGate` (NY, ~60 linjer): leser `GetHealthEventsAsync(userId, now-14d, now)` + `GetExerciseSummariesAsync` og returnerer `IsBlocked` hvis ≥ 2 SafetyFreeze siste 7 d, stigende StrainPeriod/Fatigue-trend uke-over-uke, eller ≥ 3 økter med komfortbrudd siste 7 d. **Stateless** — beregnes fra persistert historikk hver gang, og løser dermed C5 (in-memory-lås) uten ny lagringsmekanisme.

### Patch 7 — `ProgressionOrchestrator.cs:340` (bugfix, forberedelse til fase 2)

```csharp
// FØR:  if (avgResonance < _options.MinimumStableResonance || profile.UsesResonance)
// ETTER:
if (profile.UsesResonance && avgResonance < _options.MinimumStableResonance)
```

### Patch 8 — `App.xaml.cs` (DI, ved ~:137-146)

```csharp
services.AddSingleton<ExerciseSessionRecorder>();
services.AddSingleton<MasteryEvaluator>();
services.AddSingleton<ProgressionSafetyGate>();
```

---

## 6. Regression checklist

**Eksisterende tester som SKAL bestå uendret:**
- [ ] `ExerciseIntelligenceCoordinatorTests` (32) — koordinatoren er urørt
- [ ] `InlineCoachPolicyTests` (27) — coaching-stien urørt
- [ ] `VocalHealthSupervisorTests` — supervisor urørt (kun nye kallere)
- [ ] `SessionAnalyticsStoreTests` (13) — store urørt (kun nye kallere)
- [ ] `ComfortZoneControllerTests` (17), `FemVoiceScoreEngineTests` (25), `FeedbackConsistencyGuardTests` (21)
- [ ] `ProgressionOrchestratorTests` (11) — **OBS:** Patch 7 endrer `BuildProgressionDecision`-oppførsel; tester som asserter RESONANCE_FIRST for resonansprofiler med *god* resonans vil (riktig) begynne å feile og må oppdateres til ny forventet gren
- [ ] `SafetyLockTests` — `ApplyExternalSafetyBlock` er additiv; eksisterende `RecordStrainIncident`-tester upåvirket
- [ ] `ExerciseDetailViewModel`-relaterte tester — 2-args-ctor består (evaluator er valgfri param)

**Nye tester som må skrives:**
- [ ] `ExerciseSessionRecorderTests`: aggregering (snitt/transisjoner/maks-hold), lock-episodetelling (false→true-kanter, ikke nivå), comfort-breach-kanter, Reset av supervisor ved BeginSession, AbortSession persisterer ikke, health-mapping (Lock→40, Restrict→72 ≥ 70-grensen)
- [ ] `ClinicalSessionScoreTests`: profilvektet score; **pitch-nøytralitet** (høyere pitch i sonen endrer IKKE score — kun compliance teller); caps (lock→≤40, lav compliance→≤55, halv tid→≤30); 0 ticks → 0
- [ ] `MasteryEvaluatorTests`: alle gater enkeltvis (20-øktskrav, resonans, stabilitet, locks 7/14 d, breach-økter, fatigue-trend); demotion ved fersk lock; konservativ default ved tom historikk; profiler uten resonans/stabilitet hopper over respektive gater
- [ ] `ProgressionSafetyGateTests`: blokkering ved ≥2 SafetyFreeze/7 d, stigende strain-trend, komfortbrudd-grense; ikke-blokkering ved ren historikk
- [ ] `MainViewModel`-integrasjon: strain-flagg → RecordStrainIncident → promotering blokkert selv med score ≥ 75

**Manuell QA (krever Windows + mikrofon):**
- [ ] Øvelse start→stopp: score reflekterer faktisk resonans/stabilitet (ikke bare tid); kort økt med god stemme > lang stille økt
- [ ] Mastery-badge oppdateres uten UI-frys ved åpning av øvelse (async-lasting)
- [ ] Fremprovoser strain (presset stemme): supervisor → Lock → shield viser låst + HoldArc fryser + score cappes
- [ ] Restart appen: tidligere safety-locks synlige i gate-beslutning (persistert i femvoice.db, `SessionAnalyticsHealthEvents`)
- [ ] Eldre databaser (uten analytics-rader): mastery viser maks Stable, ingen krasj
- [ ] Forsiden: promotering nå mulig med reell score, men blokkert etter safety-hendelser

**Byggemiljø:** `net10.0-windows` — på Linux kun `dotnet build -p:EnableWindowsTargeting=true`; full testkjøring krever Windows (Microsoft.WindowsDesktop.App).

---

## 7. Clinical Safety Impact Report

| Klinisk krav (prompt) | Før | Etter |
|---|---|---|
| Mastery krever resonanskonsistens | ❌ Ingen stemmedata i score i det hele tatt | ✅ Snittresonans siste 10 økter ≥ profilterskel (hard gate) |
| Mastery krever stabilitetskonsistens | ❌ | ✅ Snittstabilitet ≥ `StabilityThreshold` (hard gate) |
| Mastery krever komfortsone-compliance | ❌ Komfortdata ble aldri persistert | ✅ Compliance i øktscore + maks 2/10 brudd-økter for Mastered |
| Mastery krever helse-/safety-compliance | ❌ Safety-lock kunne ikke engang utløses (health=100) | ✅ Lock-episoder persisteres; 0 locks siste 14 d kreves; fersk lock → demotion til Developing |
| Mastered krever 20+ økter | ❌ 3 økter + snitt ≥ 80 (tidsbasert) holdt | ✅ ≥ 20 økter + alle gater |
| Progresjon stoppes ved gjentatte komfortbrudd | ❌ Ingen kobling | ✅ `ProgressionSafetyGate`: ≥ 3 brudd-økter/7 d blokkerer |
| Progresjon stoppes ved gjentatte safety-locks | ❌ Lås-mekanisme fantes men var død (aldri kalt, in-memory) | ✅ ≥ 2 SafetyFreeze/7 d blokkerer — stateless fra persistert historikk, overlever restart |
| Progresjon stoppes ved stigende strain/fatigue | ❌ Strain ble aldri målt i øvelsesløkka | ✅ Supervisor vekket per tick; uke-over-uke-trend blokkerer |
| Pitch alene kan aldri låse opp Mastery / trigge progresjon / øke vanskelighet | ⚠️ Delvis tilfeldig (ingenting kunne) — men `DisplayQuality` belønner pitchhøyde (D1) | ✅ Pitch inngår utelukkende som sone-compliance (0–1); eksplisitt testkrav på pitch-nøytralitet |
| Resonans/stabilitet/komfort/helse foran pitch | ❌ | ✅ Vekter 0.45/0.25 + harde gater foran pitch-compliance 0.20 |

**Restrisikoer og bevisste designvalg (til klinisk gjennomgang før implementering):**

1. **Resonans-proxyens kvalitet:** `ResonanceProxyEngine` er en FFT-proxy med kjente svakheter (fallback-formanter F1=350/F2=2000/F3=2800 ved < 2 topper; `fallbackResonance` i ExerciseWindow:728-732 er delvis RMS-drevet). Gating på snitt over 10 økter demper støy, men **resonansgaten arver proxyens bias** — høy intensitet kan fortsatt flattere resonansscoren noe. Anbefalt oppfølging (fase 2): strain-vektet resonans.
2. **Lås-latch innen økt:** Supervisor de-eskalerer ikke fra Lock så lenge staten er låst (stable-kravet i VocalHealthSupervisor.cs:100-104 krever `!IsSafetyLocked`). Med health-mapping Lock→40 blir låsen «klebrig» ut økten og nullstilles ved neste `BeginSession` (Reset). Dette er **tilsiktet** (lås = avslutt og hvil), og historikken består i analytics — men det bør bekreftes klinisk at lås ikke skal kunne friskmeldes midt i en økt.
3. **Restrict→72 låser ikke:** Bevisst valgt så Restrict gir advarsel (Caution-territorium) uten lås, for å unngå selvforsterkende lås-spiral (lås → strain-score +0.80 → evig lås). Kun supervisorens Lock-tilstand (3 restrict-sykluser) når under 70-terskelen.
4. **Migrasjonseffekt:** Eksisterende `AverageScore`-verdier er tidsbaserte og inflaterte. Etter fiksen vil snittet synke gradvis (inkrementelt snitt). Brukere som i dag ser «Mastered» vil typisk falle til Developing/Stable pga. manglende analytics-historikk (konservativ default). **Dette er klinisk riktig, men bør kommuniseres** — vurder en engangs-nullstilling av `ExerciseProgress.AverageScore` ved migrering for renere data.
5. **`OverallScore`-fiksen på forsiden** (C3) gjør at promotering går fra *umulig* til *mulig-men-gatet*, og at feilaktig degradering opphører. Promoteringsterskelen (75) treffes nå av FemVoiceScore som allerede har «resonans før pitch»-straffer — konsistent med resten.
6. **Subjektiv rapport** (C7) forblir ulest i denne fasen; fase 2 (aktivering av `ProgressionOrchestrator`, som allerede støtter `SubjectiveReport.IndicatesHealthConcern` → pause) er den naturlige mottakeren.

**Anbefalt fase 2 (utenfor denne fiksen):** Aktiver `ProgressionOrchestrator` (datakilden er nå fylt av Patch 1) ved å kalle `OnSessionCompletedAsync` fra øvelses-stopp og la `SuggestedProfile` persisteres via eksisterende `ExerciseProfileStore` — da blir også per-øvelse vanskelighetsskalering helse-gatet. Krever Patch 7 (`&&`-fiksen) først.
