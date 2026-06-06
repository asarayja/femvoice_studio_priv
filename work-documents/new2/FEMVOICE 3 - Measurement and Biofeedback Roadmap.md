# FEMVOICE 3 - Measurement and Biofeedback Roadmap

## Formål

Dette dokumentet foreslår hvordan FemVoice kan måle mer nyttige ting for stemmefeminisering uten å redusere brukerens stemme til et enkelt pitchmål.

## Hvorfor dette trengs

UCSF peker på at stemme og kommunikasjon påvirkes av pitch, resonans, intonasjon, intensitet, stemmekvalitet, artikulasjon, taletempo og mer. Pitch er viktig, men ikke nok. Resonans og stemmekvalitet kan være like viktig for opplevd femininitet, og for trygg trening.

## Nåværende styrker

Prosjektet har allerede:

- Live audio capture
- Pitch detection
- Resonance proxy
- Spectrogram intelligence
- Exercise guidance
- Health Intelligence
- SmartCoach
- Per-device mikrofonkalibrering

Dette er et godt grunnlag. Neste steg er å gjøre feedback mer klinisk meningsfull.

## Foreslåtte måleområder

### 1. Resonance Brightness

Mål:

- F1/F2/F3 proxy
- spectral centroid
- ratio mellom lav/mid/high energi
- stabilitet over setning, ikke bare øyeblikk

UI:

- "Mer fremme"
- "Mørkere/tilbaketrukket"
- "Stabil fremre resonans"

Unngå:

- "mannlig resonans"
- absolutte krav uten personalisering

### 2. Vocal Weight Proxy

Vocal weight handler om hvor tung/pressed lyden oppleves. Mulige signaler:

- RMS/loudness relativt til brukerens baseline
- spectral tilt
- harmonic/noise proxy
- strain indicators

UI:

- Lett / balansert / presset
- "Prøv mindre volum og mykere onset"

### 3. Pitch Comfort Band

Ikke vis bare "mål Hz". Lag personlig komfortbånd:

- Basert på baseline, brukerens mål og trygg historikk.
- Separat for humming, vokaler, ord og setninger.
- Ikke push over komfortbånd ved strain.

UI:

- "Innenfor komfortsonen"
- "Litt over komfortsonen - sjekk at stemmen kjennes lett"

### 4. Intonation and Prosody

Mål:

- pitch movement over phrase
- monotoni vs variasjon
- oppadgående/nedadgående kontur
- stabilitet i korte setninger

Dette bør komme etter resonans/komfort, ikke som første feature.

### 5. Airflow and Onset

Mål:

- brå start vs myk onset
- airflow consistency proxy
- klipping/overload fra mic

UI:

- "Mykere start"
- "Jevnere luft"
- "Mindre press"

## Ny feedbackmodell

Erstatt tomme eller generiske live feedback-paneler med en rangert liste:

1. Trygghet: komfort/strain
2. Hovedmål for øvelsen
3. Ett konkret neste grep
4. Progresjon over siste 30 sekunder

Eksempel:

- Status: Trygg og stabil
- Fokus: Resonans
- Neste grep: Flytt lyden litt mer frem med rolig humming
- Trend: Mer stabil enn starten av økten

## Dataarkitektur

Ny modell:

- `VoiceFeatureFrame`
  - Timestamp
  - PitchHz
  - PitchConfidence
  - ResonanceBrightness
  - VocalWeightProxy
  - AirflowBalance
  - OnsetSharpness
  - StrainRisk
  - DeviceCalibrationId

- `ExerciseFeedbackFrame`
  - ExerciseId
  - TargetDimension
  - SafetyState
  - PrimaryCueKey
  - SecondaryCueKey
  - Confidence

## Prioritet

P0:

- Confidence-aware feedback: ikke vis sterke råd når mic/pitch confidence er lav. Status 2026-06-02: testet for realtime no-voice og breathing low-intensity.
- Resonance-first exercise feedback. Status 2026-06-02: eksisterende SmartCoach prioriterer resonans under terskel, og VoiceGoalProfile pitch-fokus kan ikke overstyre lav resonans.
- Vocal weight/strain proxy som safety signal. Status 2026-06-02: eksisterende Health Intelligence/strain-flyt og recovery-credit er koblet til trygg progresjon; mer avansert vocal-weight proxy gjenstår som framtidig målefeature.

P1:

- Intonation/prosody feedback.
- Opptak før/etter med målte endringer.
- Stabilitet over setninger.

P2:

- AI-basert selvrefleksjon/coach basert på øktnotater, ikke lydopptak som standard.
- Clinician review export.

## Acceptance Criteria

- Appen kan gi nyttig feedback selv når pitch ikke er hovedmålet.
- Humming-øvelser evalueres som humming, ikke som "snakke". Status 2026-06-02: `ResourceTextPolicyTests.HummingResources_DoNotUseSpeechModeInstructions` blokkerer tale-/speech-instruksjoner i `ResonanceHumming`-ressurser.
- Lav confidence gir "vi hører ikke nok signal" i stedet for negativ stemmefeedback. Status 2026-06-02: `FeedbackSignalPolicyTests` dekker realtime no-voice og breathing low-intensity feedback som signal-/kalibreringscue.
- Resonans, komfort og safety vises før pitch-jakt.

## Kilder

- UCSF beskriver flere relevante voice/communication-dimensjoner og at pitch alene ikke er nok: https://transcare.ucsf.edu/guidelines/vocal-health
- ASHA beskriver gender-affirming voice and communication som en kombinasjon av pitch, resonans, speech og nonverbal communication: https://www.asha.org/practice-portal/professional-issues/gender-affirming-voice-and-communication/
