# Avalonia Translation — Contribution & Review Workflow

Date: 2026-06-18 · Avalonia head only. **Readiness/governance slice — no native review performed here.**

> ⚠️ **Machine/model-generated translations.** The 18 non-English / non-Norwegian languages in
> `FemVoice.Avalonia/Localization/ScaffoldTranslations.cs` are **model-generated and NOT native-speaker reviewed**.
> **Do not claim production/clinical or native-language parity** for any language until a native speaker has
> reviewed and approved it (flip its `IsNativeReviewed` flag — see below). This document defines the safe workflow
> for that future review/contribution. It does **not** itself review any language.

## The language model (how resolution works)
- **Source / reference language: Norwegian (`nb-NO`)** — authored. Strings live as the in-code fallback arguments
  to `Localized.Get(key, "<norsk>")` and in the shared Core neutral resource. Norwegian has **no overlay entry**.
- **Global fallback: English (`en-US`)** — anything missing for the selected language falls back to English, then
  to the Norwegian source string.
- **Avalonia overlay translations** live in `ScaffoldTranslations.ByLanguage` (2-letter language key → `{ key → text }`),
  for the high-visibility scaffold UI (nav rail, shell chrome, Settings sections, local-preferences card).
- **Review/readiness metadata** lives in `FemVoice.Avalonia/Localization/TranslationStatus.cs` — one
  `CultureTranslationStatus` per supported culture: `Code`, `DisplayName`, `IsSource`, `IsFallback`,
  `IsMachineGenerated`, `IsNativeReviewed`, `Notes`.

`Localized.Get(key, norwegianFallback)` resolves in this order: per-language overlay → genuine culture-specific
Core resource → **English fallback** (overlay then Core) → Norwegian source string.

## How to REVIEW a language (native speaker)
1. Open `ScaffoldTranslations.cs`, find the language's 2-letter block (e.g. `["de"] = new() { ... }`).
2. Correct the strings in place (this file only — see boundaries). Keep the same keys; do not add/remove keys here.
3. In `TranslationStatus.cs`, set that culture's `IsMachineGenerated: false, IsNativeReviewed: true` and update
   `Notes` (e.g. "Reviewed by <name/initials>, <date>"). **Only flip `IsNativeReviewed` to true after an actual
   native-speaker review** — never for convenience.
4. Run the smokes (below). Open a PR; the PR should name the reviewer and the language.

## How to UPDATE / ADD a translation safely
- **Update:** edit the value in `ScaffoldTranslations.cs` for that language. Nothing else.
- **Add a new scaffold string:** (a) use `Localized.Get("New_Key", "<norsk fallback>")` in the VM/view;
  (b) add `"New_Key"` to the **English** overlay (so the global fallback covers it) and to other languages as
  available; (c) register it in `ScaffoldStrings.NativeTranslationBacklog` if it is an Avalonia-only key (the
  coverage smoke enforces this). The English overlay key set defines the **required visible key set**.

## Boundaries (DO NOT cross)
- **Do not** edit Core resx (`FemVoice.Core/Resources/Strings*.resx`) or any WPF localization
  (`LocalizationService`, `SetLanguage`, `LocExtension`, `LocConverter`, `ThemeManager`).
- **Do not** touch DB/`UserSettings`/SQLite, audio/mic, clinical/domain, SmartCoach/Progression, privacy/backup/
  reports/export.
- **Do not** mutate the global thread culture; language activation is Avalonia-local only.
- Keep `FemVoice.Avalonia` referencing only `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- **Do not** remove the machine-generated caveat or mark an unreviewed language native-reviewed.

## How to RUN validation after a translation change
From the repo root (Linux, .NET 10 at `~/.dotnet`):
```
dotnet run --project FemVoice.Avalonia -- --avalonia-translation-contribution-smoke
dotnet run --project FemVoice.Avalonia -- --avalonia-localization-coverage-smoke
dotnet run --project FemVoice.Avalonia -- --settings-language-activation-smoke
```
Then the full gate (`scripts/linux-portable-gate.sh` style): `dotnet build`, `dotnet list package --vulnerable`,
`dotnet test FemVoice.Tests.Portable` (known baseline 1570/1580), all Avalonia smokes, and packaging.

## What `--avalonia-translation-contribution-smoke` checks
All 20 cultures have metadata; exactly one source (`nb-NO`) and one fallback (`en-US`); the 18 others are
`IsMachineGenerated && !IsNativeReviewed` with no over-claim; the required visible keys are covered and non-empty
for every overlay language; English fallback holds for unsupported cultures; the machine-generated caveat is
present; and the translation source files contain no WPF-localization/DB/global-culture references.

> The repository is private/proprietary; no open-source license assumed.
