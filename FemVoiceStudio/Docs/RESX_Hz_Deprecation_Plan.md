# RESX Hz-related Keys Deprecation Plan

Goal: remove or deprecate resource strings that embed raw Hz values (e.g. "Target: 220 Hz") and replace them with localization keys that accept numeric pitch values or formatted ranges supplied at runtime.

Steps
- Inventory: search all `Strings*.resx` and code/XAML for keys or literal strings that mention "Hz", "Hz)", numeric pitch labels, or keys containing `Pitch`, `Hz`, or `Frequency`.
- Usage map: for each key, record callers (XAML binding, ViewModel, code-behind, tests) and whether the value is presentation-only or used for logic.
- New pattern: introduce formatting keys that accept placeholders, e.g. `PitchTarget_Format = "Target: {0} Hz"` (temporary) but prefer keys that the UI formats with `LocalizationService.FormatPitch(double)` to avoid encoding units in RESX where possible.
- Compatibility: add temporary fallback keys with prefix `Obsolete_` or keep original keys but mark them in `Docs/CHANGELOG-MVVM-RESX.md` as deprecated and map them in `LocalizationService` to new keys for a 1-2 release transition.
- Implementation tasks:
  - Update ViewModels to expose numeric pitch bounds and formatting helpers instead of using raw RESX values.
  - Replace XAML usages: bind to numeric properties and use `LocConverter` or `StringFormat` to render with localized unit strings.
  - Update RESX files: add new non-Hz-specific keys (e.g. `Pitch_Target_Label`, `Pitch_Range_Template`) and add conservative translations where missing.
  - Run tests and UI spot-checks for each supported locale.
- Rollout strategy: keep both old and new keys for one release; log deprecation; remove old keys in a later major cleanup once translators have updated files.

Risks & mitigations
- Risk: missing translations cause visible placeholders — mitigate by adding conservative fallbacks in all locales and running the UI with each locale during QA.
- Risk: code still reading old keys for logic — ensure all logic reads numeric properties from models/ViewModels, not RESX.

Deliverables
- `Docs/RESX_Hz_Deprecation_Plan.md` (this file)
- Patch replacing RESX keys and updating ViewModels/XAML (follow-up)
- Tests / CI job verifying no missing resource keys

Next actions I can take now
- Run a repository search to inventory Hz-related keys and produce a short CSV/MD table of findings.
- Start updating one exercise ViewModel/XAML to use numeric pitch bounds + localized formatter as a concrete example.

Timeline suggestion
- Inventory & mapping: 1–2 hours
- Implement formatter + example replacement: 1–2 hours
- Update RESX across locales and CI run: 1–3 hours depending on locale count
