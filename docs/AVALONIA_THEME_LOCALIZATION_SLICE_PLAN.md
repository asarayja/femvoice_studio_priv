# Avalonia Theme + Localization Adapter Parity — Slice Plan

Date: 2026-06-16 · Branch: `avalonia-theme-localization-slice` (off `main` @ `fe3b43c`, incl. PR #1–#9).

> **Status: IMPLEMENTED (Linux-verified, headless).** Display-only UI-infrastructure slice. No clinical/domain
> behaviour changed · no WPF behaviour changed · no Android/iOS · no real mic · no persistence · no
> SmartCoach/progression · no safety-gate enforcement · localization semantics preserved · theme changes are
> Avalonia-only. See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Give the Avalonia head a safe theme + localization foundation before more UI surfaces are added: named theme
brushes (Dark/Light) for the shell chrome instead of scattered hardcoded hex, and a safe read-only
localization adapter so shell/nav/status/deferred labels resolve through the shared `LocalizationService`
without changing any resource key, culture, or localization semantics.

## 2. Theme foundation
- **New** `FemVoice.Avalonia/Themes/ShellTheme.axaml` — a `ResourceDictionary` with `ThemeDictionaries`
  (`Dark` + `Light`) defining 14 named shell brushes (`ShellHeaderBackgroundBrush`, `ShellPanelBackgroundBrush`,
  `ShellBorderBrush`, `ShellAccentBrush`, `ShellHeadingBrush`, `ShellMutedBrush`, `ShellFaintBrush`,
  `ShellSubtleTextBrush`, `ShellBodyTextBrush`, `ShellOkBrush`, `ShellOkBorderBrush`, `ShellDeferredTitleBrush`,
  `ShellDeferredBorderBrush`, `ShellStatusBackgroundBrush`). Dark = the current appearance (preserved);
  Light = sensible equivalents for system light theme.
- **`App.axaml`** merges `ShellTheme.axaml` into `Application.Resources`. FluentTheme retained;
  `RequestedThemeVariant="Default"` (follows system).
- **`MainWindow.axaml`** replaces the shell's hardcoded hex with `{DynamicResource Shell*Brush}` so the chrome
  follows the active theme variant. (Inner exercise/dashboard views are out of scope for this slice.)

## 3. Localization adapter (read-only; semantics preserved)
- **New** `FemVoice.Avalonia/Localization/Localized.cs` — `Get(key, fallback)` reads
  `LocalizationService.Instance[key]`; the indexer returns the key itself when missing, so a missing key (or
  empty value) maps to the readable fallback. Never calls `SetLanguage`; writes nothing.
- **New** `FemVoice.Avalonia/Localization/LocalizedValue.cs` — a reactive `INotifyPropertyChanged` holder that
  subscribes to the service's `PropertyChanged` ("Item[]"/"CurrentCulture") and re-raises `Value`, so bound
  labels update if the language ever changes (no semantic change).
- **New** `FemVoice.Avalonia/Localization/TrExtension.cs` — an Avalonia markup extension `{loc:Tr Key=…,
  Fallback=…}` returning a one-way `Binding` to a `LocalizedValue`. Not a WPF localization-markup port; no WPF
  dependency.
- **`MainWindow.axaml`** static labels (header subtitle, nav heading/hint, info heading, display-only mode/detail,
  deferred footnote) use `{loc:Tr Key=Shell_*, Fallback='…current text…'}`.
- **`ShellViewModel`** routes nav labels + `MicStatusText`/`ModeText` through `Localized.Get`; **`DeferredSurfaceViewModel`**
  routes its title/message through it. All keys are namespaced `Shell_*`; since they are not in the RESX yet,
  every label falls back to the current Norwegian text → behaviour identical today, but the path is localization-ready.

## 4. Why this is safe / semantics preserved
No resource key, RESX file, culture, or `SetLanguage` behaviour is touched. The adapter only *reads* the
existing indexer and falls back to the current literal. The WPF localization markup (LocExtension/LocConverter)
is NOT ported or referenced. Theme changes are Avalonia resource dictionaries only.

## 5. Deferred / not in this slice
Full 176-key WPF theme palette parity (only the shell chrome is themed); theming the inner exercise/dashboard
views; populating `Shell_*` RESX keys (kept as fallbacks); runtime language-switch UI (Settings is deferred);
real Settings/Analysis/Reports surfaces; packaging; mobile.

## 6. Smoke (`--theme-loc-smoke`)
Headless: `Localized.Get` resolves a known key (`Common_Yes`→"Ja") and falls back on a missing key;
`LocalizedValue` + `TrExtension` resolve/fall back (the markup pattern); shell/nav/status/deferred labels
resolve or fall back; and a **guarded** runtime check (when an Avalonia platform is available) confirms all 14
shell brushes resolve in both Dark and Light variants. No `SetLanguage` is called.

## 7. Gate
`dotnet build` (0 warnings) · all 9 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580; 1569 acceptable due to the known ComfortZone flake) · leak guard clean · Windows CI via PR.
