# FEMVOICE 1 - Clinical Product Direction

## Formål

Dette dokumentet foreslår veien videre for FemVoice Studio som et hjelpeverktøy for trans kvinner og transfeminine brukere som vil feminisere stemmen på en trygg, personlig og klinisk ryddig måte.

Målet er ikke å gjøre appen til en erstatning for logoped/SLP eller laryngolog. Målet er å gjøre appen bedre som treningsstøtte: måle, forklare, minne om trygg bruk, vise progresjon og støtte selvvalgte stemmemål.

## Faglig grunnlag

Oppdatert søk 2026-06-01 peker på disse prinsippene:

- Stemmefeminisering handler ikke bare om pitch. UCSF beskriver pitch, resonans, intonasjon, intensitet, stemmekvalitet, artikulasjon, taletempo og ikke-verbal kommunikasjon som relevante dimensjoner.
- Pitch alene er utilstrekkelig for opplevd femininitet. UCSF skriver at pitchendring alene ikke bør være første eller eneste behandling.
- Resonans, spesielt mer oral/lys resonans og lett fonasjon, er sentralt. UCSF beskriver resonant voice therapy og flow phonation som relevante teknikker.
- ASHA legger vekt på at mål skal være selvbestemte og kan være feminine, androgyne, kjønnsnøytrale eller situasjonsbaserte.
- WPATH SOC8 plasserer voice and communication i en bred, koordinert helsemodell og peker på at feltet er tverrfaglig og i utvikling.
- Johns Hopkins beskriver både terapi, øvelser, feedback, opptak og kirurgi som mulige deler av care, men også at trening og integrering over tid er viktig.

## Produktretning

FemVoice bør utvikles som en trygg, personalisert treningspartner med fire hovedakser:

1. Resonans og stemmekvalitet først, pitch som sekundær støtte.
2. Klinisk sikker progresjon med belastningsgrenser, hvile og komfortscore.
3. Personlige mål og kontekst: brukerens ønskede stemme, ikke en binær fasit.
4. Bedre feedback-loop: lytte, prøve, måle, reflektere, justere.

## Ikke bygg dette som fasit

Appen bør ikke:

- Fortelle brukeren at stemmen er "mannlig" eller "feil".
- Straffe lav pitch hvis resonans, komfort og brukerens mål er gode.
- Presse mot faste Hz-mål som universell femininitetsstandard.
- Bruke "passing" som primær score.
- Gi medisinsk diagnose eller si at brukeren har stemmeskade.
- Oppmuntre til høy pitch med anstrengt fonasjon.

## Foreslåtte hovedinitiativer

### 1. Voice Goal Profile

Lag en onboarding/profil der brukeren kan velge hva hun faktisk ønsker:

- Feminint, mykt, lyst, naturlig, profesjonelt, androgyn, situasjonsbasert.
- Fokusområder: resonans, stabilitet, intonasjon, stemmevekt, komfort, volum.
- Hvilke situasjoner hun trener for: samtale, telefon, jobb, gaming/voice chat, offentlig sted.

Dette bør påvirke SmartCoach, øvelsesvalg og feedback-tekst.

### 2. Multi-Dimensional FemVoice Map

Bytt fra "en score" til et kart:

- Resonance brightness
- Vocal weight / perceived heaviness
- Pitch comfort
- Intonation variety
- Onset smoothness
- Airflow balance
- Vocal effort / strain risk
- Consistency over time

FemVoiceScore kan fortsatt finnes, men bør forklares som "treningssignal", ikke sannhet.

### 3. Clinically Safe Practice Engine

Utvid Health Intelligence med:

- Max voice load per day basert på historikk.
- Tidlige tegn på press: fallende stabilitet, økende shimmer/jitter proxy, lavere komfort, mer pitch drop.
- Recovery-mode som automatisk foreslår mykere øvelser.
- Tydelig "stopp og hvil" uten skam.

### 4. Ear Training og Self-Perception

Stemmefeminisering krever at brukeren lærer å høre hva hun gjør. Appen bør få:

- A/B-opptak før/etter.
- Resonans-eksempler med brukerens egne opptak.
- "Hva endret seg?" refleksjon etter økt.
- Favorittopptak som viser trygg fremgang.

### 5. Clinician Review Mode

For brukere som går til logoped/SLP:

- Eksporter øktdata som PDF/CSV.
- Marker øvelser, belastning, komfort og notater.
- Ikke inkluder sensitive identitetsdata uten aktivt samtykke.

## Prioritet

P0:

- Voice Goal Profile. Status 2026-06-02: teknisk modell/provider, Settings-UI og SmartCoach-hook er implementert; full førstegangs-onboarding gjenstår som senere produktarbeid.
- Sikker språkbruk i score/coach. Status 2026-06-02: RESX safety-copy policy og humming-copy policy er implementert.
- Resonans- og komfort-first feedback. Status 2026-06-02: low-signal feedback, recovery-credit og pitch-overstyringsvern i SmartCoach er testet.
- Manuell test av USB og jack mikrofon. Status: gjenstår som manuell mikrofon-QA.

P1:

- Multi-dimensional feedback map
- Ear training/opptaksbibliotek
- Bedre vocal load/recovery. Status 2026-06-02: recovery-practice teller positivt i ukefremdrift uten prestasjonspress; videre vocal-load UX gjenstår.

P2:

- Clinician export
- Situasjonsbasert stemmetrening
- Post-surgery mode for brukere som har hatt stemmekirurgi

## Kilder

- UCSF Gender Affirming Health Program: Transgender voice and communication - vocal health and considerations: https://transcare.ucsf.edu/guidelines/vocal-health
- ASHA Practice Portal: Gender Affirming Voice and Communication: https://www.asha.org/practice-portal/professional-issues/gender-affirming-voice-and-communication/
- WPATH Standards of Care Version 8: https://wpath.org/publications/soc8/
- Johns Hopkins Medicine: Transgender and Gender-Diverse Voice Care: https://www.hopkinsmedicine.org/health/expert-qa/transgender-and-gender-diverse-voice-care
