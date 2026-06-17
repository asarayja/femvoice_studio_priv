# Avalonia Settings Persistence — Readiness Audit Report

Date: 2026-06-17 · Branch: `avalonia-settings-persistence-readiness-slice` · Baseline: `main @ 639e9a2`.

> **Audit / design / readiness only — NO persistence implemented, NO Settings control enabled, NO behavior change.**
> The only production change is a guardrail smoke (`--settings-persistence-readiness-smoke`); `SettingsViewModel`,
> `SettingsView.axaml`, Core, and WPF are untouched. This produces the plan + guardrails for a *future approved*
> behavior slice.

## WPF settings behaviour map (source of truth)
`FemVoiceStudio/Views/SettingsWindow.xaml(.cs)` is DI-wired to `DatabaseService`, `ILocalizationService`,
`IVoiceGoalProfileProvider`, `LocalBackupService`, and a `MicrophoneCalibrationWindow`. Persistence stores:

| Area | WPF control | Backing / command | Persistence store | Touches |
| --- | --- | --- | --- | --- |
| Language | ComboBox (20) | `LocalizationService.SetLanguage()` | **text file** (`LoadLanguagePreference`/`SaveLanguagePreference`) + thread culture | runtime culture switch |
| Theme | RadioButtons (System/Light/Dark) | `ThemeManager.SwitchTheme()` / `CurrentThemeMode` | **ThemeManager settings file** + mirrored to DB `UserSettings.Theme` | app theme resources |
| Audio – hear-own-voice | CheckBox | `UserSettings.HearOwnVoice` | **SQLite DB** (`GetUserSettings`/`UpdateUserSettings`) | audio playback path |
| Microphone calibration | Button | opens `MicrophoneCalibrationWindow` → Core `MicrophoneCalibrationService` | DB calibration data | **audio device / platform** |
| Voice-goal (focus/style) | 2 ComboBoxes | `IVoiceGoalProfileProvider` (`SaveVoiceGoalProfile`/`LoadVoiceGoalProfile`) | DB / profile store | **targets/clinical-adjacent** |
| Accessibility | CheckBox(es) (e.g. "Rolig modus") | `UserSettings` flags | SQLite DB | UI behaviour |
| Database clear | Button | `DatabaseService.ResetDatabase()` (confirm dialog) | **SQLite DB (destructive)** | all stored data |
| Backup / restore | Buttons | `LocalBackupService` | **DB backup files** | all stored data |
| Privacy / diagnostics | consent text + toggles | `UserSettings` / export services | DB + export | privacy/data export |
| About / version / license | text | static | none | informational |

**Key risk note:** WPF's `UserSettings` row mixes harmless UI prefs (`Theme`, `HearOwnVoice`) with
**clinical-adjacent fields** — `PreferredMinPitch`/`PreferredMaxPitch` (pitch targets), `CurrentDifficulty`,
`AutoAdvanceLevel`, `ConsistencyScore`, streak/stats. A future Avalonia store must therefore **never** read/write
the WPF `UserSettings` DB row; it must use a separate Avalonia-local store for harmless UI prefs only.

## Avalonia current status (display-only)
`SettingsViewModel` holds **no services, no commands, no IDisposable, parameterless ctor**; every row is inert
(`AllControlsDeferred`); all controls are `IsEnabled=False`, bound to nothing. No persistence, no DB, no
theme/language/audio/file I/O. Confirmed by `--settings-smoke`, `--settings-visual-parity-smoke`, and the new
`--settings-persistence-readiness-smoke` (which also source-checks that the Settings VM/view reference none of the
WPF persistence/behavior hooks).

## Settings readiness matrix
- **A — Safe future UI preferences:** Theme preference, Language preference, simple Accessibility toggle. *(Still
  must NOT be implemented here.)* Persist to a **new Avalonia-local file** (e.g. app-data JSON), **not** the WPF
  DB/ThemeManager.
- **B — Platform/runtime settings:** Microphone/audio device selection, calibration. Requires audio/platform
  abstraction approval (no Linux/macOS audio backend exists yet).
- **C — Data/privacy/database actions:** Backup, restore, clear DB, privacy export/delete. Requires explicit
  database/privacy design approval (`IDatabaseService`/`LocalBackupService` are forbidden in Avalonia today).
- **D — Clinical/domain-adjacent:** Voice-goal focus/style (affects targets/profiles); the `UserSettings` pitch
  targets/difficulty/auto-advance. Requires WPF parity + clinical/safety review.
- **E — Informational only:** About / version / license text. (Already display-only in Avalonia.)

## Future staged implementation proposal (none implemented here)
1. **Stage 1 — harmless UI preference persistence only:** theme + language + simple accessibility prefs, to a NEW
   Avalonia-local file (no DB, no ThemeManager, no WPF `UserSettings`). No runtime activation yet.
2. **Stage 2 — runtime theme/language activation:** wire the saved prefs to the Avalonia theme + the existing
   `ScaffoldStrings`/`Localized` overlay culture, only after a persistence contract + tests are approved.
3. **Stage 3 — platform audio preferences:** only after an audio-device abstraction design is approved.
4. **Stage 4 — data/privacy/database actions:** only after database/privacy design approval.
5. **Stage 5 — voice-goal / clinical-adjacent settings:** only after WPF parity + clinical/safety review.

## Test/smoke plan for future implementation
- Keep `--settings-persistence-readiness-smoke` as a tripwire until Stage 1 is approved; then evolve it to assert
  the Stage-1 store writes ONLY whitelisted UI-pref keys and never touches the DB/`UserSettings`/clinical fields.
- Add a no-write unit test for the readiness state (the VM performs no I/O on construct/navigate).
- Stage 1: round-trip test for the Avalonia-local pref file; assert no DB access; assert clinical fields untouched.
- Stage 2+: explicit approval-gated tests per stage.

## Statements
- **No persistence implemented.** No file/DB/config writes; no startup read of persisted settings; no controls enabled.
- **No WPF/clinical/domain behaviour changes.** Core resx untouched; WPF untouched; no forbidden references.
- Repo is private/proprietary; no open-source license assumed.
