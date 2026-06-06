Du er en senior C#/WPF-arkitekt med dyp erfaring i MVVM, event-drevet arkitektur, kliniske biofeedback-systemer og profesjonell UX i sanntidsapper.

Jeg jobber med **FemVoice Studio** – et sanntids biofeedback-system for stemmefeminisering bygget på **.NET 10 + WPF**. De vedlagte filene er autoritative – all ny kode må respektere dem fullt ut.

## Teknisk Miljø

- **.NET 10 + WPF** – ingen legacy .NET Framework-begrensninger
- **Ingen MVVM-rammeverk** – ren `INotifyPropertyChanged` + egen `RelayCommand` (se `RelayCommand.cs`)
- UI-thread marshalling via `Application.Current.Dispatcher.BeginInvoke` – moderne async/dispatcher-APIer er tilgjengelige
- Ingen bakoverkompatibilitet nødvendig

## Autoritative Filer (vedlagt)

- `ExerciseWindow.txt` → behandles som `ExerciseWindow.xaml` – lever output i korrekt XAML-syntaks
- `ExerciseWindow.xaml.cs`
- `ExerciseLiveState.cs`
- `ExerciseIntelligenceCoordinator.cs`
- `ExerciseTargetProfile.cs`
- `ExerciseDefinition.cs`
- `ExerciseDetailViewModel.cs`
- `RelayCommand.cs`
- `LocalizationService.cs`
- `LightTheme.txt` / `DarkTheme.txt` → behandles som XAML resource dictionaries

## Arkitekturregler Som Ikke Kan Brytes

- Live feedback kommer **utelukkende** via `ExerciseLiveState` (immutable snapshot, alle metrics normalisert 0–1)
- `ExerciseIntelligenceCoordinator` er single source of truth – **skal ikke røres**
- `ExerciseDetailViewModel` eier all biofeedback-logikk og coordinator-subscription; code-behind håndterer kun UI-mirroring, animasjoner og timer-lifecycle
- Ingen per-øvelse UI-logikk i XAML eller code-behind
- Ingen Hz eller rå tall eksponert i UI
- All tekst via `LocalizationService[key]` – ingen hardkodede strenger i XAML eller code-behind
- Alle farger via `DynamicResource` – ingen hardkodede farger i XAML
- Full Light/Dark theme-støtte via eksisterende resource dictionaries (brush-nøkler som `BackgroundSecondaryBrush`, `AccentPrimaryBrush`, `SuccessBrush`, `TextPrimaryBrush` etc.)

## Filer Som Ikke Skal Røres

`ExerciseIntelligenceCoordinator.cs`, audio engines, scoring-algoritmer, safety logic, `ExerciseListViewModel` (kun navigasjon), `RelayCommand.cs`, `LocalizationService.cs`

---

## Oppgave 1 – Komplett `ExerciseDetailViewModel.cs`

Implementer følgende basert på eksisterende arkitektur i den vedlagte filen:

**Subscriptions og threading:**
- Subscribe til `ExerciseIntelligenceCoordinator.ExerciseUpdated` og `InlineCoachUpdated`
- All state marshallet til UI-thread via `Application.Current.Dispatcher.BeginInvoke`

**Observable properties (INotifyPropertyChanged):**
- Normaliserte metrics fra `ExerciseLiveState`: `PrimaryMetricScore`, `StabilityScore`, `HoldProgress` (alle 0–1)
- Computed display properties: `ShieldState` (`ShieldDisplayState` enum: Safe/Warning/Locked), visibility flags (`ShowResonanceBar`, `ShowStabilityMeter`, `ShowHoldProgress`, `ShowPitchDirection`, `ShowAirflowIndicator`)
- `ObservableCollection<IndicatorViewModel>` – 6 indikatorer: Resonance, Stability, Pitch, Hold, Shield, Airflow; aktiv/inaktiv per profil
- `IEnumerable<GuidanceItem>` bygget fra profilnøkler (`ClinicalPurposeKey`, `PhysicalFocusKey`, `CommonMistakesKey`, `SafetyInfoKey`)
- `CoachMessage` (string), `CoachSeverity` (`MessageSeverity` enum), `IsCoachMessageVisible` (bool)
- `IsExerciseRunning` (bool), `SessionElapsedSeconds` (audio-drevet fra `ExerciseLiveState`)
- `FeedbackModeKey`, `ThresholdStrategyKey`, `ActiveIndicatorPackage`

**Helper-typer (definer i samme fil eller som nested):**
- `IndicatorViewModel`: `LabelKey`, `IconGlyph`, `Value` (0–1), `IsActive`
- `GuidanceItem`: `IconGlyph`, `HeadingKey`, `BodyKey`
- `ShieldDisplayState` enum: `Safe`, `Warning`, `Locked`

**`ApplyProfile(ExerciseTargetProfile profile)` metode:**
- Setter visibility flags basert på `UsesResonance`, `UsesStability`, `UsesPitch`, `UsesIntensity`
- Bygger `GuidanceItems`-samling fra profilnøkler via `LocalizationService`
- Oppdaterer aktive indikatorer i `ObservableCollection`

