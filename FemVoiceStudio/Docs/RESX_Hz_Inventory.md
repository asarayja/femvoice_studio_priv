# RESX Hz Inventory

This file lists resource keys in `Resources/Strings.resx` that contain `Hz` or format placeholders that will render frequencies.

- `Exercise_103_Steps` — value previously contained explicit Hz; now generalised to avoid raw Hz exposure.
- `Exercise_104_Steps` — same, numeric start/end removed.
- `Exercise_105_Steps` — removed numeric target tone.
- `Exercise_109_Steps` — removed explicit range, now references "målområdet".
- `Exercise_112_Steps` — removed explicit Hz values and guidance updated.
- `Exercise_104_Rationale` — removed numeric range
- `Exercise_105_Rationale` — removed numeric range
- `Exercise_HzFormat` — `{0} Hz` (format string)
- `Exercise_HzRange` — `{0}-{1} Hz` (format string)
- `Exercise_Hz` — `Hz` (unit label)
- `Main_FrequencyHz` — `Frekvens (Hz)`
- Multiple coach/feedback strings use placeholders and units; examples:
  - `Feedback_PitchUnder`: `Gjennomsnittlig pitch er {0} Hz, som er under målområdet ({1}-{2} Hz).`
  - `Feedback_PitchOver`: `Gjennomsnittlig pitch er {0} Hz, som er over målområdet ({1}-{2} Hz).`
  - `Feedback_PitchOK`: `Bra! Gjennomsnittlig pitch er {0} Hz, innenfor målområdet.`
  - `Feedback_PitchVariationLow`: `Pitch-variasjonen er lav ({0} Hz). Målet er minst {1} Hz.`
  - `Feedback_PitchVariationGood`: `God pitch-variasjon! Du har {0} Hz variasjon.`
  - Resonance-related: `Utsøkt! Resonansen er optimalt feminine ({0}/{1} Hz)` etc.
  - `Heve pitch! Prøv å nå minst {0} Hz`
  - `Senk pitch! Prøv å holde deg under {0} Hz`

Notes:
- The exercise step/rationale entries above were updated to remove explicit Hz numbers.
- Format strings (`{0} Hz`, `{0}-{1} Hz`) remain; callers format numeric values into these. Per the deprecation plan, we should migrate callers to present qualitative guidance or to format numbers only in contexts where clinically necessary.

Next suggested actions:
- Replace format-string usages in ViewModels/Guidance so UI displays qualitative guidance instead of raw Hz where possible.
- If numeric display is required, centralize formatting via a `LocalizationService.FormatPitch(double)` that can optionally hide numbers per privacy/clinical settings.
- Add tests verifying no resource value directly contains hard-coded Hz numbers.
