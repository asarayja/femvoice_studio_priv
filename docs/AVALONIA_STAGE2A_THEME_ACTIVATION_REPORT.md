# Avalonia Stage 2A — Runtime Theme Activation — Slice Report

Date: 2026-06-17 · Branch: `avalonia-stage2a-theme-activation-slice` (off `main` @ `3a8bfbf`).

> **Stage 2A only.** Activates the saved **theme** preference at runtime (Avalonia-only) — at startup and when the
> user saves a new theme in the Stage-1 Settings card. **Language remains persisted-only and is NOT runtime-
> activated. Reduce-motion remains persisted-only and is NOT runtime-activated.** No WPF `ThemeManager`, no WPF
> `LocalizationService`/`SetLanguage`, no DB/`UserSettings`/SQLite, no audio/clinical/SmartCoach/Progression/
> privacy/backup/report/export/Core/WPF behaviour change.

## What was added
- **`Theming/ThemeActivation.cs`** — the ONLY runtime theme-activation point. `ToVariant(ThemePreference)` maps
  System→`ThemeVariant.Default`, Light→`ThemeVariant.Light`, Dark→`ThemeVariant.Dark`. `Apply(theme)` sets
  `Application.Current.RequestedThemeVariant` (null-safe — no-op without a running Application; FluentTheme honours
  it live, no restart). `ApplyFromStore(store)` applies a theme **only if a valid saved preference exists**;
  otherwise it leaves the existing default (dark) baseline untouched. Avalonia-only; no culture/language change, no
  reduce-motion effect, no DB/Core/WPF.
- **`Preferences/UiPreferencesStore.cs`** — added `TryLoad(out prefs)` returning `true` only for a valid existing
  file (false on missing/empty/invalid/**corrupt**, never throws). `Load()` now delegates to it. Stage 2A uses
  `TryLoad` so activation applies only a real user-saved preference and otherwise **preserves the dark baseline**.
- **`App.axaml.cs`** — `OnFrameworkInitializationCompleted` calls `ThemeActivation.ApplyFromStore()` **before** the
  window is created, so a saved theme is in effect from first paint. Fail-safe; theme only.
- **`ViewModels/UiPreferencesViewModel.cs`** — `Save` now persists **and** live-applies the theme via
  `ThemeActivation.Apply(Theme)` (null-safe). Language and reduce-motion are saved but **not** activated; the status
  text says so ("Tema er aktivert; språk/bevegelse lagres men aktiveres ikke ennå").

## Dark-baseline guarantee
- No saved preference / invalid / corrupt file → `ApplyFromStore` is a no-op → the `App.axaml`
  `RequestedThemeVariant="Dark"` baseline stands. Verified: `--visual-baseline-smoke` with no pref → `actualVariant='Dark'`.
- A valid saved preference (e.g. Light) → applied at startup. Verified: with a saved Light pref →
  `actualVariant='Light'`. `--visual-baseline-smoke` was made Stage-2A-aware (accepts the Dark baseline **or** the
  exactly-applied saved-preference variant).

## Smoke coverage
- **New `--settings-theme-activation-smoke`** (32nd; initialises the Avalonia platform headlessly, skips→pass with
  no display): pure `ToVariant` mapping; saved **Dark/Light/System** apply via `ApplyFromStore` (RequestedThemeVariant
  matches); **missing** file → no apply, baseline preserved; **corrupt** file → no apply, baseline preserved;
  applying a theme does **not** change `CurrentUICulture` (language/reduce-motion not runtime-activated).
- **Updated `--settings-persistence-readiness-smoke`** (post-Stage-2A guardrail): behaviour-heavy sections still
  inert; `SettingsViewModel` not IDisposable; the Settings VM/view + preference files + `ThemeActivation` reference
  no WPF/DB/clinical hooks and perform **no language/culture or reduce-motion activation** (theme activation via
  Avalonia `RequestedThemeVariant` in `ThemeActivation` is now allowed).
- `--settings-smoke`, `--settings-visual-parity-smoke`, `--settings-preferences-persistence-smoke`,
  `--avalonia-localization-coverage-smoke` remain green.

## Guardrails (verified)
Build 0/0; **32/32 smokes**; `Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` +
`FemVoice.Audio.Abstractions`; leak guard clean (no `IDatabaseService`/WPF `ThemeManager`/`LocalizationService`/
`MicrophoneCalibration`/engine refs); **Core `Strings.*.resx` untouched (git-clean)**; portable **1570/1580** (10
known localization-data baseline failures; unchanged); packaging 18/18 published + `.deb` + macOS.

## Explicitly NOT done (boundaries respected)
No language runtime activation (Stage 2B); no reduce-motion activation; no WPF `UserSettings`/SQLite/`IDatabaseService`;
no WPF `ThemeManager`/`LocalizationService`/`SetLanguage`; no audio/mic, backup/restore, privacy export/delete,
reports/export, clinical/domain, SmartCoach/Progression, Core, or WPF change.

> The repository is private/proprietary; no open-source license assumed.
