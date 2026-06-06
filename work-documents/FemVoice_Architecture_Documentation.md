# FemVoice Architecture Documentation

Status: 2026-06-05

Dette dokumentet samler den kliniske og tekniske arkitekturen slik koden står nå. Fokus er biofeedback, helse/sikkerhet, progresjon, analytics, mikrofonrobusthet, språk og feedback-konsistens.

## System Overview

FemVoice Studio er et WPF-program med sanntidsmotorer i `Services`, presentasjonslogikk i `ViewModels` og visninger i `Views`.

Hovedflyten:

1. Audio/analyse-motorer produserer pitch, formanter, resonans, score og comfort-zone state.
2. `ExerciseIntelligenceCoordinator` samler dette til `ExerciseLiveState` og `InlineCoachMessage`.
3. `ExerciseDetailViewModel` oversetter live state til adaptive UI-egenskaper uten rå Hz der pitch ikke er primærmetrikken.
4. `VocalHealthSupervisor` og `HydrationAdvisor` vurderer belastning, fatigue, pause og hydrering.
5. `SessionAnalyticsStore` journalfører økt-, øvelse- og helsehendelser.
6. `ProgressionOrchestrator` vurderer historikk, helse og subjektiv rapport før progresjon.
7. `FeedbackPipeline` og `FeedbackConsistencyGuard` filtrerer all feedback før UI viser den.

Kjerneprinsipp: helse og sikkerhet har høyere prioritet enn progresjon, og resonans/stabilitet har høyere klinisk verdi enn pitch-jakt.

## Current Implementation Snapshot

Status per 2026-06-05:

