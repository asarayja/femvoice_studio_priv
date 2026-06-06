RESX Plan — guidance keys and policy

Goal
- Add localization keys used by `ExerciseTargetProfile` so GuidancePanel and indicators display meaningful clinical text.

Scope
- Add keys for: Clinical purpose, Physical focus, Common mistakes, Safety info, Feedback mode, Threshold strategy, Indicator package summaries.
- Keep keys short, semantic, and grouped by profile.
- Provide Norwegian clinical copy as defaults (can be expanded later).

Keys to add (example list)

For resonance humming:
- Purpose_ResonanceHumming = "Hensikt: Etablere fremre resonans og redusere bakre belastning."
- Focus_ResonanceForward = "Fokus: Føl og bevar resonans i fremre ansatsrom."
- Mistakes_ResonanceHumming = "Vanlige feil: Spenne kjevemuskulatur, løfte skuldre eller presse i halsen."
- Safety_ResonanceHumming = "Sikkerhet: Avbryt ved sårhet eller vedvarende heshet; kontakt terapeut om nødvendig."
- FeedbackMode_ResonanceHumming = "Visuell: Resonans-indikator"
- ThresholdStrategy_Adaptive = "Terskel: Adaptiv, justeres etter prestasjon"
- IndicatorPackage_ResonanceHumming = "Resonans • Stabilitet"

For pitch exercises (generic keys):
- Purpose_PitchExercise = "Hensikt: Trene presis pitchkontroll innen komfortsonen."
- Focus_Pitch = "Fokus: Kontroller pitch uten anspenthet."
- Mistakes_PitchExercise = "Vanlige feil: Overkompensering, hard glottal attack."
- Safety_PitchExercise = "Sikkerhet: Unngå ved smerte eller vedvarende heshet."
- FeedbackMode_Pitch = "Auditiv + visuell: Komfortsone-indikator"
- ThresholdStrategy_ComfortZone = "Terskel: Komfortsone-basert"
- IndicatorPackage_Pitch = "Pitch • Stabilitet"

(Repeat pattern for Intonation, StrawPhonation, StabilityTraining, GlideUp, etc.)

Design notes
- Text only: do not hardcode colours in RESX; UI chooses brushes via resources.
- Keep guidance short (one sentence heading/body) with optional extended help pages later.
- Support formatting tokens if needed (e.g., {0} for dynamic values).
- Provide both short label keys (for badges) and longer paragraph keys (for Guidance body) where required.

Light/Dark and accessibility
- Strings are colour-agnostic. Ensure UI uses DynamicResource brushes that adapt to themes.
- Use simple Norwegian plain language for clinical copy; avoid long paragraphs.

Implementation steps
1. Add keys to `Resources/Strings.resx` (and localized variants if present).
2. Run `dotnet build` and `dotnet test` to verify no missing resources break compile.
3. Inspect `ExerciseWindow` to ensure `GuidanceItems` and `FeedbackModeLabel` render expected text.
4. Iterate copy with clinician if needed.

Do you want me to apply these keys now to `Resources/Strings.resx` with the suggested Norwegian copy? If yes, I will add a conservative set for all factory profiles and run the build/tests.
