# FEMVOICE - Release Readiness Checklist

## Automatisert Status 2026-06-01

Automatisert verifikasjon er gronn etter siste endringer.

- Build: `dotnet build FemVoiceStudio.slnx --no-restore -p:BaseOutputPath=.\bin\CodexBuild\ --verbosity minimal` - gronn, 0 warnings, 0 errors.
- Test: `dotnet test .\FemVoiceStudio.slnx --no-build -p:BaseOutputPath=.\bin\CodexBuild\` - 115/115 app-tester og 312/312 testprosjekt-tester gront.
- `NotImplementedException` er fjernet fra aktive converters.
- `HydrationAdvisor` er validert med trend-, cooldown/no-spam- og safety-lock-tester.
- Appens DI-graf er validert med automatisert smoke-test mot midlertidig testdatabase.
- Refererte lokaliseringsnokler i kode valideres automatisk mot `Strings.resx`.

Manuell QA-oppdatering 2026-06-01:

- Exercise start/stopp fungerer.
- Sprakbytte fungerer.
- Theme-bytte fungerer.
- `HearOwnVoice` ble funnet som feil i exercise-audioflyt: live audio-monitor var opt-out i praksis. Fikset ved at audio playback nå er opt-in og `ExerciseWindow` leser Settings før live-audio starter.
- Pitch-grafen på hovedsiden brukte hardkodede OxyPlot-farger og er oppdatert til theme-farger.
- Mikrofon-kalibrering er lagt inn per device: Settings har wizard for stille rom + komfortabel stemme/humming, og lagret profil kan styre noise gate og voiced RMS-threshold. Kalibrerte terskler støtter også rene lav-output mikrofoner og avviser ugyldig voice/humming-fase som er for lik bakgrunnsstøy. Manuell test med fysisk mikrofon gjenstår.
- Mikrofonvalg bruker nå Windows standard opptaksenhet først, slik at både USB og jack/analog mic kan testes ved å sette riktig input i Windows før appen startes. USB prioriteres ikke lenger hardkodet over jack.
- Klinisk avgrensning/disclaimer er lagt inn i Settings, og unsafe voice-pressure copy policy er automatisert for RESX-verdier.
- Humming resource policy er lagt inn, og breathing/no-signal feedback bruker nå signal-/komfortspråk i stedet for "snakk høyere"/voice projection.
- Realtime no-voice og breathing low-intensity feedback har automatiserte tester som bekrefter signal-/kalibreringscue og ingen stemmepressformulering.
- Exercise timer har testbar fallback-state og automatiserte tester som bekrefter at timeren fortsetter selv uten live feedback/ViewModel-sekunder.
- Mikrofonkalibrering er stabil mot WaveIn device-index-endring, og recovery-practice teller i ukens sessions/minutter uten å påvirke performance averages.
- VoiceGoalProfile-teknisk modell/provider er lagt inn, og SmartCoach kan bruke brukerens `PrimaryFocus` når det ikke bryter helse-/resonansprioritet.
- Mikrofonkalibrering er nå adaptiv: flere gode kalibreringer forbedrer eksisterende profil gradvis, og UI-vinduet viser lange meldinger bedre.
- VoiceGoalProfile har nå Settings-UI med balansert standard, fokusvalg og målstil. Profilen lagres lokalt og er dekket av persistenstester.
- Chart-labels i `AnalysisWindow`, `PitchChartViewModel` og `ResonanceChartViewModel` er flyttet til RESX. Resonansstatus-copy bruker nå fremoverplassering/komfort-språk i stedet for bastante femininitetsfasiter.

## Arkitektur

☑ Alle builds gronne
☑ Ingen kjente runtime exceptions i testet start/stopp-, sprak- og theme-flyt
☑ Dependency Injection-graf validert automatisert
☑ Full WPF app-start validert manuelt
☑ Ingen kritiske `NotImplementedException` i aktiv kode

## Biofeedback

☐ Alle indikatorer oppdateres korrekt i manuell app-kjoring
☐ HoldArc fungerer i manuell app-kjoring
☐ Comfort Zone fungerer i manuell app-kjoring
☐ ShieldPanel fungerer i manuell app-kjoring
☐ Guidance fungerer i manuell app-kjoring
☐ SmartCoach fungerer i manuell app-kjoring

## Health Intelligence

☑ VocalHealthSupervisor validert med automatiserte tester
☑ HydrationAdvisor validert med automatiserte tester
☑ RecoveryPolicy validert med automatiserte tester
☑ Restrict/Lock validert med automatiserte tester

## Audio / mikrofon

☑ Per-device kalibreringsprofil støttes teknisk
☑ Kalibrert noise gate kan brukes av `AudioCaptureService`
☑ Kalibrert voiced RMS-threshold kan brukes av pitch-deteksjon
☑ UI-flyt for å måle stille rom + komfortabel stemme/humming er implementert
☐ Mikrofonkalibrering validert manuelt med fysisk USB/headset/innebygd mikrofon
☐ Mikrofonkalibrering validert manuelt med jack/analog mikrofon valgt som Windows standard input

## Lokalisering

☑ Alle sprakfiler oppdatert for siste endringer
☑ Ingen manglende RESX-nokler i automatiserte kontroller
☑ Refererte lokaliseringsnokler i kode finnes i neutral RESX
☑ Analyse-/pitch-/resonanschart labels er RESX-styrt
☑ Ingen eksplisitte Hz-verdier i kvalitative exercise/milestone resource-tekster
☑ Sprakbytte fungerer i manuell WPF-kjoring

## UX

☐ GuidancePanel validert manuelt
☐ Live Feedback validert manuelt
☐ Progression UI validert manuelt
☑ Dark Theme validert manuelt
☑ Light Theme validert manuelt

## Testing

☑ Unit tests gronne
☑ Integration tests gronne
☐ Manual QA gjennomfort
☐ Lang okt (>30 min) testet
☑ Recovery-scenarier dekket av automatiserte tester

## Klinisk kvalitet

☑ Guidance innhold gjennomgatt i dokumentasjon og RESX-basert content library
☑ SmartCoach sprak dekket av feedback consistency og localization-arbeid
☑ Helsemeldinger dekket av VocalHealth/Hydration feedback mapping
☑ Ingen klandrende formuleringer i hydration-rad
☑ Terapeutisk tone opprettholdt i automatisert gjennomgang av nye health/hydration meldinger
☑ Unsafe voice-pressure copy blokkeres i RESX-policytest
☑ Humming-ressurser blokkeres fra tale-/speech-instruksjoner i policytest
☑ Low-signal feedback valideres som mikrofon-/kalibreringscue i automatiserte tester
☑ Exercise timer fallback valideres uavhengig av live feedback
☑ Recovery-practice teller positivt i ukefremdrift uten prestasjonspress
☑ Device calibration stabil mot device-order-endring
☑ Mikrofonkalibrering kan forbedres over flere runder
☑ VoiceGoalProfile kan settes i Settings og påvirker SmartCoach når profil finnes og det er klinisk trygt

## Klar for lansering

Automatisert release-verifisering er gronn. Production Ready krever fortsatt manuell WPF QA, sprakbytte-test, theme-test og lang okt-test.