- Appen bygger grønt med `dotnet build .\FemVoiceStudio.slnx -p:BaseOutputPath=.\bin\CodexBuild\`.
- Testpakken går grønt med `dotnet test .\FemVoiceStudio.slnx --no-build -p:BaseOutputPath=.\bin\CodexBuild\`: 115 app-tester og 329 testprosjekt-tester.
- Exercise Guide har kompaktere detail-layout slik at guidance, timer, live feedback og status er synlige med mindre scrolling.
- Exercise live feedback fungerer med timer-fallback, humming/speech-aware copy og signal-/kalibreringscue ved lav confidence.
- Main page pitch chart viser aktiv target-zone per vanskelighetsgrad, stabilisert pitch trace og trygge realtime-meldinger om under/innenfor/over målsonen.
- Mikrofonkalibrering lagres per device og bruker normalisert device-identitet slik at USB, jack/analog, headset og laptop-mikrofoner kan få egne terskler.
- Voice Goal Profile finnes i Settings og kan påvirke SmartCoach når safety/resonansprioritet tillater det.
- Spectrogram intelligence er koblet til `AnalyzerWindow` via `SpectrogramResonanceMapper`.
- RESX-policy tester blokkerer utrygg eller pressende språkbruk i brukerrettede ressurser.

## Core Real-Time Engines

### `ResonanceProxyEngine`

Lag: real-time analysis.

Ansvar: produserer resonansscore og `FormantSnapshot` med F1/F2/F3 slik at resten av systemet kan bruke resonans uten å kjenne rå FFT/audio.

Inputs / outputs: analysepipeline inn, resonansscore og formanter ut.

Event-integrasjoner: `ExerciseIntelligenceCoordinator` abonnerer på resonans/formanter. `AnalyzerWindow` bruker formantdata via `SpectrogramResonanceMapper`.

Klinisk rolle: lærer fysisk resonansplassering, ikke bare tonehøyde.

Mulig forbedring: hold visualiseringsregler utenfor motoren.

### `FemVoiceScoreEngine`

Lag: real-time scoring.

Ansvar: leverer normalisert score som kan kombineres med resonans, stabilitet og comfort-zone.

Klinisk rolle: støtter helhetsfeedback, men skal ikke alene drive helse eller progresjon.

Mulig forbedring: dokumenter scorekomponentene tydeligere hvis de eksponeres for bruker.

### `ComfortZoneController`

Lag: real-time safety/adaptation.

Ansvar: beregner comfort-zone state og safety lock for pitch-relaterte øvelser.

Klinisk rolle: hindrer faste Hz-mål og bruker pitch som komfortområde.

Mulig forbedring: all ny pitch-UI må gå via profil/comfort-zone, ikke hardkodede tall.

### `PitchTargetZonePolicy`

Lag: target-zone policy.

Ansvar: definerer trygge, vanskelighetsavhengige pitch-soner for hovedgrafen og klemmer øvelsesmål slik at avansert nivå ikke presser brukeren over klinisk ønsket øvre grense.

Klinisk rolle: gjør pitch til en komfort-/målsonemåling, ikke en instruks om å jage stadig høyere Hz. Dagens policy holder avansert øvre sone på 240 Hz.

Testdekning: `PitchTargetZonePolicyTests` dekker nivåsoner og clamping.

### `PitchTraceStabilizer`

Lag: visual signal stabilization.

Ansvar: filtrerer pitch-verdien som tegnes i hovedgrafen, korrigerer sannsynlige 2x/3x/4x harmoniske hopp og avviser urealistiske enkeltspikes når stemmen ellers ligger lavere.

Klinisk rolle: hindrer at mørkere stemme eller dårlig pitch-detection feilaktig vises som en brå topp til svært høy pitch. Brukeren skal kunne se om stemmen faktisk ligger mørkere/lavere eller lysere/høyere.

Testdekning: `PitchTraceStabilizerTests` dekker harmonic correction, gradvis bevegelse og avvisning av ekstrem førsteverdi.

### `PitchChartAxisRangeCalculator`

Lag: chart visualization policy.

Ansvar: beregner visningsområde for pitch-grafen slik at aktiv target-zone og faktisk pitch er synlig uten å låse aksen til gamle hardkodede verdier.

Klinisk rolle: støtter tydelig visuell feedback for nybegynner, middels og avansert uten å gjøre avansert sone bredere eller mer pressende.

## Exercise Intelligence Layer

### `ExerciseTargetProfile`

Lag: domain model.

Ansvar: beskriver hvilke metrikker øvelsen bruker: resonans, pitch, stabilitet, intensitet, hold-lengde og adaptive terskler.

Inputs / outputs: opprettes av `ExerciseProfileFactory`, profile store eller `ProgressionOrchestrator`; leses av coordinator og viewmodel.

Klinisk rolle: gjør feedback spesifikk per øvelse. Resonansøvelser slipper pitch-jakt, mens glide/intonasjon kan vise pitch som primærfeedback.

### `ExerciseLiveState`

Lag: live state model.

Ansvar: bærer normaliserte sanntidsverdier fra coordinator til UI, health/hydration og analytics.

Klinisk rolle: hindrer at UI tolker rå audio selv.

### `InlineCoachMessage`

Lag: feedback model.

Ansvar: beskriver korte coach-signaler med reason code, severity og auto-dismiss.

Event-integrasjon: publiseres fra coordinator og mappes via `InlineCoachFeedbackMapper` før `FeedbackPipeline`.

### `ExerciseIntelligenceCoordinator`

Lag: application service, UI-agnostisk.

Ansvar:

- Samler events fra `ResonanceProxyEngine`, `FemVoiceScoreEngine`, `ComfortZoneController`, `VoiceHealthMonitor` og `SmartCoachEngine`.
- Evaluerer live state med adaptive profilgrenser.
- Fryser hold-progress under safety lock.
- Publiserer `ExerciseLiveState` og `InlineCoachMessage`.

Kjernealgoritmer:

- Primary metric velges fra aktiv profil.
- Pitch normaliseres bare når pitch brukes.
- Hold-progress øker bare når profilens metrikker er innenfor mål og safety ikke er låst.
- Coach messages rate-limites per reason code.

Helse- og sikkerhetsrolle: safety lock overstyrer hold/progresjon og blokkerer ordinær teknikkfeedback.

Mulig forbedring: flytt hardkodede fallbackmeldinger til localization/mapper hvis de blir synlige uten pipeline.

### `ExerciseDetailViewModel`

Lag: presentation/application boundary.

Ansvar:

- Er broen mellom coordinator og exercise-UI.
- Oversetter live state til bindbare UI-egenskaper.
- Skjuler pitch-retning når pitch ikke er primærprofil.
- Viser klinisk score-loop, status og progresjonsforklaring.

Klinisk rolle: beskytter mot overfokus på rå Hz og prioriterer resonans/stabilitet/safety.

Mulig forbedring: flytt mer av direkte label-logikken fra `ExerciseWindow.xaml.cs` til VM over tid.

### `SpectrogramResonanceMapper`

Lag: visual mapping service.

Ansvar: mapper formanter, resonansscore og brightness til testbar spectrogram visual state.

Klinisk rolle: viser formanttopper og fremre resonanssone uten å belønne høy pitch som proxy.

Mulig forbedring: utvid tester for flere stemmeprofiler når formantmål personaliseres.

## Vocal Health & Hydration Layer

### `VocalHealthSupervisor`

Lag: health intelligence service.

Ansvar: vurderer strain, fatigue, pausebehov og safety state fra normaliserte live-metrikker.

Inputs / outputs: `ExerciseLiveState` og baseline inn; `VocalHealthDecision`, analytics events og feedback mapper ut.

Kjernealgoritmer: trendbasert evaluering med spike/noise-filter, strain raskere enn fatigue, recovery med stabil forbedring.

Helse- og sikkerhetsrolle: kan eskalere til `Caution`, `Restrict` eller `Lock`; low performance alene er ikke helserisiko.

### `HydrationAdvisor`

Lag: supportive physiology service.

Ansvar: foreslår hydrering basert på resonansdrift, stabilitetsvarians og akkumulert vokal load.

Klinisk rolle: støtter restitusjon, men styrer aldri session flow alene.

### `VocalHealthLegacyBridge`

Lag: compatibility adapter.

Ansvar: mapper eldre `VoiceHealthMonitor` warning/critical/lockout til ny health decision-flyt.

Mulig forbedring: fjernes når eldre health-events er migrert.

## Microphone Compatibility Layer

### `MicrophoneCalibrationService`

Lag: audio calibration service.

Ansvar: måler stille rom og stemme/humming manuelt, beregner noise floor, voice RMS, SNR, peak dBFS, voiced threshold og compatibility flags.

Klinisk rolle: skiller teknisk signalproblem fra stemmefeedback. Lavt eller støyete signal skal gi råd om mikrofon/kalibrering, ikke negativ vurdering av stemmen.

Implementert robusthet:

- Kalibrering lagres per mikrofonprofil.
- Device-navn normaliseres slik at device order-endringer ikke blander profiler.
- Lav-output mikrofoner kan få mer sensitive voiced thresholds når SNR er brukbar.
- Kompatibilitetsflagg dekker low output, high noise floor, clipping risk, possible noise gate og possible AGC/compression.
- Kalibrerte terskler brukes i analyzer, real-time analysis og subsystem capture.

Testdekning: `MicrophoneCalibrationServiceTests` dekker device-stabilitet, terskler, compatibility flags og calibration failure/advice cases.

Mulig forbedring: legg inn eksplisitt UI-valg mellom presis måling og robust mic-modus.

### `MicrophoneCalibrationProfile`

Lag: audio profile model.

Ansvar: bærer kalibrerte måleverdier og compatibility flags for valgt device.

Klinisk rolle: gjør feedback mer rettferdig på tvers av USB, jack/analog, headset og laptop-mikrofoner.

### Audio capture/analyzer integration

Filer: `AudioCaptureService`, `AudioAnalyzerService`, `AudioAnalysisEngine`.

Ansvar: bruke kalibrert frame RMS, soft noise gate og low-output sensitivity uten destruktiv per-sample gating.

Klinisk rolle: humming og lav/stille stemme skal kunne detekteres når signalet faktisk er brukbart, samtidig som dårlig signal merkes som teknisk usikkert.

## Progression System

### `VoiceGoalProfile` / `LocalVoiceGoalProfileStore`

Lag: personalization model and persistence.

Ansvar: lagrer brukerens stemmemål og primærfokus, for eksempel balansert fokus, resonans, intonasjon, pust/luftflyt eller pitch-komfort.

Klinisk rolle: støtter at en trans bruker kan ha egne stemmemål uten at systemet gjør binære antakelser om "riktig" femininitet. Profilen kan påvirke SmartCoach bare når helse, sikkerhet og resonansprioritet tillater det.

Integrasjon:

- Settings har brukerflate for Voice Goal Profile.
- `IVoiceGoalProfileProvider` gir profilen til tjenester som trenger den.
- `SmartCoachEngine` kan bruke `PrimaryFocus` som prioriteringssignal når det ikke bryter safety/regler.

Testdekning: `LocalVoiceGoalProfileStoreTests` og SmartCoach-relaterte tester dekker lagring og trygg bruk av profil.

### `ProgressionOrchestrator`

Lag: application service for post-session decisions.

Ansvar:

- Evaluerer historiske økter og helsehendelser.
- Foreslår adaptive profile updates, pause, regresjon, plateau eller variasjon.
- Tar inn `SubjectiveReport` for helsebekymring og motivasjonsfall.

Inputs / outputs: `SessionAnalyticsStore`, `ProgressionOrchestratorContext`, `SubjectiveReport` og `ExerciseTargetProfile` inn; `ProgressionOrchestratorDecision` og events ut.

Kjernealgoritmer:

- Composite score vekter resonans, stabilitet og hold.
- Safety/fatigue vurderes før performance.
- Progresjon krever konsistent forbedring over flere økter.
- Pitch comfort skaleres først etter resonans/stabilitet.
- Recovery practice teller positivt i økter/minutter, men ekskluderes fra performance averages.

Helse- og sikkerhetsrolle: safety, fatigue, subjektiv helsebekymring og motivasjonsfall kan pause/regressere progresjon.

Event-integrasjon: `ExerciseWindow` sender subjektiv rapport etter økt via `OnSubjectiveReportSubmittedAsync`.

Mulig forbedring: persister subjektive rapporter historisk hvis rapportene skal vises i dashboards.

### `ExerciseProfileStore`

Lag: persistence adapter.

Ansvar: lagrer personaliserte profile overrides per bruker/øvelse med in-memory og SQLite-backed implementasjoner.

Klinisk rolle: gjør progresjon personlig og trygg over tid.

Mulig forbedring: legg på migrering/versjonering hvis profilformatet endres.

## Analytics & Data Layer

### `SessionAnalyticsStore`

Lag: application data service.

Ansvar:

- Journalfører session start/completion.
- Lagrer exercise summaries.
- Lagrer safety, pause, hydration og health trend events.
- Tilbyr trenddata til progression og dashboards.

Klinisk rolle: gir historisk vurdering uten å lagre rå audio.

Mulig forbedring: UI bør fortsatt lese aggregerte modeller, ikke SQL direkte.

### Repositories

Filer: `ISessionAnalyticsRepository`, `InMemorySessionAnalyticsRepository`, `SqliteSessionAnalyticsRepository`.

Ansvar: skjuler lagringsdetaljer fra analytics/orchestrator.

Klinisk rolle: gjør testing isolert og produksjonsdata persistent.

## Feedback Consistency Layer

### `FeedbackConsistencyGuard`

Lag: policy service.

Ansvar: prioriterer, filtrerer, rate-limiter og eskalerer `FeedbackCandidate`.

Kjernealgoritmer:

- Safety/health overstyrer progression og praise.
- Pause overstyrer teknikkhint.
- Aktiv strain blokkerer ros.
- Konflikter håndteres med suppression/eskalering.

Klinisk rolle: hindrer motstridende eller stressende feedback.

Mulig forbedring: vurder audit-logg for suppressed/escalated decisions.

### `FeedbackPipeline`

Lag: event gateway.

Ansvar: kjører candidates gjennom guard og publiserer `FeedbackApproved`, `FeedbackSuppressed` og `FeedbackEscalated`.

Klinisk rolle: felles port for alle synlige meldinger.

### Feedback mappers

Filer: `SmartCoachFeedbackMapper`, `InlineCoachFeedbackMapper`, `ProgressionFeedbackMapper`, `HydrationFeedbackMapper`, `VocalHealthFeedbackMapper`.

Ansvar: oversetter modulspesifikke events til `FeedbackCandidate` og `FeedbackGuardContext`.

Klinisk rolle: bevarer prioritet, severity, localization key og conflict key på tvers av moduler.

### `FeedbackService`

Lag: legacy/main-page feedback service.

Ansvar: genererer session feedback og sanntidstekst for hovedsiden.

Nåværende realtime-regel:

- Ingen voiced signal gir signal-/mikrofonkalibreringscue.
- Pitch under aktiv target-zone beskrives som under målsonen og lavere/mørkere enn grønn sone.
- Pitch innenfor aktiv target-zone beskrives som komfortabelt innenfor målsonen med resonansfokus.
- Pitch over aktiv target-zone beskrives som høyere/lysere enn grønn sone og ber brukeren slippe ned uten press.

Klinisk rolle: hovedskjermen skal samsvare med pitch-grafen og ikke bruke gamle pressende pitch-kommandoer.

Testdekning: `FeedbackSignalPolicyTests` dekker no-voice, under/innenfor/over target-zone, breathing low-intensity og utrygg språkbruk.

## Localization & Language Guardrails

### `LocalizationService` / RESX resources

Lag: UI language and copy.

Ansvar: leverer brukerrettede tekster for norsk, engelsk og øvrige språkfiler via RESX.

Implementert:

- Exercise Guide title/labels og progress-tekster er flyttet til språkfiler.
- Realtime pitch feedback bruker nye target-zone nøkler i alle RESX-filer.
- Chart labels i analyzer/pitch/resonance er flyttet til RESX der de er brukerrettede.
- Humming-øvelser behandles språklig som humming, ikke som tale/speech.
- Safety-copy policy blokkerer pressende eller dysfori-triggende tekst i ressurser.

Testdekning:

- `ResourceTextPolicyTests` blokkerer utrygge fraser som pitch-press, binære stemmefasiter og projection-copy.
- Humming resource policy hindrer speech/speak/snakk/tale-instruksjoner i humming-ressurser.

Mulig forbedring: enkelte gamle backupfiler og tekniske debugstrenger kan fortsatt forvirre ved søk, selv om de ikke er aktiv UI.

## Event Flow Maps

### Live exercise loop

```text
Audio/analyse
  -> ResonanceProxyEngine / FemVoiceScoreEngine / ComfortZoneController
  -> ExerciseIntelligenceCoordinator
  -> ExerciseLiveState
  -> ExerciseDetailViewModel
  -> ExerciseWindow live feedback
