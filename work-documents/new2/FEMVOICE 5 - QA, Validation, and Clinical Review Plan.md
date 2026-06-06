# FEMVOICE 5 - QA, Validation, and Clinical Review Plan

## Formål

Dette dokumentet definerer hvordan FemVoice bør testes videre før det kan vurderes som et seriøst hjelpeverktøy for stemmefeminisering.

## Viktig avgrensning

Automatiserte tester kan bekrefte at kode fungerer. De kan ikke alene bekrefte at feedback er klinisk riktig, trygg eller emosjonelt god for en trans bruker. Det krever manuell QA, brukerfeedback og helst gjennomgang av kvalifisert stemmefagperson.

## Testområder

### 1. Mikrofon og lyd

Må testes manuelt:

- USB-mikrofon
- Jack/analog mikrofon
- Headset-mikrofon
- Innebygd laptop-mikrofon
- Svak mic-input
- Støyende rom
- Lav stemme/humming
- Avstand nær/fjern

Krav:

- Appen bruker Windows standard input.
- Kalibrering lagres per device.
- Kalibreringsflyten er manuell mellom fasene: bruker starter måling av stille rom og stemme/humming selv.
- Kalibrering viser live RMS/dBFS og sluttstatus for SNR og peak-nivå.
- Lav-output mic kan fortsatt gi "voice detected".
- Dårlig signal gir teknisk råd, ikke stemme-kritikk.
- For lavt signal, signal nær støy og for høyt/clipping-nært signal gir forskjellige råd.
- HearOwnVoice er av med mindre brukeren aktivt har slått det på.

### 2. Exercise lifecycle

Må testes:

- Start exercise
- Stop exercise
- Pause/restart
- Timer
- Live feedback
- Guidance
- Hold/status
- Summary
- SmartCoach update etter økt

Krav:

- Ingen freeze.
- Ingen tom live feedback når signal er godt.
- Humming-øvelser behandles som humming.
- Timer fortsetter selv om feedback confidence er lav.

### 3. Klinisk feedback

Manuell gjennomgang:

- Coach-meldinger
- Guidance-tekst
- Safety/recovery-meldinger
- Progression/goal-tekst
- Feilmeldinger for mic

Krav:

- Ingen skam.
- Ingen binær "du høres mannlig ut".
- Ingen pitch-jakt.
- Safety/risk meldinger er tydelige og milde.
- Appen anbefaler pause/fagperson ved relevante symptomer.

### 4. Språk

Må testes:

- Norsk
- Engelsk
- Språkbytte mens appen er åpen
- Exercise Guide title/labels
- Summary/progress
- Calibration wizard

Krav:

- Ingen resource keys synlige.
- Ingen norsk tekst i engelsk UI.
- Klinisk språk er likt trygt på norsk og engelsk.

### 5. Tilgjengelighet og UX

Må testes:

- Lite vindu
- Skjermskalering
- Mye tekst
- Keyboard navigation
- Kontrast i dark/light theme
- Scroll i Exercise Guide

Krav:

- Brukeren ser live status uten å scrolle under øvelse.
- Viktige safety-meldinger er synlige.
- Ingen tekst overlapper.
- Farger er ikke eneste indikator.

## Klinisk review

Før appen beskrives som klinisk trygg bør minst én kvalifisert fagperson se gjennom:

- Exercise taxonomy
- Feedback cues
- Safety thresholds
- Recovery policy
- Language guardrails
- Disclaimer

Rolle:

- SLP/logoped med erfaring i gender-affirming voice care
- Eventuelt laryngolog for medisinsk safety og røde flagg

## Brukerfeedback

Minste anbefalte runde:

- 3-5 transfeminine brukere
- Minst én bruker med USB mic
- Minst én med jack/headset mic
- Minst én med lav/stille stemme
- Kort intervju etter bruk

Spørsmål:

- Føltes feedback trygg?
- Var målene forståelige?
- Ble du presset mot pitch?
- Hjalp appen deg å høre resonans/komfort?
- Var noe dysfori-triggende?

## Automatiserte tester som bør legges til

P0:

- Safety copy policy test for nye RESX/coach-tekster. Status 2026-06-02: implementert for RESX-verdier.
- Humming mode test: ingen "speak/no speech" copy i humming-øvelser. Status 2026-06-02: implementert for `ResonanceHumming`-ressursverdier.
- Confidence-aware feedback test: svak mic gir calibration cue. Status 2026-06-02: implementert for realtime no-voice og breathing low-intensity feedback via `FeedbackSignalPolicyTests`.
- Exercise timer test uavhengig av live feedback result. Status 2026-06-02: implementert via `ExerciseSessionTimerStateTests`; lokal fallbacktimer tikker selv når ViewModel/live feedback ikke leverer sekunder.

P1:

- VoiceGoalProfile affects SmartCoach. Status 2026-06-02: implementert med `VoiceGoalProfile`, `IVoiceGoalProfileProvider`, Settings-UI, RESX-tekster og SmartCoach-/persistenstester. Full førstegangs-onboarding gjenstår som senere produktarbeid.
- Recovery practice counts toward weekly goal. Status 2026-06-02: implementert med `TrainingSession.IsRecoveryPractice`; recovery teller i sessions/minutter, men ekskluderes fra performance averages.
- Device calibration remains stable when device order changes. Status 2026-06-02: implementert med normalisert mikrofonnavn og testet mot WaveIn device-index-endring.

P2:

- Export report snapshot tests. Status: framtidig feature/testarbeid; ingen aktiv eksportflyt er identifisert som åpen blokkering i denne runden.
- Visual regression screenshots for Exercise Guide. Status: krever kjørende WPF/visuell QA og hører til manuell/visuell testfase.

## Release Gates

### Alpha

Automatiserte tester grønne.

### Beta

Manuell appflyt testet med USB og jack/analog mic.

### Release Candidate

Minst én full 30-min økt uten freeze, audio leak eller feedback failure.

### Production Ready

Klinisk språkreview, manuell WPF QA, språk/theme QA og brukerfeedback er gjennomført.

## Kilder

- WPATH SOC8 beskriver transgender healthcare som tverrfaglig og i utvikling: https://wpath.org/publications/soc8/
- UCSF anbefaler comprehensive voice evaluation ved stemmeplager og fremhever trygg, effektiv, pasientspesifikk behandling: https://transcare.ucsf.edu/guidelines/vocal-health
- ASHA beskriver gender-affirming voice and communication som bredere enn pitch og basert på brukerens selvbestemte mål: https://www.asha.org/practice-portal/professional-issues/gender-affirming-voice-and-communication/
- Johns Hopkins beskriver terapi, feedback, opptak/practice og vocal health som relevante deler av gender-affirming voice care: https://www.hopkinsmedicine.org/health/expert-qa/transgender-and-gender-diverse-voice-care