**RelayCommands:**
- `StartExerciseCommand`, `StopExerciseCommand`, `PauseExerciseCommand`, `DismissCoachMessageCommand`
- Bruk eksisterende `RelayCommand`-mønster fra `RelayCommand.cs` (konstruktør med `Action` + optional `Func<bool>`)

**IDisposable:** Unsubscribe fra coordinator-events i `Dispose()`

## Oppgave 2 – Komplett ny `ExerciseWindow.xaml`

Lever komplett, syntaktisk korrekt XAML. Bevar eksisterende to-visnings-struktur (ListView/DetailView) fra vedlagt `ExerciseWindow.txt`.

**Fjern fra DetailView:**
- `TargetPitchPanel` og all Hz-basert visning
- Statisk feedback-UI

**`ExerciseGuidancePanel` (alltid synlig i DetailView, ikke betinget av økt-start):**
- `ItemsControl` bundet til `ViewModel.GuidanceItems` med `GuidanceItemTemplate`
- `GuidanceItemTemplate` forventer `GuidanceItem` med `IconGlyph`, `HeadingKey`, `BodyKey`
- Bruk eksisterende styles: `GuidanceCard`, `GuidanceHeading`, `GuidanceBody`
- Tekst resolves via `{loc:Loc}` markup extension eller `LocConverter`

**Live Feedback Panel (Visibility="Collapsed", toggled fra code-behind):**
- `ResonanceBarPanel`, `StabilityPanel`, `HoldPanel` (arc-geometri, `HoldProgress` 0–1 → 0–360°), `ShieldPanel` (alltid synlig når panelet vises), `CoachHintPanel` (med fade-animasjoner `CoachFadeIn`/`CoachFadeOut`)
- Hver metrikk-panel: `Visibility="Collapsed"` – drivet av ViewModel boolean properties
- `ProgressBar`-elementer bruker `MetricBar`-style med dynamiske Foreground-farger (`SuccessBrush`, `AccentPrimaryBrush`, `InfoBrush`)
- `PitchDirectionPanel`, `AirflowPanel` – betinget på eksisterende visibility flags

**Tema og lokalisering:**
- Alle farger via `DynamicResource` – ingen hardkoding
- Ingen hardkodet tekst – bruk `{loc:Loc KeyName}` gjennomgående
- Bevar eksisterende converters: `BoolToVisibility`, `InverseBoolToVisibility`, `ProgressToPercent`, `SeverityToBrush`
- Bevar eksisterende styles: `PrimaryButton`, `SecondaryButton`, `ExerciseCard`, `GoalBadge`, `IndicatorLabel`, `MetricBar`, `CardBorderStyle`

## Oppgave 3 – Lokalisering (RESX-nøkler)

**Lever ikke redigerte .resx-filer.** Returner i stedet en strukturert nøkkelliste i følgende format:

| Key | English Text |
|-----|-------------|

Inkluder:
- Nye key-grupper: `Exercise.ClinicalPurpose.*`, `Exercise.PhysicalFocus.*`, `Exercise.CommonMistakes.*`, `Exercise.SafetyInfo.*`, `Indicator.*`, `Coach.*`
- Eksplisitt liste over **utdaterte keys som skal slettes** (pitch/Hz-relaterte)
- Konsistente navn, ingen duplikater, fallback-trygge verdier

## Oppgave 4 – Datamodeller

**`ExerciseTargetProfile.cs`** – bekreft at følgende allerede er til stede, legg til evt. manglende:
- `ClinicalPurposeKey`, `PhysicalFocusKey`, `CommonMistakesKey`, `SafetyInfoKey` (string, init-only)
- `FeedbackModeKey`, `ThresholdStrategyKey`, `IndicatorPackageSummaryKey`
- `IReadOnlyList<IndicatorType> ActiveIndicators`
- Profil er immutable – ingen endringer i runtime-logikk

**`ExerciseDefinition.cs`** – bekreft/legg til:
- `ClinicalPurposeKey`, `PhysicalFocusKey`, `CommonMistakesKey` som localization keys (erstatter evt. gjenværende hardkodede tekstfelt)
- `FromExercise`-factory kopierer guidance keys fra Exercise-objekt
- Ingen Hz-range string formatting

---

## Forventet Leveranse

1. **Endringsoversikt fil-for-fil** – hva er endret og hvorfor
2. **Komplett `ExerciseDetailViewModel.cs`** – fullt implementert, produksjonsklar
3. **Komplett `ExerciseWindow.xaml`** – syntaktisk korrekt XAML, ikke pseudo-kode
4. **RESX nøkkelliste** – Markdown-tabell med nye keys + liste over slettede keys
5. **Oppdaterte datamodeller** – kun endrede seksjoner av `ExerciseTargetProfile.cs` og `ExerciseDefinition.cs`

## Kvalitetskrav

Koden skal være produksjonsklar. Full MVVM-separasjon, skalerbar guidance-arkitektur og lokalisering-first design er ikke valgfritt – det er baseline. Ingen hack-løsninger, ingen hardkodede verdier, ingen brudd på arkitekturreglene over.