```

### Inline coaching loop

```text
ExerciseIntelligenceCoordinator
  -> InlineCoachMessage
  -> InlineCoachFeedbackMapper
  -> FeedbackPipeline
  -> FeedbackConsistencyGuard
  -> FeedbackApproved
  -> ExerciseDetailViewModel visible coach message
```

### Health and hydration loop

```text
ExerciseLiveState
  -> VocalHealthSupervisor / HydrationAdvisor
  -> VocalHealthDecision / HydrationAdvice
  -> SessionAnalyticsStore events
  -> Feedback mappers
  -> FeedbackPipeline
  -> UI only when approved
```

### Post-session progression loop

```text
Exercise completed
  -> SessionAnalyticsStore summary
  -> SubjectiveReportPanel
  -> ProgressionOrchestrator
  -> ProgressionOrchestratorDecision
  -> ExerciseProfileStore override when profile changes
  -> ProgressionFeedbackMapper
  -> FeedbackPipeline
```

### Subjective post-session loop

```text
Exercise stopped
  -> SubjectiveReportPanel
  -> SubjectiveReport
  -> ProgressionOrchestrator.OnSubjectiveReportSubmittedAsync
  -> ProgressionPaused when strain/fatigue/low comfort/readiness drop is reported
  -> ExerciseProfileUpdated only when subjective safety check is clear
  -> FeedbackPipeline
