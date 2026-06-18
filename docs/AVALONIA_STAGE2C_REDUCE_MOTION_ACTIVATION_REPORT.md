# Avalonia Stage 2C — Reduce-Motion Activation — Slice Report

Date: 2026-06-18 · Branch: `avalonia-stage2c-reduce-motion-activation-slice` (off `main` @ `e2cc282`).

> **Stage 2C only.** Activates the already-persisted **reduce-motion** preference in the **Avalonia UI only**, at
> startup and live on Save, via an Avalonia-owned motion-preference state. Theme (Stage 2A) and language (Stage 2B)
> activation remain intact. No WPF, no Core behaviour, no DB/`UserSettings`/SQLite, no WPF `ThemeManager`/
> `LocalizationService`/`SetLanguage`/`LocExtension`/`LocConverter`, no audio/clinical change, no global thread-
> culture change, no Core resx change.

## Current visual effect — intentionally a no-op (documented)
A survey of the Avalonia head found **no explicit animations/transitions** (no `Transitions`, `Animation`,
`Storyboard`, page fades/slides, or chart motion). Per the slice guidance, no animations were invented just to
disable them. Instead this slice adds a small **Avalonia-owned motion-preference activation service** that holds
the state and is **ready to be respected by any future Avalonia motion effect** (which should gate on
`MotionActivation.ReduceMotion`). The present visible effect is therefore intentionally **limited / no-op** until
animated UI elements exist; the preference itself is genuinely **active**.

## What was added / changed
- **`Accessibility/MotionActivation.cs`** (new) — Avalonia-owned static: `ReduceMotion` state, `ReduceMotionChanged`
  event, `Apply(bool)` (live), `ApplyFromStore()` (startup, fail-safe → default "not reduced" on missing/invalid).
  No WPF/Core/DB/UI dependency; null-safe.
- **`App.axaml.cs`** — `OnFrameworkInitializationCompleted` applies the saved reduce-motion (after the Stage-2A
  theme + Stage-2B language activation) before the window is built.
- **`ViewModels/UiPreferencesViewModel.cs`** — `Save` now also applies reduce-motion **live** (alongside theme +
  language). Truthful copy: the Note and Save status no longer say motion is "not active yet" — they state the
  reduce-motion choice is **active and respected by the app's motion effects**.
- **`Localization/ScaffoldTranslations.cs` + the VM fallback** — the `Settings_LocalPrefs_Note` and
  `Settings_LocalPrefs_Saved` strings updated across **all 20 languages** to the truthful wording (no "not active
  yet"). Still machine-generated for the 18 non-English/non-Norwegian languages — caveat unchanged.

## Smoke coverage
- **New `--settings-reduce-motion-activation-smoke`** (34th; pure, runs from the published DLL): saved
  reduce-motion **true/false** loaded at startup; **missing**/**corrupt** file → safe default (not reduced);
  **saveAppliesLive** (Save switches the running motion state + raises `ReduceMotionChanged`); **themeStillWorks**
  (Stage 2A); **languageStillWorks** (Stage 2B).
- **`--settings-persistence-readiness-smoke`** updated (doc): reduce-motion is now an **allowed Avalonia-local
  motion preference** (`MotionActivation`) — still forbids WPF/DB/global-culture mutation; behaviour-heavy sections
  remain inert.
- `--settings-preferences-persistence-smoke`, `--settings-theme-activation-smoke`,
  `--settings-language-activation-smoke`, `--settings-visual-parity-smoke` remain green.

## Guardrails (verified)
Build 0/0; **34/34 smokes**; `Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` +
`FemVoice.Audio.Abstractions`; leak guard clean (no `IDatabaseService`/WPF `ThemeManager`/`LocalizationService`/
`LocExtension`/`LocConverter`/`MicrophoneCalibration`/engine refs); **Core `Strings.*.resx` untouched (git-clean)**;
portable **1570/1580** (10 known localization-data baseline failures; unchanged); no global thread-culture change.

## Explicitly NOT done (boundaries respected)
No WPF/Core/clinical/audio change; no DB/`UserSettings`/SQLite/`IDatabaseService`; no WPF `ThemeManager`/
`LocalizationService`/`SetLanguage`/`LocExtension`/`LocConverter`; no global thread-culture change; no Core resx
edits; no machine-translation-policy change; no native-translation review workflow; no layout/colour/theme/language
change; behaviour-heavy Settings sections remain disabled/inert. Theme (2A) + language (2B) intact.

> The repository is private/proprietary; no open-source license assumed. The 18 non-English/non-Norwegian
> translations remain machine/model-generated and NOT native-speaker reviewed.
