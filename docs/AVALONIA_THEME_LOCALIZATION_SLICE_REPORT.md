# Avalonia Theme + Localization Adapter Parity — Slice Report

Date: 2026-06-16 · Branch: `avalonia-theme-localization-slice` (off `main` @ `fe3b43c`).

> **Display-only UI-infrastructure slice.** No clinical/domain behaviour changed · no WPF behaviour changed ·
> no Android/iOS started · no real mic · no persistence · no SmartCoach/progression · no safety-gate
> enforcement · **localization semantics preserved** · theme changes are Avalonia-only.

## 1. What this slice does
Adds a safe theme + localization foundation to the Avalonia head: named Dark/Light shell theme brushes
(replacing scattered hardcoded hex in the shell), and a read-only localization adapter (resolver + reactive
backing + `{loc:Tr}` markup extension) so shell/nav/status/deferred labels resolve through the shared
`LocalizationService` with a readable fallback. Behaviour and on-screen text are identical today (keys fall
back to the current Norwegian literals); the app is now localization- and theme-variant-ready.

## 2. Files changed
- **New** `FemVoice.Avalonia/Themes/ShellTheme.axaml` — `ResourceDictionary` with `ThemeDictionaries`
  (Dark/Light), 14 named shell brushes.
- **New** `FemVoice.Avalonia/Localization/Localized.cs` — `Get(key, fallback)` (read-only resolver; missing→fallback).
- **New** `FemVoice.Avalonia/Localization/LocalizedValue.cs` — reactive holder (subscribes to "Item[]"/"CurrentCulture").
- **New** `FemVoice.Avalonia/Localization/TrExtension.cs` — `{loc:Tr Key=…, Fallback=…}` → one-way `Binding`.
- **Edit** `FemVoice.Avalonia/App.axaml` — merge `ShellTheme.axaml` into `Application.Resources` (FluentTheme kept).
- **Edit** `FemVoice.Avalonia/MainWindow.axaml` — `{DynamicResource Shell*Brush}` for shell colours; `{loc:Tr}` for
  static labels (single-quoted fallbacks).
- **Edit** `FemVoice.Avalonia/ViewModels/ShellViewModel.cs` — nav labels + `MicStatusText`/`ModeText` via `Localized.Get`.
- **Edit** `FemVoice.Avalonia/ViewModels/DeferredSurfaceViewModel.cs` — title/message via `Localized.Get`.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--theme-loc-smoke`.
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/` (read-only use of `LocalizationService`), or `FemVoice.Audio.Windows/`.

## 3. Localization semantics preserved
- The adapter only **reads** `LocalizationService.Instance[key]` (the existing Core API) and never calls
  `SetLanguage` or writes anything. The indexer's missing-key behaviour (returns the key) is mapped to a
  readable fallback (= the current literal), so no visible text changes today.
- No RESX file, resource key, or culture is altered. The WPF localization markup (LocExtension/LocConverter)
  is **not** ported or referenced.

## 4. Theme: Avalonia-only
A new Avalonia `ResourceDictionary` with `ThemeDictionaries` provides shell brushes per variant; `MainWindow`
binds them via `DynamicResource`. No WPF theme-manager dependency, no WPF brushes. Dark = the current look;
Light = prepared equivalents (not yet user-switchable — Settings is deferred). Inner views untouched.

## 5. Display-only limitations
Only the shell chrome is themed (not the inner exercise/dashboard views). `Shell_*` RESX keys are not populated
(fallbacks shown). No runtime language-switch UI. No packaging/RIDs. Full WPF theme parity is a later slice.

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 9 smokes OK (incl. `--theme-loc-smoke`; guarded runtime check confirmed all 14 shell
brushes in Dark+Light) · no vulnerable packages · leak guard clean (zero real references) · refs only
Core + Audio.Abstractions · Tmds 0.21.3 · portable 1569–1570/1580 (known ComfortZone flake) · Windows CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched. Localization semantics preserved.** All additions are
display-only Avalonia UI infrastructure.