```

### Analyzer spectrogram loop

```text
Audio analyzer
  -> ResonanceProxyEngine formants/resonance
  -> SpectrogramResonanceMapper
  -> AnalyzerWindow overlays
  -> clinical score status
```

### Main page pitch graph loop

```text
AudioAnalysisEngine / AudioAnalyzerService
  -> MainViewModel CurrentPitch / SmoothedPitch
  -> PitchTargetZonePolicy active target-zone
  -> PitchTraceStabilizer visual pitch value
  -> PitchChartAxisRangeCalculator chart axis
  -> FeedbackService target-zone realtime text
  -> MainWindow pitch chart and realtime feedback
```

### Microphone calibration loop

```text
Settings
  -> MicrophoneCalibrationWindow
  -> MicrophoneCalibrationService quiet-room phase
  -> MicrophoneCalibrationService voice/humming phase
  -> MicrophoneCalibrationProfile per device
  -> calibrated thresholds in analyzer/live capture
  -> technical advice when signal quality is weak/noisy/clipping/processed
```

## Clinical Validation Notes

Resonans prioriteres korrekt:

- `ExerciseTargetProfile` definerer om resonans er primærmetrikken.
- `ProgressionOrchestrator` øker resonanskrav før pitch comfort.
- Spectrogrammet viser formanter og fremre resonanssone.

Pitch misbrukes ikke:

- Exercise-UI skjuler pitch-retning for ikke-pitch-profiler.
- Pitchstatus viser comfort-zone status, ikke rå Hz.
- Pitch comfort skaleres først etter stabil resonans/stabilitet.
- Main page pitch-graf viser target-zone per vanskelighetsgrad og stopper avansert øvre mål ved 240 Hz.
- Realtime pitch-tekst forklarer under/innenfor/over målsonen i komfortspråk, ikke som krav om å gå høyere.
- Pitch trace filtrerer sannsynlige harmonic spikes slik at mørkere stemme ikke feilvises som brå høy pitch.

Mikrofonvariasjon håndteres:

- Per-device calibration lagrer terskler for valgt mikrofon.
- Lav-output, støy, clipping-risk, noise gate og AGC/compression får tekniske råd.
- Humming/lav stemme skal håndteres som målemodus, ikke som "ingen snakking" når signalet er brukbart.
- Manuell USB/jack/headset/laptop QA gjenstår før release readiness kan hevdes.

Fatigue håndteres:

- `VocalHealthSupervisor` bruker trendbasert fatigue.
- `HydrationAdvisor` gir støtte uten å eskalere safety state.
- `ProgressionOrchestrator` pauser/regresserer ved fatigue history.

Safety locks er konsekvente:

- Coordinator fryser hold-progress.
- Feedback guard undertrykker lavere prioritet under safety/health.
- Analytics journalfører safety events.

Progresjon er helsefokusert:

- Health, pause, subjective health concern og motivation drop vurderes før vanlig progresjon.
- Progresjon krever konsistent historikk, ikke enkeltscore.
- Subjektiv rapport etter økt kan pause progresjon selv om automatiske målinger ser gode ut.
- Profile updates etter økt skjer først etter at brukeren har fått rapportere komfort, tretthet og press.

## UI/UX Polish Findings

- Exercise live feedback forklarer resonans, stabilitet, hold, score og safety samlet.
- Pitch-UI er redusert der pitch ikke er primærmål.
- Exercise Guide detail-layout er strammet inn slik at live feedback/status/timer er enklere å se uten tung scrolling.
- Main page pitch chart har theme-aware styling, aktiv target-zone, stabilisert live trace og target-zone copy.
- Analyzer gir pedagogisk resonansoverlay.
- Øvelsessiden viser kort subjektiv stemmerapport etter stopp, slik at brukeren kan rapportere press/tretthet før progresjon låses inn.
- `ExerciseWindow.xaml.cs` har fortsatt noe orchestration og direkte UI-labeloppdatering.
- Noen gamle `.old`/backupfiler ligger fortsatt i treet og kan gi falske treff ved tekstsøk.

## Improvement Backlog

Arkitektur:

- Reduser code-behind-ansvar i `ExerciseWindow.xaml.cs`.
- Versjoner profile override-formatet i `ExerciseProfileStore`.
- Vurder audit-logg for suppressed/escalated feedback decisions.
- Rydd eller flytt `.old`/backupfiler ut av aktivt prosjekttre når det ikke lenger trengs for sammenligning.

Klinisk:

- Persister subjektive rapporter hvis de skal brukes i historiske dashboards.
- Dokumenter scorevekter i brukerrettet språk hvis de eksponeres.
- Utvid tests for flere stemmeprofiler/formantmål.
- Fullfør manuell klinisk språk-/feedbackreview med kvalifisert stemmefagperson før appen omtales som klinisk validert.

Ytelse:

- Profilér analyzer overlay ved høy FFT-rate.
- Sørg for at health/hydration-evaluering forblir throttlet under lange økter.

UX:

- Legg til tydelig Settings-status for aktiv mic-profil og profile health.
- Legg til eksplisitt "presis måling" vs "robust mic-modus" hvis manuell QA viser behov.
- Kjør visuell QA for Exercise Guide på små vinduer, høy DPI og dark/light theme.

Fremtidige AI-muligheter:

- Bruk analytics summaries, ikke rå audio, som input til personaliserte forslag.
- La AI forklare trends etter økt, men ikke overstyre safety/progression policies.

## Consistency Check

Matcher dokumentasjonen koden: ja, basert på eksisterende services, viewmodels, tests og roadmapstatus per 2026-06-05.

Ansvarsoverlapp: `ExerciseWindow.xaml.cs` har fortsatt noe orchestration. `MainWindow.xaml.cs` håndterer fortsatt pitch chart rendering direkte, men klinisk target-zone/trace-policy ligger i testbare services. Analyzer har egen clinical score-formel, separat fra exercise composite score.

Eventmangler: ingen kritiske mangler funnet per 2026-06-05. Subjektiv post-session rapport er koblet til orchestrator fra exercise-UI. `SessionStarted` i `SessionAnalyticsStore` står fortsatt som optional/ikke-kritisk i tidligere dokumentgjennomgang.

Logikkhull: ingen kritiske safety-first-hull funnet. Testgap: analyzer clinical score og full visuell WPF-flyt er ikke unit-testet separat. Manuell USB/jack/headset/laptop-mikrofon-QA gjenstår.

## Stop-and-Validate Summary

Dokumentasjonsfasen gir:

- Seksjonert arkitekturdokumentasjon.
- Fil-for-fil dokumentasjon for klinisk sentrale filer.
- Eventflyt-kart for live feedback, health, hydration, progression, analyzer, main pitch graph og microphone calibration.
- Klinisk validering av resonans, pitch, fatigue, safety, mikrofonvariasjon og progresjon.
- UI/UX-polish-funn og strukturert improvement backlog.
