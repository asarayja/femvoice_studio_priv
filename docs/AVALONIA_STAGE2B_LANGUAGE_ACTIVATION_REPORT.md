# Avalonia Stage 2B — Runtime Language Activation (LIVE, 20 languages) — Slice Report

Date: 2026-06-18 · Branch: `avalonia-stage2b-language-activation-slice` (off `main` @ `aa35246`).

> **Stage 2B.** Activates the saved **language** preference at runtime, **Avalonia-only**, with **LIVE in-session
> switching** (no restart): changing the language in Settings re-renders the navigable UI immediately. The
> high-visibility UI (navigation rail, shell chrome, the Settings page sections, and the local-preferences card) is
> translated for **all 20 supported languages**, **ENGLISH is the global fallback**, and Norwegian is the source.
> No WPF `LocalizationService`/`SetLanguage`/`LocExtension`/`LocConverter`, no DB/`UserSettings`/SQLite, no WPF
> `ThemeManager`, no Core/WPF/clinical/audio change, **no global thread-culture change**.

> ⚠️ **MACHINE-TRANSLATED — NOT native-reviewed.** Per the explicit user decision (which overrode the earlier
> "no machine translation" rule), the 18 non-English / non-Norwegian languages are **model-generated** translations
> (see `Localization/ScaffoldTranslations.cs`, clearly marked). They make the UI switch end-to-end but should be
> reviewed by native speakers before any production/clinical release.

## How it works (Avalonia-LOCAL, live)
- `Localized.CurrentCulture` is the Avalonia-local UI culture; setting it raises **`Localized.LanguageChanged`**.
- `ShellViewModel` subscribes to that event and **rebuilds the navigation rail + the current localized page +
  chrome** in the new culture (the dashboard uses hardcoded strings and is intentionally not rebuilt; a running
  exercise is left in place). This is the live in-session refresh.
- `LanguageActivation.Apply(code)` (Save) sets the culture live; `LanguageActivation.ApplyFromStore()` (startup)
  applies the saved language before the window is built. Both are Avalonia-local — no Core `SetLanguage`, no global
  thread-culture change, no shared-Core mutation.
- `Localized.Get` resolution order: (1) per-language Avalonia overlay (`ScaffoldTranslations`); (2) a genuine
  culture-specific value from the shared resources; (3) **ENGLISH fallback** (overlay then English resource) for
  non-Norwegian cultures; (4) the Norwegian source string. For Norwegian, the Norwegian source is used directly.

## Coverage
- **Norwegian (nb):** source language (in-code fallbacks + Core neutral resource).
- **English (en):** complete for the high-visibility keys + global fallback.
- **18 others (sv, da, fi, de, fr, es, pt, it, hr, nl, pl, tr, uk, ro, cs, hu, el, ar):** machine-translated for the
  high-visibility keys (nav rail, shell chrome, Settings sections, local-preferences card) in
  `ScaffoldTranslations.ByLanguage`.
- **Deep deferred-scaffold strings** (Analysis/Reports/Diagnostics/SmartCoach/Progression detail text on the
  display-only placeholder pages) are NOT individually translated for all 20 yet → they fall back to **English**
  (then Norwegian source). Filling these is straightforward follow-up content work.

## What was added / changed
- **`Localization/ScaffoldTranslations.cs`** (new) — the machine-generated per-language translation table (20 langs).
- **`Localization/Localized.cs`** — Avalonia-local `CurrentCulture` with a `LanguageChanged` event; resolution with
  per-language overlay → culture-specific Core → **English fallback** → Norwegian source.
- **`Localization/ScaffoldStrings.cs`** — `TryGet(culture, key)` consults `ScaffoldTranslations.ByLanguage` (by
  2-letter language) ahead of the Core resolver; product-invariant values preserved.
- **`ViewModels/ShellViewModel.cs`** — `BuildPages()`/`BuildNav()` refactor + subscribes to `LanguageChanged` to
  rebuild nav + current page + chrome live; `NavItems` is now observable.
- **`App.axaml.cs`** — applies the saved language (with the Stage-2A theme) at startup before the window is built.
- **`ViewModels/UiPreferencesViewModel.cs`** — `Save` applies theme + language **live**; truthful status copy.

## Smoke coverage
**`--settings-language-activation-smoke`** (33rd) now asserts: per-culture apply (sv/en/nb); **`allCulturesSwitch`**
(every one of the 20 cultures returns a translated nav label); **`englishFallback`** (a culture outside the 20 →
English, not Norwegian); **`englishOverlay`** + **`norwegianFallback`**; **`liveRefreshSignal`** (changing the
language raises `LanguageChanged` exactly on change); **`saveAppliesLive`** (Save switches the running resolver —
the status even renders in the new language); startup read; missing/corrupt/unknown safe; **`threadCultureUntouched`**
(global thread culture never changed). `--settings-theme-activation-smoke`, `--theme-loc-smoke`,
`--localization-text-polish-smoke`, `--avalonia-localization-coverage-smoke`, `--settings-smoke`,
`--settings-visual-parity-smoke`, `--shell-smoke` remain green.

## Guardrails (verified)
Build 0/0; **33/33 smokes**; `Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` +
`FemVoice.Audio.Abstractions`; leak guard clean (no `IDatabaseService`/WPF `ThemeManager`/`LocalizationService`/
`LocExtension`/`LocConverter`/`MicrophoneCalibration`/engine refs); **Core `Strings.*.resx` untouched (git-clean)**;
portable **1570/1580** (10 known localization-data baseline failures; unchanged); no global thread-culture change.

## Explicitly NOT done (boundaries respected)
No WPF `LocalizationService`/`SetLanguage`/`LocExtension`/`LocConverter`; no WPF settings files; no DB/`UserSettings`/
SQLite/`IDatabaseService`; no WPF `ThemeManager`; no global thread-culture change; no Core resx edits; no audio/mic,
backup/restore, privacy export/delete, reports/export, clinical/domain, SmartCoach/Progression, Core, or WPF change.
Reduce-motion remains persisted-only. Stage-2A theme activation remains intact.

> The repository is private/proprietary; no open-source license assumed.
