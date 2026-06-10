# RC-0 Hydration Advisor — samlet review

**Dato:** 2026-06-10
**Type:** Review only — INGEN kodeendringer gjort (per oppgavens «Do not patch code yet»).
**Metode:** Multi-agent review (3 reviewere: arkitektur / klinisk-UX / test-regresjon) → koordinator-syntese → adversarial verifisering. **6/6 bærende påstander bekreftet (0 refutert)**, hver av 2 uavhengige skeptikere.

> RC-0-rammer respektert i alle forslag: ingen nytt hydreringssubsystem, ingen ny
> dashboard, ingen ny SmartCoach-arkitektur, ingen ny persistensmodell/migrasjon, ingen
> stor refaktor, ingen medisinske påstander, ingen konstant hydreringsspam. Norsk
> kildetekst først; oversettelser er separat oppfølging. Patch holdes minimal og
> gjenbruker eksisterende økt-state.

---

## 1. Dagens hydreringslogikk

Hydreringsteksten når skjermen via **én delt coach-meldingsflate**, men produseres av
**to uavhengige per-tick-produsenter**:

1. `ExerciseIntelligenceCoordinator` publiserer `ExerciseLiveState` per tick (rate-limitet 100ms) — `ExerciseIntelligenceCoordinator.cs:620`, bygd `:559-587`.
2. `ExerciseSessionRecorder.OnExerciseUpdated` (abonnert `:218`) kaller per ikke-idle tick **begge** rådgivere: `_healthSupervisor.Evaluate(state)` (`ExerciseSessionRecorder.cs:487`) **og** `_hydrationAdvisor?.Evaluate(state)` (`:488`). Idle/stille ticks droppes først (`IsIdleState`, `:456`).
3. `HydrationAdvisor.Evaluate` (`HydrationAdvisor.cs:55-103`) bygger en score: resonansdrift ≥0.06 → +0.40; stabilitetsvarians ≥0.03 → +0.25; dempet `_accumulatedLoad` ≥0.55 → +0.25 (`LoadDecay` 0.96, `:158`); stabilitetsdrift +0.10; negativ slope +0.10. Foreslår når `score ≥ SuggestionThreshold (0.65)` **og** `!IsSafetyLocked` **og** 2-min `MinimumSuggestionInterval` siden `_lastSuggestionAt` (`:78-85`).
4. `VocalHealthSupervisor.EvaluateHydration` (`VocalHealthSupervisor.cs:180-191`) folder uavhengig resonansdrift, stabilitetsvarians og `fatigueScore` (≥0.45) inn i sin egen `HydrationScore ≥ 0.65`.
5. Begge går til `SubmitHealthFeedback` (`ExerciseSessionRecorder.cs:510-545`): `HydrationFeedbackMapper.Map` sender nøkkel `VoiceHealthFeedback_Hydration` med ReasonCode `HYDRATION_RESONANCE_DRIFT`/`HYDRATION_LOAD` (`FeedbackPipeline.cs:262-275`); `VocalHealthFeedbackMapper.Map` sin `HydrationSuggested`-gren sender **samme nøkkel** med ReasonCode `HYDRATION_SUGGESTED` (`:344-352`).
6. `FeedbackConsistencyGuard.Submit` rate-limiter 2s *per ReasonCode* (`:127-131`), undertrykker hydrering ved SafetyFreeze (`:198-199`).
7. `ExerciseDetailViewModel.OnPipelineFeedbackApproved` (`:862-891`) resolver `_localization.GetString(decision.Candidate.Message)` (`:880`) → bundet `CoachMessage` (`:961-968`).

Teksten er **lokalisert, ikke hardkodet**. Norsk kilde: `Strings.resx:2275-2277` —
*«Litt vann kan hjelpe. Ta noen rolige slurker og fortsett forsiktig.»* `HydrationAdvisor`
er DI-singleton (`App.xaml.cs:214`). Advisoren leser aldri `SessionElapsedSeconds`
(finnes `ExerciseLiveState.cs:71`), aldri fatigue/strain, og `Reset()` (`:105-119`)
kalles kun i tester. Hydrering er knyttet til **både** Voice Health og den frittstående
advisoren — **ikke** til SmartCoach.

