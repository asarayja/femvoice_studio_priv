FEMVOICE – FeedbackConsistencyGuard
Konsistens- og prioriteringslag for all feedback
🧱 1) Kjerneansvar

✅ Opprett FeedbackConsistencyGuard service
✅ Ingen UI-avhengighet
✅ Ingen analyse-logikk
✅ Ingen progresjonsberegning
✅ Kun ansvar for prioritering og filtrering

👉 Denne modulen skal ikke lage feedback.
Den skal bare bestemme hva som får slippe gjennom.

🔗 2) Skal motta events fra

✅ SmartCoachEngine
✅ VocalHealthSupervisor
✅ HydrationAdvisor
✅ ExerciseIntelligenceCoordinator
✅ ProgressionOrchestrator

📤 3) Skal publisere

✅ FeedbackApproved
✅ FeedbackSuppressed (valgfritt for logging)
✅ FeedbackEscalated (ved konflikt)

Kun FeedbackApproved går videre til UI.

⚖ 4) Prioriteringshierarki (må være eksplisitt definert)

Rekkefølge:

1️⃣ Safety Freeze / Health Warning
2️⃣ Active Strain Alerts
3️⃣ Pause Recommendation
4️⃣ Hydration Suggestion
5️⃣ Technique Correction
6️⃣ Performance Praise
7️⃣ Progression Update

✅ Helse overstyrer alltid progresjon
✅ Pause overstyrer teknikkhint
✅ Strain overstyrer ros
✅ Ros undertrykkes ved helsehendelse

🔄 5) Konflikthåndtering

Eksempler guard må håndtere:

❌ “Excellent hold!” + “Strain detected”
❌ “Increase difficulty” + “Fatigue rising”
❌ “Hydrate now” + “Session should end”

Løsning:

✅ Undertrykk lavere prioritet
✅ Logg konflikt
✅ Eskaler ved gjentatt konflikt

⏱ 6) Rate Control / Stabilitet

✅ Ikke send to hints samtidig
✅ Ikke send motstridende hint innen kort intervall
✅ Prioriter eksisterende aktiv Warning før nye Info
✅ Respekter SmartCoach rate limit

🧠 7) Konsistensregler (klinisk viktige)

✅ Ingen ros hvis hold ikke stabil
✅ Ingen progresjonsforslag under fatigue
✅ Ingen teknikkøkning under health risk
✅ Ingen hydrationhint under aktiv freeze

📊 8) Logging (for klinisk sporbarhet)

✅ Logg undertrykte meldinger
✅ Logg konfliktårsaker
✅ Logg eskalering
✅ Logg gjentatt suppression

Dette er viktig for senere analyse.

🧪 9) Unit test-krav
Prioritet:

✅ Health warning overstyrer ros
✅ Pause overstyrer teknikkhint
✅ Freeze overstyrer progresjon

Konflikt:

✅ Motstridende hint filtreres korrekt
✅ Kun høyeste prioritet slippes gjennom

Stabilitet:

✅ Ingen dobbel-publisering
✅ Rate limit respekteres

Edge cases:

✅ Samtidige events håndteres
✅ Thread safety testet

📐 10) Arkitekturkrav

✅ Event-drevet
✅ Thread-safe
✅ Ingen UI-logikk
✅ Ingen klinisk beslutning her
✅ Kun prioritering
✅ Testbar isolert

Status 2026-05-28:

✅ `FeedbackConsistencyGuard` implementert
✅ `FeedbackPipeline` implementert som event-basert inngang til guard
✅ `ProgressionFeedbackMapper` implementert for å sende ProgressionOrchestrator-beslutninger gjennom guard
✅ Guard registrert i DI sammen med pipeline og mapper
✅ Exercise completion-flow sender progresjonsbeslutninger gjennom pipeline etter analytics/progression-vurdering
✅ Progression-feedback blir ikke vist direkte i UI ennå, for å unngå hardkodet engelsk tekst før språkkeys finnes
✅ Tester dekker prioritet, konflikt, rate-limit, eskalering, active strain, pipeline events, progression-mapping og parallell innsending

Status 2026-05-29:

✅ `VocalHealthSupervisor` koblet til FeedbackPipeline via `VocalHealthFeedbackMapper`
✅ Health/pause/hydration/strain-feedback går gjennom guard før visning
✅ Health-feedback bruker lokaliserte ressursnøkler
✅ Tester dekker health lock og hydration mapping
✅ Progresjonsfeedback bruker lokaliserte ressursnøkler og kan vises etter guard-godkjenning
✅ `ExerciseIntelligenceCoordinator` inline coach koblet til FeedbackPipeline via `InlineCoachFeedbackMapper`
✅ Inline coach-feedback bruker lokaliserte ressursnøkler før den vises i exercise-UI
✅ Tester dekker inline coach safety lock, hold-complete praise og suppression ved ustabil hold
✅ `SmartCoachEngine` koblet direkte til FeedbackPipeline via `SmartCoachFeedbackMapper`
✅ SmartCoach health/tip/motivation/achievement-meldinger filtreres før de lagres som synlige coach-meldinger
✅ Tester dekker SmartCoach health warning, tip mapping og rate-limit suppression
✅ `HydrationAdvisor` koblet til FeedbackPipeline via `HydrationFeedbackMapper`
✅ HydrationAdvisor-feedback filtreres før visning og undertrykkes under safety freeze
✅ Tester dekker hydrering-mapping og advisor-beslutninger

Gjenstår i denne modulen:

✅ Ingen åpne punkter i dette dokumentet per 2026-05-29
