# FEMVOICE - Release Verification Plan

Status: 2026-06-05

## 1. Purpose

Dette dokumentet definerer siste release-hardening og valideringsfase for FemVoice Studio. Det bygger bro mellom ferdig implementasjon og release readiness ved å beskrive hva som må verifiseres før appen kan behandles som en release candidate.

Målet er ikke å starte ny feature-design. Målet er å bekrefte at eksisterende arkitektur, biofeedback, mikrofonflyt, språk, helsebeskyttelse, dataflyt og UI fungerer stabilt sammen i reell bruk.

## 2. Release Phase Definition

Gjeldende fase er:

**Release Verification & Hardening Phase**

I denne fasen skal ny feature work minimeres. Ny kode bør bare tas inn når den:

- fikser en release blocker
- lukker et testet sikkerhets-/helseproblem
- fikser en WPF/UI-feil som hindrer normal bruk
- fikser data-, privacy-, localization- eller mikrofonfeil som gjør release utrygg
- gjør eksisterende funksjon testbar eller stabil uten å endre produktomfang

Ikke-blokkerende forbedringer flyttes til post-RC backlog.

## 3. Verification Commands

Kjør fra repo-/workspace-roten:

```powershell
dotnet build .\FemVoiceStudio.slnx -p:BaseOutputPath=.\bin\CodexBuild\
dotnet test .\FemVoiceStudio.slnx --no-build -p:BaseOutputPath=.\bin\CodexBuild\
```

For release candidate må begge kommandoer være grønne uten nye warnings som peker på release-relevant risiko.

Aksept:

- App-prosjekt bygger.
- Testprosjekt bygger.
- Alle unit/integration tests passerer.
- Ingen test er ignorert for å skjule release blocker.
- Testresultat dokumenteres med dato og testtall i release notes eller verification log.

## 4. Build and Test Verification

Automatisert verifikasjon skal dekke:

- `ExerciseIntelligenceCoordinator`
- `ExerciseLiveState`
- `ExerciseDetailViewModel`
- timer fallback
- guidance system
- pitch target-zone policy
- pitch trace stabilization
- pitch chart axis policy
- per-device microphone calibration
- Voice Goal Profile persistence
- SmartCoach goal/profile integration
- spectrogram resonance mapping
- `VocalHealthSupervisor`
- `HydrationAdvisor`
- `SessionAnalyticsStore`
- `ProgressionOrchestrator`
- `FeedbackConsistencyGuard`
- `FeedbackPipeline`
- feedback mappers
- RESX safety/language policy tests
- subjective post-session report flow

Release blocker hvis:

- build feiler
- testpakke feiler
- testflakiness treffer samme område gjentatte ganger
- safety/language policy test feiler
- mikrofonkalibrering eller pitch-stabilisering feiler i test
- en test må fjernes for å få grønn pipeline

## 5. Module Integration Verification

Verifiser at modulene fungerer sammen, ikke bare isolert.

### Exercise Live Loop

Sjekk:

- Exercise start stopper ikke UI.
- Timer starter og fortsetter.
- Guidance vises før og under øvelse.
- Live feedback fylles når signal er brukbart.
- Humming-øvelser behandles som humming, ikke som tale/speech.
- Hold/status oppdateres uten å vise misvisende 0% når signal ikke er klart.
- Stop viser summary/subjektiv rapport uten freeze.

### Feedback and Safety Loop

Sjekk:

- Safety/health overstyrer ros og progresjon.
- Aktiv strain/fatigue gir mild, tydelig pause/recovery-copy.
- Hydration-råd rate-limites.
- FeedbackConsistencyGuard undertrykker konfliktmeldinger.
- Low-confidence signal gir teknisk mic/kalibreringscue, ikke negativ stemmefeedback.

### Progression and Analytics Loop

Sjekk:

- Completed sessions lagres riktig.
- Recovery practice teller i økter/minutter, men ikke performance averages.
- ProgressionOrchestrator vurderer health før performance.
- Subjective report kan pause eller blokkere progresjon.
- SmartCoach oppdaterer uke/progresjon etter økt.

### Main Page Pitch Loop

Sjekk:

- Pitch-grafen beveger seg når brukeren lager lyd.
- Mørkere/lavere stemme vises lavere, ikke som falske spikes til høy pitch.
- Target-zone matcher vanskelighetsgrad.
- Avansert øvre sone stopper ved 240 Hz.
- Realtime tekst sier under/innenfor/over målsonen i komfortspråk.

