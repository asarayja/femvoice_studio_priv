# FEMVOICE 2 - Clinical Safety and Language Guardrails

## Formål

FemVoice Studio må være trygg for en trans jente/trans kvinne som kan være sårbar for dysfori, perfeksjonisme og stemmepress. Appen skal støtte feminisering uten å gjøre stemmen til en feil som må "fikses".

## Kliniske prinsipper

1. Brukerens mål styrer.
2. Komfort og stemmehelse prioriteres over høy pitch.
3. Feedback skal være konkret, mild og handlingsrettet.
4. Appen må skille mellom treningssignal og medisinsk vurdering.
5. Appen må oppfordre til fagperson ved smerte, vedvarende heshet, stemmetap eller stor belastning.

## Språkregler

Ikke bruk:

- "mannlig stemme"
- "feil"
- "mislykket"
- "ikke feminin nok"
- "du må høyere"
- "passing"

Bruk heller:

- "Målet ditt er ikke helt stabilt ennå."
- "Prøv litt lettere lyd."
- "Resonansen kan flyttes litt mer frem uten å presse."
- "Dette var en tryggere variant."
- "Ta en pause hvis stemmen kjennes sliten."

## Safety States

### Green

Brukeren har stabil lyd, lav strain-risk, normal øktlengde og ingen negative komfortsignaler.

Appen kan:

- Gi vanlig progresjon.
- Foreslå neste øvelse.
- Vise positiv, konkret feedback.

### Yellow

Tegn på økende belastning:

- Pitch faller brått etter flere minutter.
- Stabilitet synker.
- Voice/humming blir mer presset.
- Brukeren rapporterer ubehag.

Appen bør:

- Senke vanskelighetsgrad.
- Foreslå humming, SOVT/straw-lignende øvelse eller pause.
- Skru coach-språk fra målpress til komfort.

### Red

Tegn som ikke skal pushes gjennom:

- Smerte
- Vedvarende heshet
- Stemmetap
- Svimmelhet/pustevansker
- Bruker rapporterer sterk dysfori eller stress

Appen bør:

- Stoppe øktforslag.
- Anbefale hvile.
- Foreslå fagperson ved vedvarende symptomer.
- Ikke gi "prøv hardere"-feedback.

## Featureforslag

### 1. Safety Copy Policy Test

Lag automatisert test som søker i RESX og coach-meldinger etter uønskede ord.

Eksempel blokkerte uttrykk:

- `mannlig stemme`
- `male voice`
- `not feminine enough`
- `failed`
- `push harder`

Status 2026-06-02:

- Implementert for brukerrettede RESX-verdier via `ResourceTextPolicyTests.UserFacingResources_DoNotUseUnsafeVoicePressureCopy`.
- Blokkerer blant annet "mannlig stemme", "male voice", "ikke feminin nok", "not feminine enough", "passing", "push harder", "snakk høyere", "speak louder", "du må høyere" og "you must go higher".
- Eksisterende "speak louder"/"snakk høyere"-feedback er endret til tryggere signal-/mikrofonkalibreringsspråk.

### 2. User-Reported Comfort Check

Etter økt:

- Stemmen kjennes: normal / litt sliten / hes / vond
- Øvelsen føltes: lett / passe / krevende / for mye
- Dysfori/stress: bedre / likt / verre

Dette bør påvirke SmartCoach og neste økt.

### 3. Recovery-First Coach

Hvis Health Intelligence går yellow/red:

- Coach skal foreslå tryggere alternativer.
- Progression skal ikke straffe pause.
- Weekly goals bør telle "recovery practice" som godkjent arbeid.

### 4. Clinical Disclaimer in Settings/About

Kort og tydelig:

FemVoice er et trenings- og feedbackverktøy, ikke medisinsk diagnose eller behandling. Ved smerte, vedvarende heshet, stemmetap eller bekymring bør brukeren kontakte kvalifisert stemmefagperson eller helsepersonell.

Status 2026-06-02:

- Implementert i Settings med RESX-nøklene `Settings_ClinicalDisclaimerTitle` og `Settings_ClinicalDisclaimerBody`.

## Acceptance Criteria

- Ingen coach-melding skammer brukeren.
- Pitch beskrives som ett signal, ikke som fasit.
- Recovery teller positivt i progresjon.
- Safety red kan stoppe øktforslag.
- Brukeren kan rapportere komfort etter hver økt.

## Kilder

- UCSF beskriver trygg, effektiv og pasientspesifikk stemmebehandling, og at pitch alene ikke bør være eneste fokus: https://transcare.ucsf.edu/guidelines/vocal-health
- ASHA beskriver selvbestemte stemmemål og bredere gender-affirming communication, ikke bare binær femininitet: https://www.asha.org/practice-portal/professional-issues/gender-affirming-voice-and-communication/
- Johns Hopkins beskriver at behandling kan inkludere terapi, feedback, opptak og øvelser, og at samlet vocal health inngår: https://www.hopkinsmedicine.org/health/expert-qa/transgender-and-gender-diverse-voice-care
