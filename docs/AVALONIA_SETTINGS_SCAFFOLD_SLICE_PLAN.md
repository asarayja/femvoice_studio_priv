# Avalonia Settings / Preferences UI Scaffold — Slice Plan

Date: 2026-06-16 · Branch: `avalonia-settings-scaffold-slice` (off `main` @ `49dcb0e`, incl. PR #1–#10).

> **Status: IMPLEMENTED (Linux-verified, headless).** Display-only settings scaffold. No clinical/domain
> behaviour changed · no WPF behaviour changed · no Android/iOS · no real mic · no persistence · no settings
> writes · no SetLanguage/culture changes · no theme-switching side effects · no SmartCoach/progression · no
> safety-gate enforcement · **all settings controls are disabled/deferred/inert.** See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Represent the WPF Settings/Preferences surface in Avalonia as a reachable, **display-only** scaffold built on the
existing shell + theme + localization foundations, with every action deferred/inert. Settings becomes an
implemented nav destination; all other missing surfaces stay deferred placeholders.

## 2. Scope (implemented)
- **New** `ViewModels/SettingsViewModel.cs` — a purely static page (no services, no commands, not IDisposable,
  no timers/subscriptions/capture). Builds 8 cards via the read-only `Localized.Get` adapter using the real
  WPF `Settings_*`/`Privacy_*` keys where available (fallback otherwise). `SettingsSection` (title/description/rows)
  + `SettingsRow` (label/value; `IsEnabled => false`). `AllControlsDeferred => true`.
- **New** `Views/SettingsView.axaml` (+ `.axaml.cs`) — cards via nested `ItemsControl`; each row shows a label +
  a "Utsatt" status + a **disabled** Button (`IsEnabled="{Binding IsEnabled}"` = false, no command). Shell theme
  brushes via `{DynamicResource}`; no value converters.
- **Edit** `ViewModels/ShellViewModel.cs` — retained inert `_settings` singleton; `ShowSettings` command; the
  "Innstillinger" nav item is now **implemented** (was a deferred placeholder); destination label + disposal
  guard updated (Settings is never disposed; it is not IDisposable anyway).
- **Edit** `MainWindow.axaml` — `DataTemplate` for `SettingsViewModel`.
- **Edit** `Program.cs` — `--settings-smoke`; updated `--shell-smoke` (implemented==3) and `--theme-loc-smoke`
  (nav[2]=="Innstillinger") for the now-implemented Settings nav.

## 3. The 8 cards (display-only)
General · Appearance/theme · Language · Audio input · Exercise preferences (voice goal) · Data / backup ·
Privacy / diagnostics · About. Each card: title + description + inert rows, every action shown as "Utsatt — kommer
senere" with a disabled control. A top banner states the whole page is display-only.

## 4. Safety / what is NOT done
No `SetLanguage`/culture change; no theme-variant persistence/switch; no voice-goal/profile write; no database
clear; no backup/restore; no microphone calibration; no real audio; no SQLite/IDatabaseService/recorder; no
SmartCoach/progression/safety-gate. The page is inert and disposable-safe (nothing to dispose). All labels are
read-only `Localized.Get` lookups (no semantic change).

## 5. Lifecycle
Settings is a retained singleton (like dashboard/guide), not IDisposable, starting no work. The shell's
transient-page disposal is preserved and exercised: navigating to Settings from a running runtime disposes the
runtime (stops synthetic capture; no orphaned frames; no duplicate runtime).

## 6. Smoke (`--settings-smoke`)
Headless: Settings nav item exists and is implemented; navigating switches `CurrentPage` to `SettingsViewModel`;
the VM is inert (not IDisposable via reflection; exposes no `IRelayCommand`; all rows `IsEnabled==false`;
`AllControlsDeferred`); 8 cards present with rows; and navigating to Settings from a running runtime disposes it
(`IsRunning==false`, trace stops growing → no orphaned capture).

## 7. Gate
`dotnet build` (0 warnings) · all 10 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580; 1569 acceptable due to the known ComfortZone flake) · leak guard clean (base +
settings-specific: no SQLite/IDatabaseService/SetLanguage/Save/Persist/Backup/Restore/MicrophoneCalibration/
ProgressionSafetyGate/SmartCoachEngine) · Windows CI via PR.