## 2. Problemer funnet

- 🔴 **KRITISK — anti-repeat-lekkasje (verifisert):** `HydrationAdvisor` er DI-singleton (`App.xaml.cs:214`) injisert i singleton-recorderen (`:179`). `BeginSession` resetter kun `_healthSupervisor` (`ExerciseSessionRecorder.cs:296`), **ikke** advisoren — `Reset()` kalles aldri i produksjon. Dermed lever `_lastSuggestionAt` *og* `_accumulatedLoad` på tvers av alle økter i hele app-kjøringen. Følge: en ny økt kan arve forrige økts last (decay kun 0.96/tick) og re-fyre nesten umiddelbart, **eller** en gammel `_lastSuggestionAt` undertrykker det første legitime nudge i en ny økt. «2-min cooldown» er i praksis per-app-levetid, ikke per-økt.
- **Dobbel produsent av identisk setning:** `HydrationFeedbackMapper` (`FeedbackPipeline.cs:269`) og `VocalHealthFeedbackMapper` `HydrationSuggested`-gren (`:347`) sender samme nøkkel med *ulik* ReasonCode. Guard-ens 2s-grense keyer på ReasonCode (`:127`), så de **koalescerer ikke** → samme linje kan vises to ganger ~2s fra hverandre. Supervisor-stien har ingen lang hydrerings-cooldown.
- **Gratis kontekst ubrukt:** `SessionElapsedSeconds` (`ExerciseLiveState.cs:71`) sendes til `Evaluate` men leses aldri; `VocalHealthDecision` (FatigueScore/FatigueDetected/StrainDetected) beregnes én linje før hydreringskallet (`:487`) men forkastes for hydrering.
- **Ingen «rakk å drikke»-budsjett:** intet tak på antall nudges per økt. `_hydrationSuggestions` telles i recorderen (`:174/496`) men advisoren ser den ikke.
- **Ingen ekte kontinuerlig-vs-pauset-signal:** `SessionElapsedSeconds` er monoton vegg-klokke, ikke dekrementert for stillhet; idle-ticks droppes (`:456`). `_accumulatedLoad`-decay under stillhet er eneste proxy. Et ekte pause-gap-felt krever ny plumbing → utenfor RC-0.
- **Meldingen kan aldri variere med kontekst:** kun én resx-streng for alle triggere; ReasonCode-skillet (`HydrationAdvisor.cs:178-181`) når aldri brukeren.

## 3. Tilgjengelige datakilder (ved live-beslutningspunktet)

| Datum | Hvor |
|---|---|
| `SessionElapsedSeconds` (sek siden StartExercise) — **ubrukt** | `ExerciseLiveState.cs:71` |
| `IsSafetyLocked` (gater allerede) | `ExerciseLiveState.cs:55`; lest `HydrationAdvisor.cs:81` |
| `IsHoldingCorrectly` (intensitets-proxy → load) | `ExerciseLiveState.cs:42`; `HydrationAdvisor.cs:154` |
| `Timestamp` (driver 2-min cooldown) | `ExerciseLiveState.cs:65`; `HydrationAdvisor.cs:77-79` |
| Resonansdrift / stabilitetsvarians / `_accumulatedLoad` | `HydrationAdvisor.cs:64-76, 151-158` |
| `VocalHealthDecision.FatigueScore/FatigueDetected/StrainDetected` — beregnet `:487`, kastes | `VocalHealthSupervisor.cs:36-41,115-127` |
| `_lastSuggestionAt` (anti-repeat) + `Reset()` finnes | `HydrationAdvisor.cs:46,105-119` |
| `_hydrationSuggestions` (per-økt-teller, i recorderen) | `ExerciseSessionRecorder.cs:174,496` |

## 4. Manglende datakilder

