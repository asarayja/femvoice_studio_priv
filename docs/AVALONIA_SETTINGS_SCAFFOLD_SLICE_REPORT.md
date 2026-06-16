# Avalonia Settings / Preferences UI Scaffold — Slice Report

Date: 2026-06-16 · Branch: `avalonia-settings-scaffold-slice` (off `main` @ `49dcb0e`).

> **Display-only settings scaffold.** No clinical/domain behaviour changed · no WPF behaviour changed · no
> Android/iOS started · no real mic · no persistence · no settings writes · no SetLanguage/culture changes ·
> no theme-switching side effects · no SmartCoach/progression · no safety-gate enforcement · **all settings
> controls are disabled/deferred/inert.**

## 1. What this slice does
Adds a reachable, display-only Settings page to the Avalonia shell. Settings is now an implemented nav
destination; the other missing WPF surfaces remain deferred placeholders. The page presents 8 cards mirroring
the WPF Settings/Preferences information architecture, with every action shown as deferred and inert.

## 2. Files changed
- **New** `FemVoice.Avalonia/ViewModels/SettingsViewModel.cs` — static page (no services/commands; not IDisposable);
  `SettingsSection` + `SettingsRow` (`IsEnabled => false`); `AllControlsDeferred => true`; 8 cards via `Localized.Get`.
- **New** `FemVoice.Avalonia/Views/SettingsView.axaml` (+ `.axaml.cs`) — converter-free cards (nested ItemsControl);
  each row = label + "Utsatt" status + a disabled Button (`IsEnabled="{Binding IsEnabled}"`=false, no command); shell theme brushes.
- **Edit** `FemVoice.Avalonia/ViewModels/ShellViewModel.cs` — inert `_settings` singleton + `ShowSettings`; Settings nav
  item implemented; destination label + disposal guard updated.
- **Edit** `FemVoice.Avalonia/MainWindow.axaml` — `DataTemplate` for `SettingsViewModel`.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--settings-smoke`; updated `--shell-smoke` / `--theme-loc-smoke` assertions
  for the now-implemented Settings nav.
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/` (read-only `Localized.Get` over `LocalizationService`), or `FemVoice.Audio.Windows/`.

## 3. Inertness (verified)
`SettingsViewModel` holds no services, exposes no commands (no `IRelayCommand`), is not `IDisposable`, and starts
no timers/subscriptions/capture/background work. Every `SettingsRow.IsEnabled` is `false`; the one interactive
control per row (a Button) binds `IsEnabled` to that false flag and has no command. `--settings-smoke` asserts
all of this behaviorally (incl. reflection checks for no-IDisposable / no-IRelayCommand).

## 4. Display-only / no forbidden behaviour
No `SetLanguage`/culture change, no theme-variant persistence/switch, no voice-goal/profile write, no database
clear, no backup/restore, no microphone calibration, no real audio, no SQLite/IDatabaseService/recorder, no
SmartCoach/progression/safety-gate. Labels resolve read-only via `Localized.Get` (real `Settings_*`/`Privacy_*`
keys where available; fallback otherwise) — localization semantics preserved.

## 5. Lifecycle safety
Settings is a retained singleton (not disposed; not IDisposable). The shell's transient-page disposal is
preserved: navigating to Settings from a running runtime disposes the runtime (stops synthetic capture; trace
stops growing → no orphaned capture; no duplicate runtime). Verified by `--settings-smoke`.

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 10 smokes OK (incl. `--settings-smoke`) · no vulnerable packages · leak guard clean (base
+ settings-specific) · refs only Core + Audio.Abstractions · Tmds 0.21.3 · portable 1570/1580 (1569 known flake) ·
Windows CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched. Localization semantics preserved.** All additions are
display-only Settings scaffold.
