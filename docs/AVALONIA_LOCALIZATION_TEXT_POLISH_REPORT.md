# Avalonia Localization & Text Polish — Slice Report

Date: 2026-06-17 · Branch: `avalonia-localization-text-polish-slice` (off `main` @ `e05e8b5`).

> **Text/localization polish only — behavior-neutral.** No new behavior, persistence, runtime language switching,
> or deferred-feature enablement; no clinical/domain or WPF change; no WPF localization dependency.

## Motivation
Recent scaffold work reused some Core `Strings.resx` keys whose neutral values are English or terse/lowercase,
producing mixed UI text (e.g. tiles "Dager på rad" / "økter" / "helse"; a Settings section titled "Audio settings";
Progression "Pitch"/"Score"; long consent paragraphs used as privacy row labels; Dashboard "Pitch-trace").

## Localization architecture (unchanged)
Avalonia uses the existing read-only `Localized.Get(key, fallback)` adapter over Core's `LocalizationService`
(`ResourceManager "FemVoiceStudio.Resources.Strings"`). When a key is missing it returns the provided fallback. The
fix keeps this pattern: for scaffold display labels that must be consistently Norwegian, the reused Core keys are
swapped for **Avalonia-scaffold-namespaced keys** (not present in Core), so `Localized.Get` deterministically
returns the Norwegian fallback. No Core resx is edited; no `SetLanguage`/culture change; no language persistence.

## Text changes (terminology decisions)
- **SmartCoach tiles**: `SmartCoach_Sessions`("økter")→ scaffold "Økter denne uken"; `SmartCoach_Health`("helse")→
  "Helsescore"; streak stays "Dager på rad". Page title `SmartCoach_Title`("Smart Coach")→ **"SmartCoach"** (one
  word, matches the nav label + product name).
- **Progression**: `Dashboard_Score`("Score")→ "FemVoice-score"; parameter `Dashboard_Pitch`("Pitch")→
  **"Tonehøyde"** (Resonans/Intonasjon already correct).
- **Settings**: `Settings_AudioSettings`("Audio settings")→ **"Lydinnstillinger"**; privacy rows
  (`Privacy_DiagnosticsConsent`/`Privacy_ResearchWarning` resolve to long consent paragraphs) → short labels
  **"Diagnostikk-samtykke"** / **"Forskningsdeling"**. Other section titles already resolved to good Norwegian
  (Tema, Språk, Stemmemål, Tilgjengelighet, Database, Personvern og lokale data, Om) and were left as-is.
- **Dashboard**: chart labels "Pitch-trace (Hz…)" / "Pitch-trace vises her under opptak" → **"Tonehøyde (Hz…)"** /
  **"Tonehøyde vises her under opptak"** (chart semantics unchanged — text only).
- Deferred/display-only phrasing kept consistent across pages: **"Utsatt · kun visning"**, **"Kommer senere"**,
  **"Syntetisk · ingen lagring"**, **"Kun visning · ingen lagring · ingen klinisk endring"**.

Exercise catalog names/descriptions (from the Core domain catalog) were **not** touched — only Avalonia-only
display text.

## Pages touched
SmartCoach scaffold, Progression scaffold, Settings, Dashboard (label text), + the new smoke. (Exercise Guide /
runtime / Analysis / Reports / Diagnostics already used consistent Norwegian; left unchanged.)

## Smoke
New `--localization-text-polish-smoke` (28th, VM-level + Dashboard source check): SmartCoach tile labels +
one-word "SmartCoach"; Progression "FemVoice-score" + params Resonans/Tonehøyde/Intonasjon; Settings has
"Lydinnstillinger" and no "Audio settings"; privacy row labels short (≤48 chars, not the consent paragraph); **no
English/terse leftovers** ("Pitch"/"Score"/"økter"/"helse"/"Audio settings") across scaffold labels;
deferred-phrase consistency; Dashboard says "Tonehøyde" not "Pitch-trace" (source check, skips→pass from the
published DLL). `--theme-loc-smoke` remains green.

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
no WPF localization dependency (no `LocExtension`/`LocConverter`); leak guard clean; no persistence/DB/analytics; no
runtime language switching/persistence; no SmartCoach/Progression engine behaviour; no clinical/domain or WPF change.

> The repository is private/proprietary; no open-source license assumed.