- Ekte kontinuerlig-vs-pauset / pause-gap-signal (intet per-tick brudd-felt; kan ikke legges til RC-0-trygt uten ny plumbing).
- Kryss-økt-timing ved live-punktet (`HoursSinceLastSession`, `SessionsLast7Days`): finnes kun ved øktslutt i recovery-aksen (`RecoveryScorer.cs:58,63,67`) — og skal **ikke** brukes som dehydreringssignal.
- Persistert «tid siden forrige påminnelse på tvers av økter»: finnes ikke, og skal ikke legges til (ny persistens/migrasjon = RC-0-forbudt).
- Direkte kunnskap om at brukeren faktisk drakk: uobserverbar; tilnærmes kun av cooldown.

## 5. Anbefalte regelendringer

1. **Fiks lekkasjen (1 linje, eksisterende sti):** kall `_hydrationAdvisor?.Reset()` i `BeginSession` rett etter `_healthSupervisor.Reset()` (`ExerciseSessionRecorder.cs:296`). Gjør `_lastSuggestionAt` + `_accumulatedLoad` per-økt. Høyest verdi / lavest risiko.
2. **Gate første nudge på reell øvetid:** undertrykk før ~120s, men **kun** når `SessionElapsedSeconds > 0`. Escape-hatch: `state.SessionElapsedSeconds == 0 || >= MinimumPracticeSeconds` holder eksisterende tester grønne (deres `State()`-helper lar feltet stå 0). Ny `MinimumPracticeSeconds` (default 120) i `HydrationAdvisorOptions`.
3. **Mat inn fatigue-kontekst (gjenbruk in-tick state):** valgfrie, defaultede params (`fatigueScore=0, fatigueDetected=false, strainDetected=false`) til `Evaluate`; send `VocalHealthDecision`-verdiene fra `:488`. Brukes kun til ordvalg/paring — **aldri** for å heve hydreringens prioritet over pause/helse. Severity forblir `Suggestion`.
4. **Per-økt repeat-budsjett (~2–3)** internt i advisoren; teller nullstilt i `Reset()`; etter cap → `Suggested=false` resten av økten uansett score.
5. **Dedupliser de to produsentene:** samkjør `VocalHealthFeedbackMapper` `HydrationSuggested`-ReasonCode (`FeedbackPipeline.cs:347`) med advisorens daglige ReasonCode så guard-ens 2s-limiter slår dem sammen. Behold delt `ConflictKey HEALTH_HYDRATION`.
6. **Ikke** legg til tid-siden-forrige-økt-ledd i advisoren: langt opphold = hvile (`RecoveryScorer.cs:63`), ikke dehydrering. `Reset()` ved øktstart garanterer allerede ren score.

## 6. Foreslåtte nivåer (norsk kildetekst)

| Nivå | Når | Melding |
|---|---|---|
| **NONE** (dominant default) | score < 0.65; eller `IsSafetyLocked`; eller i cooldown; eller < 120s reell øvetid (når `SessionElapsedSeconds>0`); eller budsjett oppbrukt; eller fersk økt etter langt opphold (`Reset()` → score 0) | *(ingen melding)* |
| **SOFT_REMINDER** (daglig mild nudge) | Første kvalifiserende nudge: score ≥0.65 drevet av resonansdrift (`HYDRATION_RESONANCE_DRIFT`), ingen strain/fatigue, cooldown passert, `!IsSafetyLocked`, ≥120s øvetid (eller `==0` i tester) | `VoiceHealthFeedback_Hydration` (gjenbruk): *«Litt vann kan hjelpe. Ta noen rolige slurker og fortsett forsiktig.»* |
| **RECOMMENDED** (fastere, fortsatt Suggestion) | Andre kvalifiserende nudge senere i økten etter cooldown, `_accumulatedLoad` fortsatt høy + vedvarende drift (evt. bekreftet av `FatigueScore≥0.45`). Distinkt ReasonCode (`HYDRATION_SUSTAINED`) så nivåene ikke rate-limiter hverandre. Prioritet `HydrationSuggestion`(40) | `VoiceHealthFeedback_HydrationSustained` (NY kildenøkkel): *«Stemmen har jobbet en stund. Ta deg en vannpause før du fortsetter.»* |
| **IMPORTANT** (paret med hvile — IKKE eskaler) | Hydreringsdrift sammenfaller med supervisorens `FatigueDetected==true` eller `PauseRecommended==true` samme tick. Guard prioriterer Pause(50)/HealthWarning(70) over Hydration(40) (`FeedbackConsistencyGuard.cs:8-18,133-138`), så hvile-linjen leder; hydrering som sekundærnotat. Aldri ved SafetyFreeze/Lock | Supervisorens `VoiceHealthFeedback_Pause`/`_Fatigue` leder; hydrering rir som NY nøkkel `VoiceHealthFeedback_HydrationWithRest`: *«Pust rolig og ta noen slurker vann mens du hviler stemmen litt.»* |