## 6. Manual WPF UI QA

Manuell WPF-test må kjøres på minst:

- standard vindusstørrelse
- mindre vindu
- høy DPI / Windows scaling
- light theme
- dark theme
- norsk språk
- engelsk språk

Sjekkliste:

- Ingen crash ved navigasjon mellom hovedside, Exercise Guide, Analyzer, Settings, SmartCoach og Progression.
- Ingen synlige resource keys.
- Ingen norsk tekst i engelsk UI for sentrale flyter.
- Ingen tekst overlapper eller forsvinner i knapper/paneler.
- Exercise Guide krever ikke tung scrolling for å se live feedback/status under aktiv øvelse.
- Timer, guidance, status, live feedback og stop/start er synlige og forståelige.
- Theme-bytte oppdaterer pitch chart, cards, text, borders og statusfarger.
- Språkbytte oppdaterer titler, labels, exercise text og feedback-copy.
- Analyzer spectrogram viser resonansstatus uten layoutbrudd.
- Main page pitch chart viser target-zone, live pitch og scoreindikatorer.

Release blocker hvis:

- appen krasjer ved normal navigasjon
- exercise start/stop fryser UI
- sentral live feedback er tom med godt signal
- timer stopper feil
- sentral tekst vises som key
- språk/theme-bytte ødelegger aktiv side
- viktig safety-melding ikke er synlig

## 7. Microphone Hardware QA

FemVoice må testes manuelt med flere mikrofontyper fordi releasekvalitet avhenger av faktisk signal.

Minimum hardware matrix:

| Type | Må testes | Aksept |
| --- | --- | --- |
| USB mic | Ja | Kalibrering lagres per device, live pitch/resonans fungerer |
| Jack/analog mic | Ja | Lavere signal håndteres med teknisk advice og brukbar detection når SNR er god nok |
| Headset mic | Ja | Ingen clipping/noise gate uten tydelig advice |
| Laptop mic array | Ja | Støy/processing gir robust, teknisk feedback |
| Quiet/low voice | Ja | Appen ber ikke brukeren presse stemmen |
| Humming | Ja | Humming kan kalibreres og evalueres som humming |

Sjekkliste:

- Windows default input brukes riktig.
- Aktiv mic vises/oppfattes riktig i Settings/calibration.
- Kalibrering har quiet-room phase og voice/humming phase.
- Kalibrering viser RMS, dBFS, SNR og peak.
- Lav-output mic gir `LowOutput`/technical advice, ikke voice criticism.
- High noise floor gir noise advice.
- Clipping risk gir gain/volume advice.
- Noise gate/AGC/compression mistanke gir teknisk advice.
- Device profile er stabil selv om device order endres.
- HearOwnVoice er av med mindre brukeren aktivt har valgt det.

Release blocker hvis:

- USB mic fungerer, men jack/analog mic ikke kan kalibreres med brukbart signal
- lav-output mic alltid tolkes som ingen stemme uten advice
- humming ikke kan gjennomføres som egen målemodus
- wrong-device profile brukes etter restart/device reorder
- brukeren hører seg selv uten at HearOwnVoice er aktivert

## 8. Localization and Safety-Copy Audit

Automatiserte RESX-policytester må være grønne. I tillegg må norsk og engelsk manuelt leses gjennom for sentrale flyter.

Audit scope:

- Exercise Guide
- live feedback
- guidance
- calibration wizard
- SmartCoach dashboard/detail
- Progression
- Health/safety warnings
- Hydration advice
- Session summary
- Voice Goal Profile
- Analyzer status
- Main page realtime pitch feedback

Krav:

- Ingen resource keys synlige.
- Ingen pitch-pressende kommandoer.
- Ingen binær eller dysfori-triggende stemmefasit.
- Ingen "speak/talk" copy i humming-spesifikke instruksjoner.
- Low signal beskrives som signal/mic/kalibrering, ikke som brukerfeil.
- Safety-copy er tydelig, mild og handlingsrettet.
- Norsk og engelsk har samme kliniske intensjon.

Release blocker hvis:

- brukerrettet tekst kan tolkes som skam, binær fasit eller press
- engelsk UI viser norsk i sentrale flyter
- norsk UI viser keys eller tekniske placeholders
- safety warning mangler eller er for svak ved health lock/restriction

## 9. Clinical Language Review

Før release candidate bør språk og feedback vurderes av minst én person med relevant stemmefaglig kompetanse, helst logoped/SLP med erfaring fra gender-affirming voice care.

