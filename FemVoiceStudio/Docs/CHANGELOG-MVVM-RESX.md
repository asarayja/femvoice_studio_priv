Change Log — MVVM, ViewModel binding and RESX updates (2026-02-20)

Summary
- Purpose: Centralize MVVM, wire live UI to `ExerciseDetailViewModel`, add guidance/localisation keys and conservative RESX entries across locales. Verified with `dotnet build` and `dotnet test` (all tests passed).

Files modified / added

- **ViewModels/RelayCommand.cs**: Added a single shared `RelayCommand` ICommand implementation and removed duplicate RelayCommand definitions.
- **ViewModels/ExerciseDetailViewModel.cs**: Exposed binding-friendly properties (brush helpers, visibility flags, `GuidanceItems`, `Indicators`), added INotifyPropertyChanged raises for new properties, kept coordinator subscription and `ApplyLiveState` logic intact.
- **Views/ExerciseWindow.xaml**: Replaced manual mirroring for many metrics with XAML bindings: metric values (`PrimaryMetricScore`, `StabilityScore`), visibility flags (`ShowResonanceBar`, `ShowStabilityMeter`, `ShowPitchDirection`, `ShowHoldProgress`, `ShowAirflowIndicator`), `GuidanceItems` ItemsControl binding, and `FeedbackModeKey` via `LocConverter`. Preserved Storyboards and hold arc geometry elements.
- **Views/ExerciseWindow.xaml.cs**: Set `LiveFeedbackPanel.DataContext` to the `ExerciseDetailViewModel`; removed mirroring cases for bound properties while preserving `MirrorHoldArc()` (geometry math) and `MirrorCoachHint()` (Storyboard triggers). Start/Stop lifecycle, DispatcherTimer usage, and session storage remain unchanged.
- **Models/ExerciseTargetProfile.cs**: Populated factory profiles with guidance/localisation keys: `ClinicalPurposeKey`, `PhysicalFocusKey`, `CommonMistakesKey`, `SafetyInfoKey`, `FeedbackModeKey`, `ThresholdStrategyKey`, and `IndicatorPackageSummaryKey` so GuidancePanel and indicators are meaningful by default.
- **Converters/LocConverter.cs**: Added a `MarkupExtension` + `IValueConverter` to resolve localisation keys in bindings and DataTemplates.
- **Resources/Strings.resx**: Added guidance headings and Norwegian copy for profile-specific keys (e.g., `Purpose_ResonanceHumming`, `Focus_VowelResonance`, `Mistakes_*`, `Safety_*`, `FeedbackMode_*`, `IndicatorPackage_*`) and general heading keys (`Guidance_ClinicalPurpose`, etc.).
- **Resources/Strings.en.resx**: Added English fallback entries for the same guidance/feedback keys.
- **Resources/Strings.*.resx** (other locales): Added minimal guidance fallback keys to locale files found under `Resources/` to avoid missing-resource issues (files updated: `de-DE`, `fr-FR`, `es-ES`, `it-IT`, `da-DK`, `sv-SE`, `fi-FI`, `hr-HR`).
- **Docs/RESX_Plan.md**: New. Contains the RESX change plan, key naming conventions, and implementation notes.
- **Docs/CHANGELOG-MVVM-RESX.md**: This file — concise file-by-file changelog.

Verification
- Ran `dotnet build` and `dotnet test` after changes. Build succeeded and all tests passed: total 315, failed 0, succeeded 315.

Notes / Next steps
- Replace English fallback copy in locale RESX files with proper translations.
- Optionally add a small `IndicatorPackage` enum/helper to centralize package names and localization lookup.
- Generate a more detailed per-line changelog if you want to commit these changes with granular diffs.

If you want, I can (pick one):
- Translate the RESX values into the target languages now (I can add conservative translations),
- Implement an `IndicatorPackage` helper and wire it to `ExerciseTargetProfile`, or
- Prepare a commit-ready patch and a short commit message for review.