## 7. Anti-spam / cooldown-regler

- Behold alle 3 **eksisterende** lag: (1) advisor 2-min `MinimumSuggestionInterval` (`HydrationAdvisor.cs:19,78-85`); (2) guard 2s per-ReasonCode (`FeedbackConsistencyGuard.cs:71,127-131`); (3) guard undertrykker hydrering ved SafetyFreeze (`:198-199`).
- **Gjør cooldownen reelt per-økt** via `Reset()` i `BeginSession` — viktigste enkeltfiks.
- Legg til per-økt repeat-cap (~2–3) i advisoren.
- Distinkte ReasonCodes per nivå (SOFT vs SUSTAINED) så de ikke rate-limiter hverandre; behold delt `ConflictKey`.
- Koalescer de to produsentene (samme ReasonCode for daglig nudge).
- Gate første nudge på ≥120s reell øvetid.
- Stol på eksisterende decay-under-stillhet (`LoadDecay 0.96`) + idle-tick-dropping: pauset→gjenopptatt re-armer langsomt, intet nytt pause-felt nødvendig.

## 8. Filer å endre (minimalt)

| Fil | Endring |
|---|---|
| `FemVoiceStudio/Services/ExerciseSessionRecorder.cs` | `_hydrationAdvisor?.Reset();` i `BeginSession` (etter `:296`); evt. send `VocalHealthDecision` inn i `Evaluate` (`:488`, beregnet `:487`). |
| `FemVoiceStudio/Services/HydrationAdvisor.cs` | `MinimumPracticeSeconds`(120) + `SuggestionBudget`(3) i options; elapsed-gate m/`==0`-escape; per-økt-teller i `Reset()`; defaultede fatigue/strain-params. Behold enkelt-arg-signaturen. |
| `FemVoiceStudio/Services/FeedbackPipeline.cs` | `HydrationFeedbackMapper.Map` velger nøkkel per ReasonCode (`:262-275`); samkjør `VocalHealthFeedbackMapper` `HydrationSuggested`-ReasonCode (`:347`). Uendret prioritet/severity/ConflictKey. |
| `FemVoiceStudio/Resources/Strings.resx` | To nye **norske kilde**-nøkler ved siden av `VoiceHealthFeedback_Hydration` (`:2275`). Oversettelser (`Strings.<culture>.resx`) = separat oppfølging. |

## 9. Tester å legge til / oppdatere