Review scope:

- Exercise taxonomy
- Guidance cues
- Realtime feedback
- Pitch target-zone copy
- Resonance and formant explanations
- Health/safety warnings
- Hydration/recovery advice
- Progression logic and user-facing explanations
- Voice Goal Profile wording
- Disclaimer and limitation language

Kliniske prinsipper som må bekreftes:

- Pitch behandles som ett signal, ikke som fasit.
- Resonans, komfort, stabilitet og helse prioriteres.
- Brukeren presses ikke mot høyere pitch.
- Appen støtter selvdefinerte stemmemål.
- Appen gir pause/recovery-råd ved tegn på belastning.
- Appen utgir seg ikke for å erstatte medisinsk eller logopedisk vurdering.

Release blocker hvis:

- klinisk reviewer finner copy eller flow som kan oppmuntre til press/skade
- appen kommuniserer binær femininitetsfasit
- health/safety messaging er utilstrekkelig eller uklart

## 10. Data and Privacy Review

FemVoice bør minimere sensitiv data og være tydelig på hva som lagres.

Sjekk:

- Rå audio lagres ikke uten eksplisitt framtidig feature/consent.
- Session analytics lagrer aggregerte målinger, ikke lydopptak.
- Microphone profiles lagrer tekniske signalverdier per device.
- Voice Goal Profile lagres lokalt.
- Debug logs er av som standard.
- Debug logs kan lukkes/ryddes og inneholder ikke mer data enn nødvendig.
- Database-tabeller er forståelige og ikke fylt med unødvendig sensitiv tekst.
- Eventuelle exports/reports er ikke aktive uten egen privacy review.

Release blocker hvis:

- rå audio eller detaljerte opptak lagres uventet
- debug logging står på som standard
- sensitiv brukerdata eksponeres i UI/logg uten grunn
- appen mangler tydelig forklaring om lokal lagring før release

## 11. Release Blocker Classification

### P0 - Release Blocker

Må fikses før release candidate:

- crash/freeze i normal exercise flow
- audio/mic flow fungerer ikke for vanlig USB eller jack/analog mic
- safety lock/health warning fungerer ikke
- live feedback tom med godt signal
- timer/start/stop ustabil
- testpakke eller build feiler
- utrygg eller dysfori-triggende copy i sentral UI
- data/privacy-avvik med rå audio eller debug data

### P1 - Release Hardening

Bør fikses før RC hvis risiko er synlig:

- layoutbrudd på små vinduer
- språkblanding i mindre sentrale paneler
- analyzer overlay performance issue
- unclear mic advice
- SmartCoach progress text oppdateres sent eller uklart
- sporadisk testflakiness

### P2 - Post-RC Backlog

Kan flyttes ut av release candidate:

- ny onboarding
- eksport/report snapshot feature
- robust/presis mic-modus toggle hvis dagens automatic mode er trygg nok
- avansert AI trendforklaring
- større redesign
- nye øvelseskategorier

## 12. Release Candidate Acceptance Criteria

FemVoice Studio kan merkes som release candidate når:

- Build er grønn.
- Alle tester er grønne.
- Manual WPF UI QA er gjennomført på norsk/engelsk og dark/light theme.
- Exercise start/stop/timer/live feedback/guidance/summary fungerer uten freeze.
- Main page pitch chart viser stabil live pitch og riktig target-zone.
- USB og jack/analog mikrofon er testet manuelt.
- Per-device calibration fungerer etter restart/device reorder.
- HearOwnVoice er bekreftet av som default.
- Localization/safety-copy audit er gjennomført uten P0.
- Clinical language review har ingen P0-funn.
- Data/privacy review har ingen P0-funn.
- Alle P0 blockers er lukket.
- P1-funn er enten fikset eller eksplisitt akseptert med begrunnelse.
- Post-RC backlog er oppdatert med P2-funn.

## 13. Final Release Verification Log Template

Bruk denne malen når release candidate verifiseres:

```text
Release candidate:
Date:
Build command:
Build result:
Test command:
Test result:
App tests:
Test project tests:

Manual WPF QA:
Norwegian:
English:
Dark theme:
Light theme:
Small window/high DPI:

Microphone QA:
USB mic:
Jack/analog mic:
Headset mic:
Laptop mic:
Low voice/humming:

Localization/safety audit:
Clinical language review:
Data/privacy review:

Open P0:
Open P1:
Accepted risks:
RC decision:
```
