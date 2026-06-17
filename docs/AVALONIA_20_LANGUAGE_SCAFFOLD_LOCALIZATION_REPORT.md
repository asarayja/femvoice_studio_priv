# Avalonia 20-Language Scaffold Localization — Slice Report

Date: 2026-06-17 · Branch: `avalonia-20-language-scaffold-localization-slice` (off `main` @ `84e839b`).

> **Localization-resource structure + coverage only — behavior-neutral.** No runtime language switching, no
> language persistence, no WPF localization dependency, no Core resx edits, no clinical/domain or WPF change, no
> deferred-feature enablement. Per the agreed decision: **Avalonia-owned resources (Core untouched)** + **reuse-only
> translations** (no machine translations committed); novel scaffold phrases keep the Norwegian fallback and are
> recorded in a documented native-translation backlog.

## Detected 20 supported languages (source of truth)
From the original WPF `SettingsWindow.xaml` language combo (matches `FemVoice.Core/Resources/Strings.*.resx`):
`nb-NO` (neutral) · en-US · sv-SE · da-DK · fi-FI · de-DE · fr-FR · es-ES · pt-BR · it-IT · hr-HR · nl-NL · pl-PL ·
tr-TR · uk-UA · ro-RO · cs-CZ · hu-HU · el-GR · ar.

## Architecture decision
Avalonia has **no resources of its own** — it resolves text via the shared Core `LocalizationService`
(`ResourceManager "FemVoiceStudio.Resources.Strings"`). The ~105 Avalonia-only scaffold keys
(`*_Scaffold_*`, `Scaffold_*`, `Analysis_*`, `Diag_*`, `Reports_*`, `Shell_Nav_*`, `Settings_*` scaffold rows, …)
exist only as Norwegian fallbacks. To complete 20-language coverage **without** editing Core's shared
`Strings.*.resx` (which the known-failing portable baseline tests scrutinize), this slice adds an **Avalonia-owned
overlay**:
- `FemVoice.Avalonia/Localization/ScaffoldStrings.cs` — the 20 `Cultures`, a `TrustedKeys`/overlay of culture-
  invariant trusted values, and the `NativeTranslationBacklog` (keys awaiting native translation).
- `Localized.Get(key, fallback)` now consults the overlay first (current culture), then the existing Core
  resolver, then the Norwegian neutral fallback. **Not a new framework** — a small lookup ahead of the existing adapter.

## Translation source strategy (reuse-only)
- **Trusted (no native review needed):** the product name **`SmartCoach_Scaffold_Title` = "SmartCoach"** is
  populated for all cultures (product names — FemVoice Studio / SmartCoach / Avalonia — are preserved, not
  translated). Other deliberately-overridden scaffold terms had **no clean existing Core equivalent** (that is why
  they were made Avalonia-only in PR #27), so no further trusted reuse was safe.
- **Documented Norwegian-fallback backlog (105 keys):** all other Avalonia-only scaffold keys keep the Norwegian
  neutral fallback and are listed in `ScaffoldStrings.NativeTranslationBacklog`, awaiting native translation. **No
  machine/heuristic translations were committed** (no native reviewer; runtime switching deferred; safety/deferred
  wording must not be guessed across 20 languages).

## Key groups in scope (backlog)
Settings visual scaffold (`Settings_Scaffold_*`, `Settings_General/FirstRun/...`), SmartCoach scaffold
(`SmartCoach_Scaffold_*`, `SmartCoach_TodayFocus`), Progression scaffold (`Progression_Scaffold_*`), Analysis /
Reports / Diagnostics scaffolds (`Analysis_*`, `Reports_*`, `Diag_*`), shared scaffold phrases
(`Scaffold_DeferredBadge/Pending/ComingSoon/Synthetic`), Shell nav/status (`Shell_Nav_*`, `Shell_Mode`,
`Shell_MicStatus`, `Shell_DeferredFootnote`). Dashboard "Tonehøyde" + other already-Core-localized terms
(Resonans/Intonasjon/Theme/Language/Backup/Restore) resolve via existing Core keys and need no new work.

## Smoke
New `--avalonia-localization-coverage-smoke`: 20 cultures registered (matches the WPF source-of-truth); trusted
overlay resolves the product name across cultures; **every Avalonia-only `Localized.Get` key is accounted for** —
either trusted or in the documented backlog — with **0 broken/undocumented keys** (source cross-check; fails on a
new unregistered scaffold key; skips→passes from the published DLL where the source tree isn't shipped); no
mojibake in the overlay. It distinguishes **trusted (1) / documented-fallback (105) / broken (0)** and fails only
on broken. `--localization-text-polish-smoke` and `--theme-loc-smoke` remain green.

## Guardrails (verified)
**Core `Strings.*.resx` untouched** (`git status` clean under `FemVoice.Core/`); portable **1570/1580** unchanged
(baseline not perturbed). `Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` refs only `FemVoice.Core` +
`FemVoice.Audio.Abstractions`; no WPF localization dependency (`LocExtension`/`LocConverter`); no runtime language
switching/persistence; no clinical/domain or WPF change; no runtime platform implementation.

## Known caveats / native-review requirement
- **Full translated UI parity is NOT achieved** — only the Norwegian default renders today (runtime switching
  deferred). The 105 backlog keys require **native translation** before any non-Norwegian UI can claim parity.
- Trusted reuse is intentionally minimal (product name only) to avoid committing unverified translations.
- Repo is private/proprietary; no open-source license assumed.
