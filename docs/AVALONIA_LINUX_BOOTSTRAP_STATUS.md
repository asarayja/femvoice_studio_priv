# Avalonia Linux Bootstrap — Status (Phase L6)

Date: 2026-06-16 · Project: `FemVoice.Avalonia` (net10.0, Avalonia 11.2.1).

## What exists (builds on Linux, ✅)
- `App.axaml` / `App.axaml.cs` — Avalonia `Application` with FluentTheme; sets `MainWindow` on classic-desktop lifetime.
- `MainWindow.axaml` / `.cs` — minimal placeholder window stating the parity status; sets a status line confirming shared services resolved.
- `Program.cs` — composition root (`Microsoft.Extensions.DependencyInjection`) + `--smoke` headless verification path.
- `Platform/AvaloniaPlatformServices.cs` — Avalonia implementations of `IUiDispatcher` (real, via `Dispatcher.UIThread`) and **placeholder** `IDialogService`, `IFileDialogService`, `ISystemThemeProvider`.

## What is wired through DI (verified by `--smoke`)
- `ILocalizationService` → `LocalizationService.Instance` (Core) — localization works (`Common_Yes → "Ja"`).
- `IUiDispatcher` → `AvaloniaUiDispatcher`.
- `IDialogService` / `IFileDialogService` / `ISystemThemeProvider` → Avalonia placeholders.
- `IAudioCaptureService` → `NoopAudioCaptureService` (synthetic available via `SyntheticAudioCaptureService`).
- Shared clinical/scoring/coach/health/report/diagnostics types from `FemVoice.Core` resolve.

## Verification
- `dotnet build FemVoice.Avalonia` → 0 errors.
- `dotnet run --project FemVoice.Avalonia -- --smoke` → prints the resolution report and exits 0 (no display required). This satisfies "Avalonia app builds and starts using shared services" in a **headless-verifiable** way. A full windowed launch needs a display/X server, which this CI host lacks; not attempted.

## NOT done yet (explicitly out of scope for this phase)
This is a **bootstrap shell only** — it does NOT pretend feature parity. Missing screens / work (later phases, see `docs/AVALONIA_MIGRATION_TRACKER.md`):
- All product views: Main dashboard, pitch chart (OxyPlot.Avalonia), exercise guide/detail, SmartCoach, progression, analysis/resonance, reports, clinician/coach dashboards, settings, calendar/statistics, first-time setup.
- Theme port (Light/Dark ResourceDictionary → Avalonia styles; `pack://` → `avares://`; system-theme via PlatformSettings).
- Localization XAML markup (`LocExtension`/`LocConverter` → Avalonia equivalents).
- Real `IDialogService` (MessageBox-equivalent), real `IFileDialogService` (Avalonia `IStorageProvider`), real `ISystemThemeProvider`, `IThemeResourceProvider`.
- Charts (`OxyPlot.Wpf` → `OxyPlot.Avalonia`).
- Windows NAudio capture behind `IAudioCaptureService` (`FemVoice.Audio.Windows`), and eventual cross-platform capture.
- View-models: the WPF VMs are not ported; shared, UI-free VM logic should be extracted and the dispatcher/brush coupling routed through the abstractions before reuse.

## Known issue
- Transitive NuGet advisory `NU1903` on `Tmds.DBus.Protocol` 0.20.0 (Avalonia Linux backend). Resolve via an Avalonia version bump later.
