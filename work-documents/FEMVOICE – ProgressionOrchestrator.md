FEMVOICE – ProgressionOrchestrator
Adaptiv langtidsmotor – komplett sjekkliste
🧱 1) Kjerneansvar (systemets “langtidshjerne”)

✅ Opprett ProgressionOrchestrator service
✅ Abonnerer kun på høynivå-events (ikke rå audio)
✅ Ingen UI-avhengighet
✅ Ingen sanntidskritiske beregninger
✅ Bruker historiske øktdata

Skal motta:

✅ SessionCompleted
✅ ExercisePerformanceSummary
✅ HealthTrendUpdated
✅ SafetyFreezeOccurred
✅ PauseRecommended
✅ HydrationSuggested

Skal publisere:

✅ DifficultyAdjustmentSuggested
✅ ExerciseProfileUpdated
✅ ProgressionPaused
✅ RegressionTriggered
✅ PlateauDetected

📊 2) Langtidsdataanalyse (grunnmur)

✅ Aggregert økt-score per øvelse
✅ Stabilitetstrender over dager
✅ Resonansutvikling over tid
✅ Fatigue-frekvens
✅ Safety-hendelser per økt
✅ Hold completion rate
✅ Konsistens mellom økter

👉 Aldri reagere på én økt alene.

📈 3) Progresjonsmodell (klinisk korrekt)

✅ Baseline-relativ forbedring
✅ Avtagende progresjon (ikke lineær)
✅ Plateaufølsom respons
✅ Regresjon ved helserisiko
✅ Prioriter resonans før pitch alltid
✅ Stabilitet som “gatekeeper”

Eksempel-prinsipper:

✔ Øk vanskelighet sakte
✔ Senk raskere ved problemer
✔ Lås progresjon ved fatigue
✔ Krev konsistens før videre steg

🚦 4) PlateauDetectionPolicy

✅ Ingen forbedring over X økter (trendbasert)
✅ Stabil ytelse uten vekst
✅ Fallende motivasjon-indikatorer (optional)
✅ Forslag om teknikkfokus fremfor økning

🔄 5) RegressionPolicy (helse først)

✅ Flere safety freezes
✅ Økende fatigue
✅ Fallende stabilitet
✅ Redusert hold-kvalitet
✅ Overbelastning

👉 Regresjon skal være raskere enn progresjon (klinisk anbefalt)

🎯 6) DifficultyScalingEngine

Justerer:

✅ Resonanskrav
✅ Stabilitetsterskler
✅ Hold-lengde
✅ Øvelsesvariasjon
✅ Pitch-comfort range (kun etter resonansstabilitet)

🧠 7) Kliniske prioriteringsregler

✅ Resonans alltid før pitch
✅ Stabilitet alltid før intensitet
✅ Helse alltid før progresjon
✅ Konsistens før vanskelighetsøkning
✅ Teknikklæring før ytelse

🧪 8) Unit test-krav (kritisk!)
Progresjon:

✅ Vanskelighet øker ved stabil forbedring
✅ Øker ikke ved spike
✅ Øker ikke ved fatigue

Plateau:

✅ Plateau oppdages korrekt
✅ Ikke falsk plateau ved kort dropp

Regresjon:

✅ Regresjon ved helsehendelser
✅ Raskere enn progresjon

Prioritering:

✅ Resonanskrav økes før pitch
✅ Stabilitet må være ok først

📐 9) Arkitekturkrav

✅ Event-drevet
✅ Ingen polling
✅ Ingen timers
✅ Ingen magic thresholds
✅ Baseline-relativ
✅ Full testbarhet
✅ Clean separation

Status 2026-05-28:

✅ Implementert `ProgressionOrchestrator`
✅ Registrert i DI
✅ Koblet inn etter fullført exercise i `ExerciseWindow`
✅ Leser historikk via `SessionAnalyticsStore`
✅ Evaluerer bare høynivådata, ikke rå audio
✅ Publiserer beslutningsevents for difficulty/profile/pause/regression/plateau
✅ Har konfigurerbare terskler via `ProgressionOrchestratorOptions`
✅ Har tests for stabil forbedring, spike-blokkering, fatigue, safety-regresjon, plateau, performance-regresjon og resonans før pitch

Status 2026-05-29:

✅ Progresjonsbeslutninger går gjennom `FeedbackPipeline`/`FeedbackConsistencyGuard`
✅ Godkjent progresjonsfeedback vises lokalisert i exercise-UI
✅ Implementert øvelsesvariasjon som egen scaling-dimensjon
✅ Orchestrator analyserer exercise summaries på tvers av øvelser i lookback-vinduet
✅ Dominerende ensidig øvelseshistorikk foreslår variasjonsprofil før videre belastningsøkning
✅ Progresjonsfeedback har egen lokalisert variasjonsmelding
✅ Tester dekker variasjonsforslag, blandet historikk og feedback-mapping

Status 2026-05-31:

✅ Implementert `IExerciseProfileStore` med in-memory og SQLite-backed persistens
✅ Lagrer personaliserte `ExerciseTargetProfile`-overrides per user/exercise
✅ `ExerciseWindow` laster lagret profil før Guidance/Live Feedback initialiseres
✅ Progresjonsbeslutninger med `SuggestedProfile` persisteres etter fullført øvelse
✅ Neste økt bruker den personaliserte profilen som baseline for videre trygg progresjon
✅ Tester dekker in-memory save/load, SQLite-persistens og upsert av profile override

Status 2026-05-31:

✅ `ProgressionOrchestratorContext` kan nå ta inn `SubjectiveReport`
✅ Subjektiv strain/fatigue/lav komfort pauser progresjon med `SUBJECTIVE_HEALTH_CONCERN`
✅ Fallende motivasjon/readiness (`WantsToContinue == false`) pauser progresjon med `MOTIVATION_DROPPING`
✅ `OnSubjectiveReportSubmittedAsync` er lagt til som eventinngang
✅ Tester dekker subjective health concern og motivation drop

Gjenstår i denne modulen:

✅ Ingen åpne punkter i dette dokumentet per 2026-05-31
