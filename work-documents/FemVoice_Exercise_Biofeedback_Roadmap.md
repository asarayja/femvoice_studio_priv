OVERORDNET MÅL

Transformere øvelsessystemet til et klinisk intelligent real-time biofeedback-system med:

• adaptive terskler
• resonansfokus
• spectrogram-basert læring
• inline SmartCoach
• hydrering & sikkerhetslogikk

✅ STATUS I DAG

☑ ResonanceProxyEngine ferdig
☑ FemVoiceScoreEngine ferdig
☑ ComfortZoneController ferdig
☑ SmartCoachEngine ferdig
☑ Real-time pitch graf ferdig
☑ Real-time spectrogram ferdig

✅ Exercise Intelligence Upgrade fullført per 2026-06-01

🧠 FASE 1 — Exercise Intelligence Core

✅ Lage ExerciseTargetProfile (per øvelse mål & metrikker)
✅ Lage ExerciseLiveState (sanntidsstatus til UI)
✅ Lage InlineCoachMessage modell
✅ Lage ExerciseIntelligenceCoordinator service
✅ Koble alle realtime motorer via events
✅ Implementere adaptive terskler (ingen faste Hz-grenser)
✅ Safety lock + freeze-logikk
✅ Unit tests for alle scenarier

Status 2026-05-31:

✅ `ExerciseTargetProfile`, `ExerciseLiveState` og `InlineCoachMessage` finnes som testbare modeller
✅ `ExerciseIntelligenceCoordinator` kobler resonance, score, comfort zone, health og SmartCoach inn i en eventdrevet loop
✅ Adaptive terskler kommer fra profil/comfort-zone state, ikke hardkodet Hz i exercise-UI
✅ Safety lock fryser hold-progress og blokkerer ordinær teknikkfeedback
✅ Tester dekker hold, reset, safety freeze, lifecycle, inline coach og profile factory

🎯 Ferdig når: øvelser reagerer intelligent på mikrofoninput

📊 FASE 2 — Spectrogram Intelligence Layer

✅ Knytte ResonanceProxyEngine til spectrogram visualisering
✅ Markere fremre resonans-soner visuelt
✅ Highlight formant-topper (F1/F2/F3)
✅ Fargekode lys/mørk klang / presset lyshet
✅ Koble Spectral Centroid til UI brightness feedback
✅ Smoothing for pedagogisk visning
✅ Unit test mapping fra resonans → visual state

Status 2026-05-31:

✅ Implementert `SpectrogramResonanceMapper`
✅ Analyzer-spectrogram viser fremre resonans-sone som eget frekvensbånd
✅ Analyzer-spectrogram tegner F1/F2/F3-overlay fra `ResonanceProxyEngine`
✅ F2 markeres tydeligere enn F1/F3 siden den er viktigst for fremre resonans
✅ Resonansstatus viser fremre/bakre/balansert/for presset i stedet for å belønne høy pitch
✅ Brightness/spectral-centroid glattes før UI-status, så visningen ikke flimrer

🎯 Ferdig når: spectrogram lærer brukeren resonans fysisk

🎯 FASE 3 — Intelligent Exercise Feedback UI

✅ Resonansbar (adaptiv target range)
✅ Stability meter
✅ Comfort zone shield indikator
✅ Hold-progress sirkel
✅ Dynamisk visning per øvelsestype
✅ Fjerne pitch-jakt UI der det ikke er primært

Status 2026-05-31:

✅ Exercise feedback UI bruker `ExerciseDetailViewModel` som eneste bro mellom coordinator og UI
✅ Resonansstatus bruker profilens adaptive `TargetResonanceMin/Max` i stedet for faste UI-grenser
✅ Stabilitetsstatus bruker profilens adaptive `StabilityThreshold`
✅ Pitch-panelet vises bare for pitch-/glide-/intonasjonsprofiler der pitch er primær feedback
✅ Live pitch-feedback viser komfortsonestatus og ikke rå Hz
✅ Tester dekker dynamisk pitch-visning, adaptive resonansgrenser og Hz-fri pitchstatus

🎯 Ferdig når: hver øvelse viser riktig biofeedback

🤖 FASE 4 — SmartCoach Inline

✅ Inline hint i øvelsesvisning
✅ Rate-limit for meldinger
✅ Severity-nivåer
✅ Klinisk forklaringslogikk
✅ Samspill med safety locks

Status 2026-05-31:

✅ `ExerciseDetailViewModel` viser inline coach-meldinger i exercise-UI
✅ `ExerciseIntelligenceCoordinator` throttler coach-reason med 5 sekunders intervall
✅ Resonans, stabilitet, pitch, hold og safety bruker eksplisitte `MessageSeverity`-nivåer
✅ `InlineCoachFeedbackMapper` sender coach-hint gjennom `FeedbackPipeline`/`FeedbackConsistencyGuard`
✅ Safety lock blokkerer ordinære teknikkhint og slipper bare helse-/safety-warning gjennom
✅ Ressursnøkler finnes for inline coach-feedback i språkfilene

🎯 Ferdig når: coaching skjer kontinuerlig uten ekstra skjerm

💧 FASE 5 — Hydration & Fatigue Intelligence

☑ Lage HydrationAdvisor modul
✅ Knytte til stability fall + mørkere spektrum + load
✅ SmartCoach meldinger for pauser/vann
✅ Health-basert øktstyring
✅ Unit tests for triggere

Status 2026-05-31:

✅ `HydrationAdvisor` bruker resonansdrift, stabilitetsvarians og akkumulert vokal load
✅ Hydration-forslag undertrykkes under safety lock
✅ Hydration-feedback går via `HydrationFeedbackMapper`, `FeedbackPipeline` og `FeedbackConsistencyGuard`
✅ Exercise-flow journalfører `HydrationSuggested` og teller hydrering i session summary
✅ `VocalHealthSupervisor` håndterer pause/fatigue/health state og støtter health-basert øktstyring
✅ Tester dekker spike-filter, drift/variance/load-trigger, safety lock og hydration feedback-mapping

🎯 Ferdig når: systemet forebygger stemmeslitasje automatisk

📈 FASE 6 — Full Clinical Feedback Loop

✅ Spectrogram + pitch + resonans + score i samme økt
✅ Adaptiv progresjonskontroll per øvelse
✅ Inline forklaringer av fremgang
✅ Safety first UX ferdig

Status 2026-05-31:

✅ Analyzer viser spectrogram, live pitch, resonansoverlay og samlet klinisk score i samme visning
✅ Exercise live feedback viser score, loop-status og progresjonsforklaring sammen med resonans/stabilitet/pitch/hold
✅ Score og status bruker normaliserte state-verdier fra coordinator/VM og ikke faste Hz-targets
✅ Progresjonsforklaring prioriterer safety lock/pause før hold-ros eller progresjonsoppdatering
✅ `ProgressionOrchestrator` kjører per øvelse etter fullført session og persisterer adaptive profile overrides
✅ Tester dekker score-loop i `ExerciseDetailViewModel`, safety-first status og Hz-fri pitchstatus

🎯 Ferdig når: FemVoice fungerer som ekte klinisk biofeedback-system

🏁 SYSTEM FERDIG NÅR

☑ Alle faser over er grønne
☑ Alle tester passerer
☑ Ingen faste tall-targets
☑ All feedback adaptiv
☑ Ingen strain-progresjon mulig

📌 NOTATER
⭐ FORDEL MED DENNE ROADMAPEN

✔ Du vet alltid hva som er ferdig
✔ Du vet alltid neste steg
✔ Du unngår scope-kaos
✔ Du bygger klinisk korrekt
✔ Systemet vokser strukturert
