Du er en senior C#/WPF-arkitekt med dyp erfaring i MVVM, event-drevet arkitektur og kliniske biofeedback-systemer.

Jeg jobber med **FemVoice Studio** – et sanntids biofeedback-system for stemmefeminisering. De vedlagte filene (`ExerciseWindow.xaml.cs`, `ExerciseLiveState.cs`, `ExerciseIntelligenceCoordinator.cs`, `ExerciseWindow.txt`) representerer eksisterende kodebase som **må respekteres fullt ut**.

Status 2026-06-01:

Dette dokumentet er en historisk refactor-prompt, ikke et aktivt åpent arbeidsdokument.
`ExerciseDetailViewModel`, guidance-panelet, Hz-fri pitchstatus, adaptive indikatorer og
subjektiv post-session progresjon er nå implementert i kodebasen. Bruk
`FemVoice_Architecture_Documentation.md` og `FemVoice_Exercise_Biofeedback_Roadmap.md`
som gjeldende status.

## Eksisterende Arkitektur

**Eksisterende filer og nøkkelatferd:**
- `ExerciseWindow.xaml.cs` – abonnerer på `ExerciseIntelligenceCoordinator.ExerciseUpdated`; mapper verdier direkte til navngitte UI-elementer (`ResonanceBar`, `StabilityBar`, `ShieldPanel`, `HoldPanel`, `CoachHintPanel`). Bruker **ikke** WPF DataContext binding for live feedback (unngår konflikt med eksisterende `ItemsControl`-bindinger) – i stedet dynamisk type-resolusjon med `PropertyChanged`-events for graceful degradation
- `ExerciseLiveState.cs` – immutable snapshot; nøkkelfelter: `PrimaryMetricScore`, `SecondaryMetricScore`, `StabilityScore`, `IsInComfortZone`, `IsHoldingCorrectly`, `HoldProgress`, `IsSafetyLocked`, `Quality`, `SessionElapsedSeconds`
- `ExerciseIntelligenceCoordinator.cs` – single source of truth; publiserer `ExerciseUpdated` (100ms rate-limit) og `InlineCoachUpdated` (5s throttle); dual-lock safety-system
- `ExerciseWindow.xaml` – dual-view layout (ListView/DetailView); `LiveFeedbackPanel` med `ResonanceBar`, `StabilityBar`, arc-basert `HoldProgress`, `CoachHintPanel` med Storyboard fade-animasjoner; `TargetPitchPanel` med Hz-visning (skal fjernes); alle tekster via `{loc:Loc}`-binding

**Historisk kontekst fra gammel prompt:**
- `ViewModels/ExerciseDetailViewModel.cs`

**Rør ikke:**
- `ExerciseIntelligenceCoordinator.cs`, `AudioAnalysisEngine.cs`, `ResonanceProxyEngine.cs`, `FemVoiceScoreEngine.cs`, `ExerciseListViewModel.cs`

## Rammeverk og mønstre

- **Ingen DI-rammeverk** – manuell `INotifyPropertyChanged` og `RelayCommand`
- Eksisterende konverter-ressurser: `BoolToVisibility`, `SeverityToBrush`, `ProgressToPercent`
- Eksisterende stiler: `MetricBar` (`ProgressBar` custom `ControlTemplate`), `PrimaryButton`, `SecondaryButton`
- All UI-oppdatering marshallet til dispatcher-tråd
- Produksjonskvalitet – ingen tester nødvendig

## Oppgave

### 1. Opprett `ViewModels/ExerciseDetailViewModel.cs`
- Implementer `INotifyPropertyChanged` manuelt
- Subscribe til `ExerciseIntelligenceCoordinator.ExerciseUpdated`; marshal alle oppdateringer til UI-tråd via `Application.Current.Dispatcher`
- Eksponer følgende properties: `ExerciseLiveState CurrentLiveState`, `ActiveIndicatorPackage`, `FeedbackMode`, `ThresholdStrategy`, `ObservableCollection<IndicatorViewModel>`, `SessionElapsedSeconds`, `CoachHint`, `Severity`
- Implement `RelayCommand` for Start, Stop, Pause og `DismissCoachMessageCommand`
- Erstatte **all** direkte UI-mapping fra code-behind (inkl. `PrimaryMetricScore → ResonanceBar`, `StabilityScore → StabilityBar`, `IsSafetyLocked/IsInComfortZone → ShieldPanel`, `HoldProgress → arc`, `CoachMessage/Severity → CoachHintPanel`)

### 2. Refaktorer `ExerciseWindow.xaml.cs`
- Fjern direkte `ExerciseIntelligenceCoordinator`-binding
- Injiser `ExerciseDetailViewModel`; sett opp `PropertyChanged`-lytting (behold eksisterende mønster uten DataContext-konflikt)
- Bevar `OnStartClick()` / `OnStopClick()` lifecycle-logikk, men delegér til ViewModel-commands
- Ingen biofeedback-logikk skal gjenstå etter refaktor

### 3. Oppdater `ExerciseWindow.xaml`
- Fjern `TargetPitchPanel` (Hz-visning)
- Legg til `ExerciseGuidancePanel` med `ItemsControl` + `DataTemplates` og bindings til: `ClinicalPurpose`, `PhysicalFocus`, `CommonMistakes`, `ActiveIndicators`, `SafetyInfo`
- Bevar eksisterende Storyboard-animasjoner for `CoachHintPanel`; bevar arc-geometri for `HoldProgress` – konverter til data-binding
- Ingen `if/else` per øvelse i XAML; all tekst via `{loc:Loc}`

### 4. Oppdater `ExerciseTargetProfile.cs`
Legg til: `ClinicalPurposeKey`, `PhysicalFocusKey`, `CommonMistakesKey`, `IReadOnlyList<IndicatorType> ActiveIndicators`

### 5. Oppdater `ExerciseDefinition.cs`
Fjern Hz-tekst; referer til guidance-keys

## Arkitekturregler (ufravikelige)

- ❌ Ingen Hz eller rå numeriske verdier eksponert i UI
- ❌ Ingen per-øvelse UI-logikk noe sted
- ❌ Ingen biofeedback-kode i XAML code-behind etter refaktor
- ✅ `ExerciseIntelligenceCoordinator` forblir fullstendig UI-agnostisk
- ✅ `ExerciseDetailViewModel` er eneste bindeledd mellom coordinator og UI
- ✅ All tekst routes via `LocalizationService`

## Forventet output

1. **Fil-for-fil endringsplan** – hvilke filer opprettes, endres, og hva konkret fjernes/legges til i hver
2. **Komplett implementasjon av `ExerciseDetailViewModel.cs`** – alle properties, commands, event-subscription, UI-thread marshalling og `RelayCommand`-implementasjon på plass, produksjonsklar
3. **Oppdatert `ExerciseWindow.xaml`** – komplett struktur inkludert `ExerciseGuidancePanel`-layout med korrekte bindings, `DataTemplates`, og bevarte animasjoner
4. **Oppdaterte `ExerciseWindow.xaml.cs`-signaturer** – vis hva som fjernes og hva som erstattes
5. **Kort forklaring** på hvordan denne arkitekturen forbedrer klinisk læringseffekt for brukeren
