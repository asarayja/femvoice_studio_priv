# FEMVOICE - Implementation Verification Matrix

## Status 2026-06-01

Matrisen er oppdatert etter kodegjennomgang, build og automatiserte tester.

Verifisering brukt:

- `dotnet build FemVoiceStudio.slnx --no-restore -p:BaseOutputPath=.\bin\CodexBuild\ --verbosity minimal` - gronn, 0 warnings, 0 errors.
- `dotnet test .\FemVoiceStudio.slnx --no-build -p:BaseOutputPath=.\bin\CodexBuild\` - 115/115 app-tester og 312/312 testprosjekt-tester gront.
- `rg -n "NotImplementedException|throw new NotImplemented" FemVoiceStudio --glob '!bin/**' --glob '!obj/**'` - ingen treff.
- `ReleaseReadinessSmokeTests` validerer appens DI-graf og runtime ViewModel-opplosning mot midlertidig testdatabase.
- `ResourceTextPolicyTests` validerer at refererte lokaliseringsnokler i kode finnes i `Strings.resx`.
- Manuell QA bekrefter appstart, exercise start/stopp, sprakbytte og theme-bytte. `HearOwnVoice` og hovedsidens pitch-graf-theme ble fikset etter QA-funn.
- Per-device mikrofonkalibrering er implementert: Settings-wizard måler stille rom + komfortabel stemme/humming og lagrer profil som brukes av noise gate og voiced RMS-threshold. Kalibrering støtter rene lav-output mikrofoner og avviser ugyldig voice/humming-fase som er for lik bakgrunnsstøy. Manuell test med fysisk mikrofon gjenstar.
- Mikrofonvalg bruker Windows standard opptaksenhet først, slik at USB og jack/analog mic kan valideres separat ved å bytte standard input i Windows.
- Settings har klinisk avgrensning/disclaimer, og RESX-policytest blokkerer unsafe voice-pressure copy.
- Humming-ressurser har egen policytest mot tale-/speech-instruksjoner, og low-signal feedback peker på mikrofon/signal/komfort i stedet for mer stemmekraft.
- Low-signal feedback har egen testdekning for realtime no-voice og breathing low-intensity.
- Exercise timer fallback er trukket ut til `ExerciseSessionTimerState` og testet uavhengig av live feedback/ViewModel-sekunder.
- Recovery-practice teller i sessions/minutter uten å påvirke performance averages; mikrofonkalibrering bruker normalisert device-navn.
- VoiceGoalProfile-modell/provider er implementert, og SmartCoach bruker `PrimaryFocus` når helse-/resonansprioritet tillater det.
- Mikrofonkalibrering er adaptiv over flere runder og kalibreringsvinduet gir mer plass til lange status-/feilmeldinger.
- VoiceGoalProfile kan settes fra Settings, lagres lokalt og har RESX-dekkede brukertekster.
- Analyse-/pitch-/resonanschart labels er flyttet til RESX, og gamle bastante resonansstatus-tekster er erstattet med tryggere komfort-/plasseringstekst.

Statuskolonner:

| Status | Betydning |
| --- | --- |
| Dokumentert | Arkitektur og ansvar definert |
| Implementert | Klassen/komponenten finnes og kompilerer |
| Integrert | Koblet til appflyt, DI, ViewModel eller feedback-pipeline |
| Testet | Dekket av automatiserte tester eller build-verifisering |
| Produksjonsklar | Automatisk verifisert; manuell WPF QA kan fortsatt gjensta |

## 1. Core Biofeedback Engine

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| AudioAnalysisEngine | ☑ | ☑ | ☑ | ☑ | ☑ |
| ResonanceProxyEngine | ☑ | ☑ | ☑ | ☑ | ☑ |
| FemVoiceScoreEngine | ☑ | ☑ | ☑ | ☑ | ☑ |
| ExerciseIntelligenceCoordinator | ☑ | ☑ | ☑ | ☑ | ☑ |
| ExerciseLiveState | ☑ | ☑ | ☑ | ☑ | ☑ |

## 2. Exercise UX Layer

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| ExerciseWindow | ☑ | ☑ | ☑ | ☑ | ☐ Manuell WPF QA gjenstar |
| ExerciseDetailViewModel | ☑ | ☑ | ☑ | ☑ | ☑ |
| LiveFeedbackPanel | ☑ | ☑ | ☑ | ☑ | ☐ Manuell WPF QA gjenstar |
| HoldArc | ☑ | ☑ | ☑ | ☑ | ☐ Manuell WPF QA gjenstar |
| ShieldPanel | ☑ | ☑ | ☑ | ☑ | ☐ Manuell WPF QA gjenstar |
| GuidancePanel | ☑ | ☑ | ☑ | ☑ | ☐ Manuell WPF QA gjenstar |

## 2b. Audio Input Calibration

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| MicrophoneCalibrationProfile | ☑ | ☑ | ☑ | ☑ | ☑ |
| MicrophoneCalibrationService | ☑ | ☑ | ☑ | ☑ | ☑ |
| Calibrated Noise Gate | ☑ | ☑ | ☑ | ☑ | ☑ |
| Calibrated Voiced Threshold | ☑ | ☑ | ☑ | ☑ | ☑ |
| Calibration UI Wizard | ☑ | ☑ | ☑ | ☑ Build/test | ☐ Manuell mikrofon-QA gjenstar |

## 3. Guidance System

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| GuidanceItems | ☑ | ☑ | ☑ | ☑ | ☑ |
| Guidance Localization | ☑ | ☑ | ☑ | ☑ | ☑ |
| ExerciseProfileFactory | ☑ | ☑ | ☑ | ☑ | ☑ |
| ExerciseTargetProfile Guidance Keys | ☑ | ☑ | ☑ | ☑ | ☑ |
| Guidance Content Library | ☑ | ☑ | ☑ | ☑ | ☑ |

## 4. Progression System

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| SessionAnalyticsStore | ☑ | ☑ | ☑ | ☑ | ☑ |
| ProgressionOrchestrator | ☑ | ☑ | ☑ | ☑ | ☑ |
| Mastery System | ☑ | ☑ | ☑ | ☑ | ☑ |
| Progress Visualization | ☑ | ☑ | ☑ | ☑ | ☐ Manuell WPF QA gjenstar |

## 5. Health Intelligence Layer

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| VocalHealthSupervisor | ☑ | ☑ | ☑ | ☑ | ☑ |
| Trend Engine | ☑ | ☑ | ☑ | ☑ | ☑ |
| StrainDetectionPolicy | ☑ | ☑ | ☑ | ☑ | ☑ |
| FatigueDetectionPolicy | ☑ | ☑ | ☑ | ☑ | ☑ |
| PausePolicy | ☑ | ☑ | ☑ | ☑ | ☑ |
| RecoveryPolicy | ☑ | ☑ | ☑ | ☑ | ☑ |
| HydrationAdvisor | ☑ | ☑ | ☑ | ☑ | ☑ |

## 6. Coaching Layer

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| SmartCoach | ☑ | ☑ | ☑ | ☑ | ☑ |
| InlineCoachMessage | ☑ | ☑ | ☑ | ☑ | ☑ |
| FeedbackConsistencyGuard | ☑ | ☑ | ☑ | ☑ | ☑ |

## 7. Localization

| Modul | Dokumentert | Implementert | Integrert | Testet | Produksjonsklar |
| --- | --- | --- | --- | --- | --- |
| RESX Infrastructure | ☑ | ☑ | ☑ | ☑ | ☑ |
| LocConverter | ☑ | ☑ | ☑ | ☑ | ☑ |
| Guidance Localization | ☑ | ☑ | ☑ | ☑ | ☑ |
| Quality Labels | ☑ | ☑ | ☑ | ☑ | ☑ |
| Mastery Labels | ☑ | ☑ | ☑ | ☑ | ☑ |
| Health Labels | ☑ | ☑ | ☑ | ☑ | ☑ |
| Clinical Disclaimer | ☑ | ☑ | ☑ | ☑ | ☑ |
| Voice Pressure Copy Policy | ☑ | ☑ | ☑ | ☑ | ☑ |
| Humming Mode Copy Policy | ☑ | ☑ | ☑ | ☑ | ☑ |
| Low-Signal Feedback Policy | ☑ | ☑ | ☑ | ☑ | ☑ |
| Exercise Timer Fallback | ☑ | ☑ | ☑ | ☑ | ☑ |
| Recovery Practice Credit | ☑ | ☑ | ☑ | ☑ | ☑ |
| Device Calibration Order Stability | ☑ | ☑ | ☑ | ☑ | ☑ |
| Adaptive Microphone Calibration | ☑ | ☑ | ☑ | ☑ | ☐ Manuell mikrofon-QA gjenstar |
| VoiceGoalProfile Settings + SmartCoach Hook | ☑ | ☑ | ☑ | ☑ | ☑ |

## 8. Hardcoded Text Audit

☑ Ingen hardkodede eksplisitte Hz-verdier i kvalitative exercise/milestone resource-tekster.
☑ Ingen `NotImplementedException` i aktiv kildekode.
☑ Aktive ViewModels/Services er ryddet etter `FEMVOICE 3`.
☑ Alle nye resource keys er lagt i RESX-filer.
☑ Refererte lokaliseringsnokler i kode finnes i neutral RESX.
☑ VoiceGoalProfile Settings-tekster finnes i RESX-filer.
☑ Chart-labels i aktive analyse-/resonans-/pitchflater finnes i RESX-filer.
☐ Full manuell visuell tekstgjennomgang i kjørende WPF-app gjenstar.

## 9. Release Readiness

## Funksjonelt

☐ Alle ovelser kan startes - krever manuell WPF QA
☐ Alle ovelser kan stoppes - krever manuell WPF QA
☐ Timer fungerer - krever manuell WPF QA
☐ Live Feedback fungerer - krever manuell WPF QA
☑ Guidance fungerer arkitektonisk og er automatisert verifisert
☑ Progression fungerer i automatiserte tester
☑ Health Intelligence fungerer i automatiserte tester

## Kvalitet

☑ Alle automatiserte tester gronne
☑ Build gronn uten warnings/errors
☑ Ingen kritiske `NotImplementedException`
☑ DI-graf og runtime ViewModel-opplosning validert automatisert
☑ Ingen kjente runtime exceptions i testet appstart/start/stopp/sprak/theme-flyt
☐ Ingen binding-feil i Output Window - krever manuell WPF app-kjoring
☑ Ingen DI-feil ved full WPF app-start i manuell QA

## Produksjonsklar Status

| Niva | Status |
| --- | --- |
| Alpha | ☑ Automatisert verifisert |
| Beta | ☑ Automatisert verifisert |
| Release Candidate | ☐ Avventer manuell WPF QA |
| Production Ready | ☐ Avventer manuell WPF QA, sprakbytte-test, theme-test og lang okt-test |

## Sluttmal

Automatisert teknisk status er gronn. Det som gjenstar for full produksjonsklar v1.0 er manuell WPF-verifisering av appstart, exercise lifecycle, binding output, theme/sprakbytte og lang okt.