- `Advisor_NotResetAcrossSessions_RegressionGuard` — bevis carry-over-bug, snu til ren oppførsel etter `BeginSession`-Reset-fiksen. (`HydrationAdvisorTests.cs` + integrasjonsassert i `HealthFeedbackIntegrationTests.cs`.)
- `Advisor_ShortSession_NoSuggestionBeforeMinimumPractice` — `SessionElapsedSeconds` 5..30 → ingen forslag; klatrer forbi 120 → kan fyre; `==0` → fortsatt tillatt (beskytter eksisterende tester).
- `Advisor_PerSessionBudget_CapsRepeats` — maks `SuggestionBudget`, deretter ingen; `Reset()` re-armer.
- `Advisor_FatigueContext_SelectsSustainedReason` — fatigue inn → andre nudge bruker `HYDRATION_SUSTAINED` → `VoiceHealthFeedback_HydrationSustained`, prioritet forblir 40.
- `Guard_TwoProducersSameReason_Coalesce` — to hydreringskandidater ~1s fra hverandre m/ samkjørt ReasonCode → andre undertrykkes (`FeedbackConsistencyGuard.cs:127-131`).
- `Supervisor_FatigueOrPause_OutranksHydration` — fatigue/pause → Pause-kandidat (50) leder, ikke hydrering (40).

**Eksisterende tester i fare** (bruker `SessionElapsedSeconds==0`, beskyttet av escape-hatch): `HydrationAdvisorTests` (linje 22-41, 63-79, 81-126), `HealthFeedbackIntegrationTests`, `FeedbackPriorityMatrixTests.cs:49,57,62,67,72`, `FeedbackConsistencyGuardTests.cs:354-373`.

## 10. Risikoer

- Hard elapsed-gate **uten** `==0`-escape ville brutt eksisterende positive tester (`State()`-helper lar feltet stå 0, `HydrationAdvisorTests.cs:183-199`). Største implementeringsrisiko.
- `BeginSession`-Reset endrer langvarig (buggy) carry-over-oppførsel — ønsket, men flagges.
- Koalescering via samkjørt ReasonCode kan maskere et genuint supervisor-only-signal hvis advisor-stien også fyrer — verifiser at supervisor-stien fortsatt vises når advisoren er null/av (`ExerciseSessionRecorder.cs:198,208`).
- Nye `Evaluate`-params **må** defaultes, ellers brekker alle eksisterende kallsteder + recorder-kallet (`:488`).
- Nye resx-nøkler må finnes i `Strings.resx` *før* kultur-filer refererer dem (manglende nøkkel viser rå nøkkelstreng på skjerm).
- `FeedbackPriorityMatrixTests`/`FeedbackConsistencyGuardTests` pinner hydreringsprioritet/-undertrykking — IMPORTANT-nivået må **ikke** heve hydreringens prioritet eller endre `BuildContext`-flagg.
- **Kan ikke bygges/kjøres på Linux dev-boksen** (.NET-windows WPF): full `dotnet test` + manuell sjekkliste må kjøres på Windows-maskinen.

## 11. Minimal patch-plan (rekkefølge)

1. `_hydrationAdvisor?.Reset()` i `BeginSession` (1 linje) — størst verdi, lavest risiko.
2. `HydrationAdvisor`: options + elapsed-gate (m/escape) + per-økt-budsjett + defaultede fatigue-params.
3. `FeedbackPipeline`: ReasonCode→nøkkel-valg + samkjør de to produsentene.
4. `Strings.resx`: to nye norske kildenøkler.
5. Tester per §9.
6. Windows: full `dotnet test` + manuell RC-0-sjekkliste.

---

## Verifiseringsstatus

6/6 bærende påstander bekreftet av 2 uavhengige skeptikere hver (0 refutert):
singleton-lekkasjen (kun `_healthSupervisor` resettes i `BeginSession`); `SessionElapsedSeconds==0`-testfellen; at fatigue/strain er tilgjengelig én linje før hydreringskallet; dobbel-produsent samme nøkkel; `Strings.resx` som norsk kilde (ingen `Strings.nb*.resx`); og SafetyFreeze-undertrykkingen.

## Suksesskriterier for en framtidig patch (godkjennes kun hvis)

Hydreringsråd blir kontekstbevisst · appen viser ikke gjentatt «drikk vann» · korte økter
trigger ikke sterke advarsler · lange/intense økter gir nyttig råd · pauser/cooldowns
respekteres · fatigue/strain kan eskalere rådet (men hydrering eskalerer aldri over
hvile) · eksisterende Voice Health-arkitektur bevart · tester dekker regeloppførselen.
