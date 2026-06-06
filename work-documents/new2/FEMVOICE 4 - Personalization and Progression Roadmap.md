# FEMVOICE 4 - Personalization and Progression Roadmap

## Formål

FemVoice bør bli mer personlig. En trans jente som feminiserer stemmen trenger ikke bare "mer pitch"; hun trenger et system som lærer hennes mikrofon, baseline, komfort, mål, fremgang og begrensninger.

## Prinsipp

Personalisering skal gjøre feedback mer rettferdig og trygg, ikke mer dømmende.

## Nåværende grunnlag

Prosjektet har allerede:

- Session analytics
- Progression orchestration
- Health Intelligence
- SmartCoach
- Per-user baselines
- Per-device mikrofonkalibrering

Neste steg er å koble disse tettere sammen.

## Foreslåtte features

### 1. Voice Goal Profile

Ny brukerprofil:

- Preferred voice direction: feminine / soft feminine / bright neutral / androgynous / custom
- Practice contexts: phone, work, friends, gaming, public speaking
- Safety limits: max session length, preferred reminders, known strain triggers
- Focus priority: resonance, weight, pitch comfort, intonation, confidence

Påvirker:

- SmartCoach
- Exercise recommendations
- Feedback wording
- Progression goals

### 2. Adaptive Baseline Model

Baseline bør være per:

- User
- Device
- Exercise type
- Mode: humming / vowel / word / sentence / free speech
- Time window: today, 7 days, 30 days

Hvorfor:

- Humming har andre signaler enn tale.
- Jack mic kan ha annet støygulv enn USB.
- Dagsform påvirker stemme.

### 3. Goal Progress Without Shame

Ukentlig mål bør telle:

- Fullført økt
- Recovery practice
- Komfortabel resonansøvelse
- Kort økt avsluttet riktig
- Journal/refleksjon

Ikke bare:

- Tid i pitchmål
- Høy score

### 4. SmartCoach Memory

SmartCoach bør huske:

- Hva fungerte sist
- Hvilke cues brukeren liker
- Når bruker ofte blir sliten
- Hvilke øvelser som gir stabil forbedring

Eksempel:

"Forrige gang fungerte rolig humming bedre enn høyere pitch. Vi starter med samme type lett resonans i dag."

### 5. Confidence-Aware Scoring

Hvis mic confidence er lav:

- Ikke senk score hardt.
- Vis teknisk melding: "Signalet er svakt. Sjekk mikrofonavstand eller kalibrering."
- Ikke gi klinisk/teknisk stemmefeedback som om målingen var sikker.

## Datamodeller

### `VoiceGoalProfile`

- UserId
- GoalStyleKey
- PrimaryFocus
- PracticeContexts
- SafetyPreferences
- PreferredCueStyle
- CreatedAt
- UpdatedAt

### `AdaptiveVoiceBaseline`

- UserId
- DeviceName
- ExerciseMode
- MetricName
- Median
- P10
- P90
- Confidence
- SampleCount
- UpdatedAt

### `CoachPreferenceMemory`

- UserId
- CueKey
- HelpfulnessRating
- LastUsedAt
- SuccessAfterCue

## Prioritet

P0:

- Voice Goal Profile. Status 2026-06-02: teknisk profilmodell/provider, Settings-UI og SmartCoach-kobling er implementert. Brukeren kan velge balansert fokus, resonans, intonasjon, pust/luftflyt eller pitch-komfort; full førstegangs-onboarding gjenstår som senere produktarbeid.
- Exercise mode-aware baseline: humming vs speech. Status 2026-06-02: humming-copy og low-signal policy er testet; full mode-baseline per humming/vowel/speech gjenstår som framtidig datamodellarbeid.
- Confidence-aware SmartCoach. Status 2026-06-02: low-signal/no-voice feedback er testet som signal-/kalibreringscue, ikke negativ stemmefeedback.

P1:

- Coach preference memory
- Recovery counted as progress. Status 2026-06-02: `TrainingSession.IsRecoveryPractice` teller i sessions/minutter, men ekskluderes fra performance averages.
- Context-specific goals

P2:

- Longitudinal trend reports
- Clinician export
- Optional cloud sync only with explicit consent

## Acceptance Criteria

- En humming-øvelse får ikke beskjed om at brukeren må "snakke".
- En lav-output mic gir teknisk calibration cue, ikke negativ stemmevurdering.
- Progression kan gå fremover selv med korte, trygge økter. Status 2026-06-02: recovery-practice teller positivt i ukefremdrift uten prestasjonspress.
- Brukeren kan definere stemmemålet sitt uten binær fasit. Status 2026-06-02: Settings har brukerflate for stemmemål og målstil, lagret via `LocalVoiceGoalProfileStore`, og SmartCoach bruker `PrimaryFocus` når safety/resonansprioritet tillater det.

## Kilder

- ASHA vektlegger selvbestemte stemmemål og bred gender-affirming communication: https://www.asha.org/practice-portal/professional-issues/gender-affirming-voice-and-communication/
- UCSF beskriver at trans kvinner kan ha varierende stemmemål og at maintenance/recalibration kan være nødvendig over tid: https://transcare.ucsf.edu/guidelines/vocal-health
- Johns Hopkins beskriver trening over tid, muscle memory og integrering som viktige deler av prosessen: https://www.hopkinsmedicine.org/health/expert-qa/transgender-and-gender-diverse-voice-care
