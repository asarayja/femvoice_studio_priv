# Avalonia Stage 1 — Harmless Local UI-Preference Persistence — Slice Report

Date: 2026-06-17 · Branch: `avalonia-stage1-ui-preference-persistence-slice` (off `main` @ `4c3840b`).

> **Stage 1 only.** Adds Avalonia-local persistence for **three harmless display-only preferences** (theme,
> language, reduce-motion). **Persistence only — NO runtime activation:** the saved theme/language are NOT applied
> to the running app (that is the separately-approved Stage 2). Does not use the WPF `UserSettings` DB row, SQLite,
> `IDatabaseService`, WPF `ThemeManager`, or WPF `LocalizationService`/`SetLanguage`. No audio, clinical/domain,
> SmartCoach, Progression, privacy/database, backup/restore, or WPF behaviour touched.

## What was added
- **`Preferences/UiPreferences.cs`** — model: `Theme` (`ThemePreference` System/Light/Dark), `Language` (culture
  code, default `nb-NO`), `ReduceMotion` (bool), `Version`. `Defaults()` + `Normalized()` (unknown language → default).
- **`Preferences/UiPreferencesStore.cs`** — Avalonia-owned, file-backed (`System.Text.Json`). Default path
  `<ApplicationData>/FemVoiceAvalonia/ui-preferences.json` — **distinct from any WPF settings file / the SQLite DB**.
  `Load()` returns safe defaults on missing/empty/invalid/**corrupt** file (catches all, never throws → no startup
  crash). `Save()` writes the normalized prefs; creates the folder if needed. Accepts an explicit path (tests).
- **`ViewModels/UiPreferencesViewModel.cs`** — interactive editor for the 3 prefs + `SaveCommand`/`ReloadCommand`;
  loads current values on construct. Theme options (enum) + language options (reuse Avalonia-owned
  `ScaffoldStrings.Cultures`, **no WPF LocalizationService**). **No runtime activation** — Save/Reload only
  round-trip the file; nothing applies a theme variant or changes the culture.
- **`SettingsViewModel`** — adds a **lazily-constructed** `Preferences` property (so no file I/O at shell startup;
  the file is read only when the Settings page is shown). Parameterless ctor preserved; not IDisposable; the
  behaviour-heavy sections remain inert. Deferred banner reworded honestly ("local UI choices are saved locally but
  not activated yet").
- **`SettingsView.axaml`** — one new **interactive** card ("Lokale UI-innstillinger") with enabled theme/language
  combos + reduce-motion toggle + Save/Reload, bound to `Preferences`. The existing behaviour-heavy sections stay
  disabled/inert.

## Inert sections unchanged
Audio, privacy/database, calibration, voice-goal/clinical-adjacent, and informational sections remain disabled and
display-only (verified by `--settings-smoke` + `--settings-visual-parity-smoke`, both still green — `SettingsViewModel`
keeps a parameterless ctor, no command properties, and is not IDisposable; the Save command lives on the separate
`UiPreferencesViewModel`).

## Smoke coverage
- **New `--settings-preferences-persistence-smoke`** (31st): round-trips the store via a TEMP path — defaults when
  no file, save writes the file, reload is exact, **corrupt file → safe defaults (no throw)**, unknown language
  normalises to default, and the default path is Avalonia-local (`FemVoiceAvalonia/ui-preferences.json`, not a DB/WPF file).
- **Updated `--settings-persistence-readiness-smoke`** (post-Stage-1 guardrail): behaviour-heavy sections still
  inert; `SettingsViewModel` not IDisposable; the Settings VM/view **and** the 3 preference files reference NO
  WPF/DB/clinical hooks (DB user-settings, `ThemeManager`, `SetLanguage`, backup, mic calibration) and perform **no
  runtime activation** (no `RequestedThemeVariant`/`ThemeVariant`/`Application.Current`/`Thread.CurrentThread`/
  `CurrentUICulture`/`CurrentCulture =`). Avalonia-local file persistence is allowed. (Detection uses non-forbidden
  substrings so it doesn't trip the leak guard.)

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
leak guard clean (no `IDatabaseService`/`ThemeManager`/`MicrophoneCalibration`/`SessionAnalyticsStore`/engine refs;
no WPF localization dependency); **Core `Strings.*.resx` untouched** (git-clean); portable **1570/1580** (baseline
unperturbed). The 7 new `Settings_LocalPrefs_*` strings were registered in `ScaffoldStrings.NativeTranslationBacklog`
(coverage smoke green) — Norwegian display text, native translation deferred like the other scaffold keys.

## Explicitly NOT done (boundaries respected)
No WPF `UserSettings`/SQLite/`IDatabaseService`; no WPF `ThemeManager`/`LocalizationService`/`SetLanguage`; **no
runtime theme/language activation** (Stage 2); no audio/mic, backup/restore, privacy export/delete, clinical/domain,
SmartCoach/Progression, or WPF behaviour change; no startup settings read (lazy load).

> The repository is private/proprietary; no open-source license assumed.
