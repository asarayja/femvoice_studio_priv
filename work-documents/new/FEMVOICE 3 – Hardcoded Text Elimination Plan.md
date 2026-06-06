# FEMVOICE – Hardcoded Text Elimination Plan.md

## Status 2026-06-01

**Ferdig for aktiv appflate.**

Første reelle oppryddingsrunde er gjennomført for aktive, synlige vinduer:

* Exercise summary
* Exercise guide / exercise detail fallback-tekster
* Live feedback panel
* Analyzer
* Resonance window
* Main window status-/feilmeldinger
* Progression dashboard
* Analysis window
* Day details
* SmartCoach detail parameterlabels
* First-time setup

Alle nye nøkler er lagt inn i alle RESX-filer. Norsk har norsk tekst, engelsk har engelsk tekst, og øvrige språk har engelsk fallback.

Andre oppryddingsrunde er gjennomført for aktive service-/coach-meldinger:

* `CoachMessageFormatter`
* `CoachMessageGenerator`
* `DirectionAnalyzer`
* `AdaptiveComfortZoneService`
* `AdaptiveDifficultyService`
* `VoiceHealthMonitor`
* `VoiceHealthService`
* `VoiceProfileExtensions`
* `TrainingFrequencyService`

`VoiceFeminizationExerciseService` er vurdert som legacy per 2026-06-01. Den er ikke registrert i DI og ser ikke ut til å være i aktiv appflyt. Den skal ikke blokkere dokumentet med mindre den tas i bruk igjen.

Tidligere gjenstående brukerrettede ViewModel-strenger i kalender-/dagdetaljer, analysevisning, `SmartCoachViewModel`, `ExerciseListViewModel` og `MainViewModel` er flyttet til RESX eller vurdert som tekniske/interne.

Tredje oppryddingsrunde er gjennomført for aktive ViewModels:

* `MainViewModel`
* `SmartCoachViewModel`
* `CalendarViewModel`
* `CalendarDay`
* `DayDetailsViewModel`
* `AnalysisPageViewModel`
* `ExerciseListViewModel`
* `ProgressionDashboardViewModel`

Gjenværende ViewModel-treff fra søk er per 2026-06-01 vurdert som ressursnøkler, tekniske formatstrenger (`Hz`, `%`, datoformat), interne kategori-/enumverdier, brush keys, glyphs eller debug/logging. Kategorifilteret i `ExerciseListViewModel` viser nå lokaliserte kategorinavn, men matcher fortsatt eksisterende interne kategoriverdier.

Fjerde og avsluttende oppryddingsrunde er gjennomført for siste brede søk i aktive service-/UI-flater:

* `ExerciseIntelligenceCoordinator`
* `SmartCoachEngine`
* `ProgressionService`
* `ComplexityEngine`
* `ProgressionRateCalculator`
* `TrendAlertService`
* `ExerciseSummaryViewModel`
* `ProgressionDashboard`
* `StatisticsWindow`
* `SettingsWindow`
* `LevelClassificationSystem`

Femte kontrollrunde 2026-06-02 ryddet de siste brukerrettede chart-labelene i:

* `AnalysisWindow`
* `PitchChartViewModel`
* `ResonanceChartViewModel`

Resonansstatus-copy i chart-viewmodel er samtidig justert bort fra bastante "optimal feminine resonance"-formuleringer til tryggere fremoverplassering/komfort-språk.

Alle nye nøkler er lagt inn i alle RESX-filer. Norsk har norsk tekst, engelsk har engelsk tekst, og øvrige språk har engelsk fallback.

Rene ikon-koder, tekniske måleenheter (`Hz`, `F1/F2/F3`), språknavn i language selectors, interne enum-/statusverdier og debug/logging er ikke regnet som feil i denne runden.

`ExerciseTextService` inneholder fortsatt et stort norsk øvelsestekstkorpus. Dette er vurdert som treningsinnhold/seed-data, ikke som generell UI-tekst, og bør tas som et eget innholdslokaliseringsarbeid hvis øvelsestekstene skal være fullstendig flerspråklige. `VoiceFeminizationExerciseService` er fortsatt vurdert som legacy/inaktiv per 2026-06-01 og blokkerer ikke dette dokumentet.

Verifisering 2026-06-01:

* `dotnet build .\FemVoiceStudio.slnx -p:BaseOutputPath=.\bin\CodexBuild\` - grønn
* `dotnet test .\FemVoiceStudio.slnx --no-build -p:BaseOutputPath=.\bin\CodexBuild\` - 115/115 app-tester og 312/312 testprosjekt-tester grønt

## Formål

Sikre at hele FemVoice er fullstendig lokaliserbar.

---

## Regler

Ingen UI-tekst skal eksistere uten RESX-nøkkel.

---

## Områder som skal verifiseres

### ExerciseWindow

* Labels
* Hints
* Empty states
* Tooltips

### Live Feedback

* Shield states
* Quality labels
* Mastery labels

### Guidance

* Alle seksjoner
* Alle overskrifter
* Alle brødtekster

### SmartCoach

* Meldinger
* Helsevarsler
* Pauseforslag

---

## Kontrolliste

☑ Synlige hovedvinduer ryddet for hardkodede norske UI-labels

☑ Aktive ViewModel-tekster flyttet til RESX eller vurdert som tekniske/interne

☑ Aktive service-/coach-meldinger flyttet til RESX

☑ Alle språkfiler oppdatert for nøklene som ble flyttet

☑ Fallback fungerer for nye nøkler

☑ Siste brede gjennomgang av aktive XAML/Services/ViewModels fullført

---

## Neste del

Gå videre til `FEMVOICE 4`.

## Ferdig når

Global søk etter:

"Text="

og

"string"

ikke avdekker brukerrettede tekster uten RESX-støtte.
