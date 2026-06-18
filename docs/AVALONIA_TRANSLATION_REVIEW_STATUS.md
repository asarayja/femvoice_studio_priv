# Avalonia Translation — Review Status

Date: 2026-06-18 · Source of truth: `FemVoice.Avalonia/Localization/TranslationStatus.cs` (this table mirrors it).

> ⚠️ **The 18 machine/model-generated languages are NOT native-speaker reviewed.** No production/clinical or
> native-language parity may be claimed for them until reviewed. Flip `IsNativeReviewed` in `TranslationStatus.cs`
> (and update this table) only after an actual native-speaker review. See
> [AVALONIA_TRANSLATION_CONTRIBUTION_WORKFLOW.md](AVALONIA_TRANSLATION_CONTRIBUTION_WORKFLOW.md).

## Status legend
- **Source** — authored reference language (Norwegian).
- **Fallback** — global fallback used when a language lacks a string (English).
- **Machine** — model-generated overlay; awaiting native review.
- **Reviewed** — a native speaker has reviewed & approved.

## Languages (20)
| Culture | Language | Role | Machine-generated | Native-reviewed |
| --- | --- | --- | --- | --- |
| nb-NO | Norsk (bokmål) | **Source / reference** | no | yes (authored) |
| en-US | English | **Global fallback** | no (authored) | not yet |
| sv-SE | Svenska | translation | **yes** | **no** |
| da-DK | Dansk | translation | **yes** | **no** |
| fi-FI | Suomi | translation | **yes** | **no** |
| de-DE | Deutsch | translation | **yes** | **no** |
| fr-FR | Français | translation | **yes** | **no** |
| es-ES | Español | translation | **yes** | **no** |
| pt-BR | Português (Brasil) | translation | **yes** | **no** |
| it-IT | Italiano | translation | **yes** | **no** |
| hr-HR | Hrvatski | translation | **yes** | **no** |
| nl-NL | Nederlands | translation | **yes** | **no** |
| pl-PL | Polski | translation | **yes** | **no** |
| tr-TR | Türkçe | translation | **yes** | **no** |
| uk-UA | Українська | translation | **yes** | **no** |
| ro-RO | Română | translation | **yes** | **no** |
| cs-CZ | Čeština | translation | **yes** | **no** |
| hu-HU | Magyar | translation | **yes** | **no** |
| el-GR | Ελληνικά | translation | **yes** | **no** |
| ar | العربية | translation | **yes** | **no** |

## Coverage notes
- **Visible scaffold UI** (nav rail, shell chrome, Settings sections, local-preferences card — the English overlay
  key set, currently **48 keys**) is translated for **all overlay languages** (en + the 18). Norwegian (source)
  resolves via in-code fallbacks.
- **Deeper scaffold strings** not in the overlay (e.g. Analysis/Reports/Diagnostics/SmartCoach/Progression detail
  text) fall back to **English**, then Norwegian. These are documented in
  `ScaffoldStrings.NativeTranslationBacklog` and are not individually translated per language yet.
- **No claim of full translated UI parity or clinical/native parity** is made.

## When a language is reviewed
Update both `TranslationStatus.cs` (`IsMachineGenerated: false, IsNativeReviewed: true`, add reviewer/date to
`Notes`) and the corresponding row above, then run `--avalonia-translation-contribution-smoke` and the full gate.

> The repository is private/proprietary; no open-source license assumed.
