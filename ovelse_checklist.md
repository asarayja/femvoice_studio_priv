# FEMVOICE – Øvelser med live mikrofonfeedback + SmartCoach inline
## Avkrysningsliste + steg-for-steg plan

---

## 0) Forarbeid

⬜ 0.1 Definer "hvilke øvelser kan bruke mic"
⬜ 0.2 Bestem standard indikator-pakke per kategori
⬜ 0.3 Anti tall-jakt-regel: tall kun med kontekst
⬜ 0.4 Definer felles terskler (defaults + config override)

---

## 1) Modeller ✅

✅ 1.1 ExerciseTargetProfile (med Validate() og factory-metoder)
✅ 1.2 ExerciseLiveState (immutable record)
✅ 1.3 InlineCoachMessage (med MessageSeverity enum)
✅ 1.4 PerformanceQuality enum
✅ 1.5 ExerciseProfileType enum (type-sikker SQLite-mapping, int-verdier 0–3)
⬜ 1.6 ReasonCode enum (bruker string reason codes foreløpig)

---

## 2) ExerciseIntelligenceCoordinator ✅

✅ 2.1 Koordinator opprettet (event-drevet, ingen polling)
✅ 2.2 Subscribe til upstream engines
✅ 2.3 Start/Stop lifecycle
✅ 2.4 Kalkuler ExerciseLiveState per audio-frame
✅ 2.5 Hold logic: HoldProgress og IsHoldingCorrectly
✅ 2.6 Safety integration: freeze/lock ved health/comfort lock
✅ 2.7 Events: ExerciseUpdated + InlineCoachUpdated

---

## 3) SmartCoach inline ✅

✅ 3.1 InlineCoachPolicy (innebygget i koordinatoren)
✅ 3.2 Rate-limit: 5 sekunder mellom samme reason code
✅ 3.3 Severity: Warning / Suggestion / Info
✅ 3.4 Hint er ikke-numerisk

---

## 4) ViewModel wiring ✅

✅ 4.1 ExerciseDetailViewModel injiserer koordinator
✅ 4.2 ObservableProperties for ExerciseLiveState
✅ 4.3 InlineCoachMessage med auto-dismiss timer
✅ 4.4 Start/Stop commands koblet til koordinator lifecycle
✅ 4.5 Dispatcher marshalling kun i ViewModel

---

## 5) UI — Live Feedback-panel ✅

✅ 5.1 LiveFeedbackPanel i ExerciseWindow.xaml
✅ 5.2 Fem indikatorer: Resonance Bar, Stability Meter, Shield, Hold Arc, Coach Hint
✅ 5.3 Start/Stop-knapper (eksisterende code-behind)
✅ 5.4 Pitch-panel skjules for resonansøvelser
✅ 5.5 Converters.cs oppdatert (ProgressToPercent + SeverityToBrush)

---

## 6) Øvelse-templates ✅

✅ 6.1 CreateResonanceHumming    — hold 3 s, stability 0.45, resonans primær
✅ 6.2 CreateResonanceVowels     — hold 4 s, stability 0.55, strengere resonanskrav
✅ 6.3 CreateCoordinatedGlideUp  — hold 0 s, stability 0.40, pitch primær (ingen Hz)
✅ 6.4 CreateStabilityTraining   — hold 6 s, stability 0.70 (høyest av alle)

---

## 7) Refaktorering — type-sikker profilmapping ✅

✅ 7.1 ExerciseProfileType enum i Models/
✅ 7.2 Exercise.ProfileType property lagt til
✅ 7.3 IExerciseProfileFactory + ExerciseProfileFactory i Services/
✅ 7.4 ExerciseDataService oppdatert:
       - MigrateExerciseColumns: ProfileType INTEGER NOT NULL DEFAULT 0
       - MapToExercise: leser ProfileType fra reader med try-catch fallback
       - INSERT: ProfileType kolonne + @ProfileType parameter
       - Seed data: alle 15 øvelser tilordnet korrekt ProfileType
✅ 7.5 ExerciseWindow.xaml.cs refaktorert:
       - _profileFactory felt + DI-resolving i InitializeLiveFeedback
       - _profileFactory?.CreateProfile(exercise.ProfileType) erstatter BuildProfileForExercise
       - BuildProfileForExercise (cat.Contains-logikk) fjernet
✅ 7.6 App.xaml.cs: services.AddSingleton<IExerciseProfileFactory, ExerciseProfileFactory>()

---

## 8) Testing

✅ 8.1 ExerciseIntelligenceCoordinatorTests — 21 tester
✅ 8.2 ExerciseProfileFactoryTests — 14 testscenarier (9 Fact + 5 Theory):
       - Ingen null for gyldige verdier
       - Validate() passerer for alle fire profiler
       - Deterministisk mapping
       - ArgumentOutOfRange for ukjent enum-verdi (99)
       - Nøkkelfelt verifisert for alle fire profiler
       - Hold-hierarki: glide=0 < humming < vowels < stability
       - StabilityTraining har høyest StabilityThreshold
       - ResonanceVowels har strengere TargetResonanceMin enn humming
       - Ingen hardkodede Hz (MinPitch/MaxPitch er null ved opprettelse)
       - Alle normaliserte terskler i [0,1]
       - RequiredHoldSeconds >= 0
✅ 8.3 Parameterless constructor (ingen mic i test)
✅ 8.4 InlineCoachPolicyTests — 27 Fact-tester:
       - Resonans: lav/høy/innenfor/blokkert av safety lock
       - Stabilitet: lav/ok/blokkert av safety lock
       - Safety: HEALTH_SAFETY_LOCK, COMFORT_ZONE_LOCK dekning, hold-frys/gjenopptakelse
       - Rate-limiting: samme reason blokkert, ulik reason ikke blokkert
       - Kontekst: PitchExercise ignorerer resonans, ResonanceExercise ignorerer pitch
       - Innhold: ingen rå tallverdier, ikke-tom ShortMessage
       - Livssyklus: ikke startet og stoppet → ingen meldinger
       - Severity-kontrakter: Suggestion/Info/Warning per type

---

## 9) Definition of Done

✅ Safety locks stopper/fryser øvelse korrekt
✅ Ingen tall-jakt UI
✅ Ingen polling / timers (ren event-drevet arkitektur)
✅ Fire øvelsesprofiler implementert med klinisk begrunnelse
✅ Ingen string-matching for profilvalg
✅ Ingen profilbygging i code-behind
✅ ExerciseProfileType lekker ikke inn i SmartCoach/adaptive komponenter
⬜ Live feedback verifisert i kjørende app
⬜ Alle tester grønne (ikke kjørt mot bygget ennå)

---

## Neste steg

**Anbefalt:** Bygg og kjør appen — verifiser at Live Feedback-panelet vises
for en resonansøvelse og at ProfileType-mappingen fungerer korrekt fra database.

**Neste steg:** Bygg og kjør alle tester, deretter verifiser Live Feedback i kjørende app.