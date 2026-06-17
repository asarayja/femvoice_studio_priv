# Settings Visual Parity Scaffold — Slice Report

Date: 2026-06-17 · Branch: `avalonia-settings-visual-parity-scaffold-slice` (off `main` @ `5a13577`).

> **UI parity scaffold only — display-only, non-persistent, clearly deferred.** No settings save/apply/reset, no
> theme/language switch, no audio-device selection, no database backup/restore/clear, no privacy export/delete, no
> diagnostics export, no persistence/analytics/clinical behaviour. WPF untouched.

## WPF source-of-truth conclusion
Inspected `FemVoiceStudio/Views/SettingsWindow.xaml`. Sections + control types:
- **Language** — `ComboBox` (20 languages, nb-NO … ar).
- **Theme** — `RadioButton`s (System / Light / Dark).
- **Audio** — `CheckBox` "hear own voice" + `Button` "open mic calibration".
- **Voice goal** — two `ComboBox`es (focus: balanced/resonance/intonation/breathing/pitch; style:
  soft_feminine/bright_neutral/androgynous/custom).
- **Data / backup** — `Button`s: clear database / create backup / restore backup.
- **Accessibility** — `CheckBox` (e.g. stress-sensitive mode).
- Plus **Privacy/diagnostics** consent and **About/version** info.
All bind to real services/persistent settings (`ISettingsService`, culture switch, DB, privacy) → reproduced as
**visual structure with DISABLED controls** only; none of the real behaviour is ported.

## Avalonia changes (display-only)
The Settings page already had 8 inert sections (label + status + generic "…" button). Enriched to better resemble WPF:
- **`SettingsViewModel`**: each `SettingsRow` now carries a `SettingsControlKind` (`Info` / `Toggle` / `Combo` /
  `Button`) + `ControlText` + converter-free `IsInfo/IsToggle/IsCombo/IsButton` switches and a `ShowDeferredChip` /
  `DeferredChip` ("Utsatt"). Added a 9th **Accessibility** section (WPF parity). Added `DeferredBadge` +
  `SafetyNote`. Rows assigned the WPF control kind: Theme/Language/Voice-goal/Mic → **disabled combo**; Hear-own-
  voice/Accessibility/Privacy → **disabled toggle (checkbox)**; First-run/Mic-calibration/Backup/Restore/Clear →
  **disabled button**; About → info. **No commands, no services, parameterless ctor, not IDisposable** (all preserved).
- **`SettingsView.axaml`**: header + "Utsatt · kun visning" badge; deferred banner; one card per section; each row
  renders the matching **disabled** control (`ComboBox`/`CheckBox`/`Button`, all `IsEnabled=False`, bound to
  nothing) plus an "Utsatt" chip; a closing safety note. Dark baseline via `Shell*` brushes; no converters.

## Disabled / deferred behavior
Every interactive control is `IsEnabled=False` and inert (no command, no binding target). All actionable rows show
an "Utsatt" chip; the page header shows "Utsatt · kun visning"; banner + safety note state nothing is saved and no
theme/language/profile/backup behaviour runs. Navigating to/from Settings has no side effects (the runtime is still
disposed correctly on nav-away — covered by `--settings-smoke`).

## Smoke
New `--settings-visual-parity-smoke` (26th): navigation opens the inert `SettingsViewModel` (no services —
parameterless ctor, no `IRelayCommand`, not IDisposable); **9** non-empty section cards; every row inert
(`IsEnabled=false`); representative disabled controls present (≥1 combo, ≥1 toggle, ≥1 button); actionable rows
carry an "Utsatt" chip; deferred wording present; shell sidebar (9 items, 6 implemented) intact. `--settings-smoke`
section count updated 8 → 9 (and stays green: inert / no-commands / not-IDisposable / runtime-dispose-on-nav).

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
leak guard clean — no `IDatabaseService`/`SessionAnalyticsStore`/`ExerciseSessionRecorder`/`ThemeManager`/`MicrophoneCalibration`/
audio/clinical references; no settings persistence, no theme/language/audio/database/privacy/backup behaviour; no
clinical/domain or WPF behaviour change; no runtime platform implementation. Build 0/0 also proves no real settings
service is referenced (they are not in the referenced assemblies).

> The repository is private/proprietary; no open-source license assumed.
