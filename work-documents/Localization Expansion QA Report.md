# FemVoice — Localization Expansion QA Report (Agent 6)

Validation of the **9 newly created files only**. Existing files were read for comparison and
never modified.

Files checked: `Strings.nl-NL.resx`, `Strings.pl-PL.resx`, `Strings.tr-TR.resx`,
`Strings.uk-UA.resx`, `Strings.ro-RO.resx`, `Strings.cs-CZ.resx`, `Strings.hu-HU.resx`,
`Strings.el-GR.resx`, `Strings.ar.resx`.

## QA Status: **PASS**

| Culture | Keys | Missing | Extra | Empty | Placeholder¹ | Glob | Pipe | Mojibake | XML valid |
|---|---|---|---|---|---|---|---|---|---|
| nl-NL | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| pl-PL | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| tr-TR | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| uk-UA | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| ro-RO | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| cs-CZ | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| hu-HU | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| el-GR | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |
| ar | 1673 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | ✅ |

¹ Placeholder column is measured against the **English primary source** = 0 mismatches.
Against the Norwegian file there are 2 keys per file
(`Report_RecommendationHighFatigueFormat`, `Report_RecommendationHighRecoveryCostFormat`) where
English repeats `{0}` and Norwegian does not — a pre-existing nb/en source difference, both
valid for `string.Format`. The translations follow the English form; no runtime risk.

## Detailed checks

- **Key coverage:** each new file has exactly the 1673 keys of `Strings.resx`/`Strings.en.resx`.
  Missing = 0, unexpected/extra = 0.
- **Empty values:** none.
- **Placeholder preservation:** `{0}`, `{1}`, `{2}`, `{0:F0}`, `{1:P0}`, `{name}`, `{count}`
  etc. preserved; pipe `|` separator counts preserved; file globs `*.zip`/`*.pdf`/`*.json`
  preserved verbatim (only the human-readable filter description translated). Verified by the
  deterministic assembler (English fallback on any structural break) and re-verified post-write.
- **XML validity:** all 9 parse cleanly; element text is XML-escaped.
- **Mojibake:** none — scanned for `Ã`, `Â`, `â€™`, `â€“`, replacement char `�`. Native scripts
  intact (Greek tonos, Ukrainian Cyrillic, Arabic, Turkish ı/ş/ğ, Polish/Czech/Hungarian/
  Romanian diacritics).
- **Safety language:** no diagnosis/guaranteed-feminization/“bad voice”/shame wording. The only
  `diagnos*` matches are the **clinical disclaimer** (states FemVoice is *not* a medical
  diagnostic tool) and the diagnostics-export / local-storage privacy strings — all carried
  over from the already-vetted Norwegian/English source.
- **Release wording:** no `beta` / `public release` / `medical certification` / `diagnostic
  tool` claims introduced.
- **Russian exclusion confirmed:** no `ru-RU`/Russian file created, not added to the picker,
  guarded by `NewLanguageResourcesTests.NoRussianResourceFile_WasAdded`.
- **Arabic RTL follow-up:** YES (non-blocking) — translations are correct MSA; the UI is not
  RTL-aware. Right-to-left mirroring is a separate follow-up; no layout was changed here.

## Files needing human review

None blocking. Optional native polish: Hungarian/Arabic phrasing and Ukrainian terminology.

## Blockers

None.

## Automated guard

`FemVoiceStudio.Tests/NewLanguageResourcesTests.cs` re-checks all of the above (existence, XML
validity, full key coverage, no empty, placeholder/pipe/glob preservation, mojibake, Russian
exclusion) so regressions are caught on the Windows `dotnet test` run.
