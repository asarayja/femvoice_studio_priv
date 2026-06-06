Du er en klinisk stemmeterapeut og senior C#/WPF biofeedback-arkitekt med dyp erfaring i event-drevne sanntidssystemer for stemmefeminisering.

Systemkontekst

FemVoice Studio er et event-drevet sanntids biofeedback-system. Live UI oppdateres utelukkende via ExerciseLiveState (immutable snapshot med alle metrics normalisert 0–1), publisert fra ExerciseIntelligenceCoordinator.cs og konsumert i ExerciseDetailViewModel.cs → ExerciseWindow.xaml / LiveFeedbackPanel.xaml.

Profiler bygges via ExerciseProfileFactory.cs basert på metadata i ExerciseDefinition.cs / Exercise.cs. Signaler kommer fra AudioAnalysisEngine.cs, ResonanceProxyEngine.cs og FemVoiceScoreEngine.cs.

Relevante kodefiler er vedlagt: ExerciseLiveState.cs, ExerciseTargetProfile.cs, ExerciseDefinition.cs, Exercise.cs, ExerciseIntelligenceCoordinator.cs, AudioAnalysisEngine.cs, ResonanceProxyEngine.cs.

Viktige arkitekturdetaljer å respektere:

ExerciseLiveState eksponerer: PrimaryMetricScore, SecondaryMetricScore, StabilityScore, IsInComfortZone, IsHoldingCorrectly, HoldProgress, IsSafetyLocked, Quality (Poor/Improving/Good/Excellent)

ExerciseTargetProfile styrer hvilke signaler som evalueres via boolske flagg: UsesResonance, UsesPitch, UsesStability, UsesIntensity

ExerciseIntelligenceCoordinator vekter scoring: resonance 0.5, stability 0.3, pitch 0.2

ResonanceProxyEngine leverer normaliserte F1/F2/F3-deltaer, spacing-ratio og sentroid-avvik — disse driver ResonanceBar, StabilityMeter og SmoothnessFlow direkte

Exercise.ProfileType (ExerciseProfileType enum) er nøkkeldiskriminator for factory-mapping

Safety-lock utløses ved health < 70 ELLER comfort-zone-brudd; fryser HoldProgress

Oppgave

Produser en komplett biofeedback-mapping for alle 15 øvelser nedenfor. Mappingen skal kunne legges direkte inn i ExerciseTargetProfile.cs og sendes via ExerciseLiveState.cs — uten UI-hardkoding eller per-øvelse logikk i ExerciseIntelligenceCoordinator.

Tillatte verdier

FeedbackMode — velg én per øvelse:

PrecisionMotor — presis motorisk måloppnåelse med tydelig terskelreferanse

GuidedContour — kontinuerlig bevegelsesguiding langs en kontur

TransferPractice — overføring av erlærte mønstre til naturlig tale

HealthSupport — beskyttet modus med aktiv safety-lock-overvåking

IndicatorPackage — velg relevante fra:

ResonanceBar — normalisert resonansskår fra ResonanceProxyEngine

StabilityMeter — frame-to-frame stabilitetssporing

PitchDirection — normalisert pitch-retning fra ComfortZoneController

SmoothnessFlow — kontur-jevnhet over tid

HoldArc — HoldProgress (0–1) mot RequiredHoldSeconds

VariabilityContour — pitch/resonans-variabilitet over tid

HealthShield — IsSafetyLocked / VoiceHealthMonitor-status

AirflowIndicator — intensitets- og luftstrømskoordinering

ThresholdStrategy — velg én:

UseProfileDefaults — bruker eksisterende factory-defaults fra ExerciseTargetProfile

OverrideWithStricterTargets — overrider med strengere kliniske grenser

NoHardThresholds — ingen harde terskler, kun visuell guidning (IntonationExercise-mønster)

Absolutte arkitekturkrav

❌ Ingen Hz-verdier eller råtall eksponert mot bruker

❌ Ingen UI-logikk hardkodet per øvelse

❌ Ingen direkte påvirkning av AudioAnalysisEngine.cs

✔ All konfigurasjon datastyrt via ExerciseTargetProfile.cs og ExerciseLiveState.cs

✔ ExerciseIntelligenceCoordinator forblir UI-agnostisk — publiserer kun ExerciseLiveState

✔ All UI-tekst bruker lokalisasjonsnøkler (RESX/LocalizationService) — ingen hardkodet display-tekst i modeller

✔ Presentasjonslogikk tilhører ExerciseDetailViewModel, ikke koordinatoren

✔ If any IndicatorPackage is empty or clinically inconsistent, revise it — no exercise may have zero active indicators unless FeedbackMode is TransferPractice

Øvelsesliste (map nøyaktig disse 15 — ikke gi nytt navn, slå sammen eller utelat)

Grunnleggende humming

Vokallyder – Fremre resonans

Stigende toner (Glide Up)

Synkende toner (Glide Down)

Konsistens-trening

S-setninger

Spørsmålsmelodi

Utsagnsmelodi

Fraselesing

Samtale-simulasjon

Resonans-skift: Fremre plassering

Starter-pitch memorisering

Pitch slide i fraser

Straw phonation (halmsfonasjon)

Emosjonell intonasjon

Output-format

Lever resultatet som en Markdown-tabell med følgende kolonner:

| Øvelse | FeedbackMode | IndicatorPackage | ThresholdStrategy | ClinicalPurpose |

IndicatorPackage: kommaseparert liste over valgte indikatorer

ClinicalPurpose: 1–2 presise kliniske setninger per øvelse

Ingen forklarende tekst utenfor tabellen

Etter tabellen

Lever en oppsummering med tre punkter:

FeedbackMode-fordeling — antall øvelser per mode

Hyppigste indikatorer — hvilke IndicatorPackage-elementer brukes oftest på tvers av øvelsene

Klinisk progresjonsforslag — ett konkret forslag til øvelsessekvens basert på mappingen, fra grunnleggende til avansert