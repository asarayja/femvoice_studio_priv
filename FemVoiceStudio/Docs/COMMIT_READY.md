# Commit-ready summary and suggested commit message

Summary of changes:

- MVVM / localization cleanup:
  - Added `IndicatorType` enum and `ActiveIndicators` to `Models/ExerciseTargetProfile.cs`.
  - Exposed `IndicatorPackage` via `IndicatorPackage` property on `ExerciseTargetProfile`.
  - Added new helper model `Models/IndicatorPackage.cs`.
  - Centralized `RelayCommand` earlier (no-op in this patch but part of prior work).

- Localization / RESX:
  - Removed embedded Hz numeric literals from exercise step and rationale entries in `Resources/Strings.resx`.
  - Propagated equivalent qualitative/phrasings to all locale RESX files under `Resources/Strings.*.resx`.
  - Added `Docs/RESX_Hz_Deprecation_Plan.md` and `Docs/RESX_Hz_Inventory.md`.

- Misc:
  - Added `Docs/COMMIT_READY.md` (this file).

Suggested commit message:

    MVVM/localization: add IndicatorPackage, remove hard-coded Hz in RESX

    - Add `IndicatorType` and `ActiveIndicators` to `ExerciseTargetProfile`
    - Add `IndicatorPackage` model and expose `IndicatorPackage` property
    - Remove explicit Hz numbers from exercise guidance strings in base RESX
    - Update locale RESX files to use qualitative phrasing (avoid raw Hz exposure)
    - Add RESX deprecation plan and inventory docs

Files changed (high level):

- Models/ExerciseTargetProfile.cs
- Models/IndicatorPackage.cs (new)
- Resources/Strings.resx (updated)
- Resources/Strings.*.resx (updated locales: en, de-DE, fr-FR, es-ES, it-IT, sv-SE, hr-HR, fi-FI, da-DK)
- Docs/RESX_Hz_Deprecation_Plan.md (new)
- Docs/RESX_Hz_Inventory.md (new)
- Docs/COMMIT_READY.md (new)

How to commit locally:

```bash
git add -A
git commit -m "MVVM/localization: add IndicatorPackage, remove hard-coded Hz in RESX"
git push origin your-branch-name
```
