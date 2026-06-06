FEMVOICE – SessionAnalyticsStore
Klinisk datalag + progresjonsgrunnmur – komplett sjekkliste
🧱 1) Kjerneansvar (hva denne modulen er – og ikke er)

✅ Opprett SessionAnalyticsStore service
✅ Kun ansvar for lagring + aggregering
✅ Ingen sanntidslogikk
✅ Ingen coaching
✅ Ingen UI-avhengighet i selve store/repository
✅ Clean repository-pattern

👉 Dette er “journalføringen” i systemet.

Skal motta events fra:

✅ SessionStarted
✅ SessionCompleted
✅ ExercisePerformanceSummary
✅ SafetyFreezeOccurred
✅ PauseRecommended
✅ HydrationSuggested
✅ HealthTrendUpdated

📊 2) Datatyper som skal lagres (kun klinisk nyttig)
Per økt:

✅ Start / slutt tidspunkt
✅ Total varighet
✅ Antall øvelser
✅ Gjennomsnittlig resonans
✅ Stabilitetstrend
✅ Pitch-komfort (ikke Hz)
✅ HealthScore trend

Per øvelse:

✅ HoldCompletionRate
✅ ResonansQualityIndex
✅ StabilityConsistency
✅ SafetyEventsCount
✅ FatigueIndicators
✅ CoachingHintsTriggered

Helse:

✅ Freeze-hendelser
✅ Strain-perioder
✅ Pauser foreslått
✅ Hydrering foreslått

🧠 3) Aggregeringslogikk (for progresjonsmotoren)

✅ Daglige snitt
✅ Ukentlige trender
✅ Øvelse-spesifikke kurver
✅ Stabilitetsutvikling
✅ Safety-frekvens
✅ Fatigue-mønstre

👉 Dette brukes direkte av ProgressionOrchestrator.

📈 4) Trendforberedelse (for grafer)

Data skal kunne gi:

✅ Resonans over tid
✅ Stabilitet over tid
✅ Hold-suksess
✅ Pausefrekvens
✅ Hydreringbehov
✅ Safety events

Uten ekstra beregning i UI.

🔐 5) Datakvalitet og sikkerhet

✅ Ingen rå audio lagres
✅ Kun normaliserte metrikker
✅ Tidsstempler konsekvente
✅ Feiltoleranse ved crash
✅ Transaksjonssikker lagring

🧪 6) Unit test-krav

✅ Økt lagres korrekt
✅ Øvelse-summer lagres korrekt
✅ Safety events lagres
✅ Aggregering gir riktige snitt
✅ Trenddata konsistent
✅ Ingen duplikater
✅ Robust ved manglende data

📐 7) Arkitekturkrav

✅ Repository interfaces
✅ Clean separation
✅ Event-drevet input
✅ Ingen UI-kobling
✅ Testbar
✅ Skalerbar
✅ Klar for AI senere

Status 2026-05-28:

✅ Implementert `SessionAnalyticsStore`
✅ Implementert `ISessionAnalyticsRepository`
✅ Implementert `InMemorySessionAnalyticsRepository` for isolerte tester
✅ Implementert `SqliteSessionAnalyticsRepository` for persistent lagring i `femvoice.db`
✅ Koblet Exercise completion-flow til analytics-lagring
✅ Lagrer session summary og exercise performance summary ved fullført exercise
✅ Lagrer safety freeze events
✅ Unit tests dekker lagring, aggregering, trenddata, duplikater, manglende data og SQLite-persistens
✅ Koble eksplisitte PauseRecommended-events fra health-laget
✅ Koble eksplisitte HydrationSuggested-events fra HydrationAdvisor
✅ Koble throttlet HealthTrendUpdated-events fra Health Intelligence Layer
✅ Tester dekker at HealthTrendUpdated lagres uten å øke intervention-tellere
✅ Koble SessionStarted fra ExerciseWindow start-flow
✅ Tester dekker åpen start-session og at completion oppdaterer samme session uten duplikat

Gjenstår i denne modulen:

✅ Ingen åpne punkter i dette dokumentet per 2026-05-31
