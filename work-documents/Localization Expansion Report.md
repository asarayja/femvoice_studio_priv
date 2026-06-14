# FemVoice — Localization Expansion Report

Adds 9 new UI languages by creating **new resource files only**. No existing `.resx` was
edited, renamed, deleted or reformatted. No Russian resource was created. No voice/scoring/
runtime logic was changed (the one approved code change is the language-picker list).

---

## Agent 1 — Inventory & Key Coverage

**Existing resource files found (12, unchanged):**
`Strings.resx` (nb/default), `Strings.en.resx`, `Strings_en.resx` (legacy en),
`Strings.sv-SE.resx`, `Strings.da-DK.resx`, `Strings.fi-FI.resx`, `Strings.de-DE.resx`,
`Strings.fr-FR.resx`, `Strings.es-ES.resx`, `String.pt-BR.resx` (note: existing filename keeps
the `String.` prefix typo — left untouched), `Strings.it-IT.resx`, `Strings.hr-HR.resx`.

**Source files used:** `Strings.resx` (Norwegian/default — structure) + `Strings.en.resx`
(English — primary wording / safety tone).

**Total key count:** 1673 per file.

**Norwegian/English key mismatch:** none — 0 keys in nb-not-en, 0 in en-not-nb, 0 duplicate
key names. (2 keys — `Report_RecommendationHighFatigueFormat`,
`Report_RecommendationHighRecoveryCostFormat` — differ in how often `{0}` is repeated between
nb and en; both forms are valid `string.Format` inputs. New translations follow the English
primary source. This is a pre-existing source nuance, not introduced here.)

**New files to create:** 9 — `Strings.nl-NL.resx`, `Strings.pl-PL.resx`, `Strings.tr-TR.resx`,
`Strings.uk-UA.resx`, `Strings.ro-RO.resx`, `Strings.cs-CZ.resx`, `Strings.hu-HU.resx`,
`Strings.el-GR.resx`, `Strings.ar.resx`.

**Language picker registration needed:** YES — the picker in `SettingsWindow.xaml` is a
hardcoded `ComboBox` list, so new cultures are invisible in the UI without editing it. This was
flagged as **LANGUAGE PICKER REGISTRATION NEEDED** and **explicitly approved** by the user
before the picker was touched (see "Approved code change" below).

**Build wiring:** the SDK default glob embeds new `Strings.<culture>.resx` automatically and
produces satellite assemblies — no `.csproj` edit and no `Strings.Designer.cs` regeneration
needed (the picker uses `ResourceManager` directly, not the generated accessor).

---

## Translation approach

Per user decision: **full native translation** of all 1673 keys × 9 languages, English
fallback only where a translation was genuinely uncertain or would have broken structure.

Pipeline (translation separated from file assembly so file integrity is deterministic):
1. Extracted an ordered key manifest with English + Norwegian source per key.
2. Translated via 63 parallel translator agents (7 key-batches × 9 languages), each preserving
   placeholders (`{0}`, `{0:F0}`, `{name}`…), pipe `|` separators, file globs (`*.zip`/`*.pdf`/
   `*.json`) and units.
3. Assembled each `.resx` deterministically from a fixed template + the validated translation
   map. Any value that was empty, or whose placeholder / pipe-count / glob set did not match the
   source, was replaced with the English value (safe fallback). Result: every file has full key
   coverage and valid XML by construction.

**Native coverage achieved** (remaining are values legitimately identical to English — OK, Hz,
F1/F2/F3 labels, brand/abbreviations — counted as fallback for transparency):

| Culture | Language | Native | EN-identical | Native % |
|---|---|---|---|---|
| nl-NL | Dutch | 1617 | 56 | 96.7 % |
| pl-PL | Polish | 1642 | 31 | 98.1 % |
| tr-TR | Turkish | 1654 | 19 | 98.9 % |
| uk-UA | Ukrainian | 1651 | 22 | 98.7 % |
| ro-RO | Romanian | 1638 | 35 | 97.9 % |
| cs-CZ | Czech | 1641 | 32 | 98.1 % |
| hu-HU | Hungarian | 1650 | 23 | 98.6 % |
| el-GR | Greek | 1653 | 20 | 98.8 % |
| ar | Arabic (MSA) | 1655 | 18 | 98.9 % |

---

## Approved code change (language picker)

`FemVoiceStudio/Views/SettingsWindow.xaml` — appended 9 `ComboBoxItem` entries to the existing
`LanguageComboBox`, with native endonyms and culture tags matching the new resx suffixes:
Nederlands `nl-NL`, Polski `pl-PL`, Türkçe `tr-TR`, Українська `uk-UA`, Română `ro-RO`,
Čeština `cs-CZ`, Magyar `hu-HU`, Ελληνικά `el-GR`, العربية `ar`. The selection handler
(`OnLanguageSelectionChanged`) already maps `Tag` → `LocalizationService.SetLanguage(tag)`; no
handler logic changed.

---

## Agent 7 — Final Localization Gate

- **Localization Expansion Status:** Implemented.
- **Existing files modified:** none (12 source/satellite resx untouched).
- **New files created:** 9 resx + this report + QA report + `NewLanguageResourcesTests.cs`.
- **Languages added:** Dutch nl-NL, Polish pl-PL, Turkish tr-TR, Ukrainian uk-UA, Romanian
  ro-RO, Czech cs-CZ, Hungarian hu-HU, Greek el-GR, Arabic ar.
- **Russian excluded:** yes — no `ru-RU`/Russian file, not in the picker, guarded by tests.
- **Key coverage:** PASS (1673/1673 every file, 0 missing, 0 extra).
- **Placeholder preservation:** PASS (0 mismatches vs the English primary source; the only
  vs-Norwegian deltas are the 2 pre-existing `{0}`-repeat source keys).
- **Mojibake:** PASS (0 across all 9 files).
- **Safety language:** PASS (only benign `diagnos*` hits — the clinical *disclaimer* itself and
  the diagnostics-export/privacy strings, all carried over from the vetted nb/en source).
- **Fallback usage:** see table above (18–56 EN-identical per file, mostly legitimately equal).
- **Human review recommended:** Finnish/Croatian were out of scope; for the new set a light
  native polish is optional for Hungarian and Arabic phrasing, and Ukrainian terminology (none
  blocking).
- **Arabic RTL layout follow-up:** YES — text is correct MSA, but the app UI is not RTL-aware;
  right-to-left layout mirroring is a separate, non-blocking follow-up (no layout changed here).
- **Language picker registration needed:** done (approved).
- **Build/test:** `dotnet build … -p:EnableWindowsTargeting=true` for app + tests →
  **0 warnings / 0 errors**; all 9 satellite `*.resources.dll` generated. `dotnet test` not run
  here (Windows-only WPF runtime) — run on Windows.
- **Runtime logic changed:** no. **No-touch systems changed:** no.
- **Remaining blockers:** none.
- **Remaining non-blocking issues:** Arabic RTL follow-up; optional native polish pass.
- **Release recommendation:** **Ready for Controlled External Testing** (run `dotnet test` and
  a visual pass on Windows; consider RTL handling before promoting Arabic broadly).
