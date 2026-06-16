# Avalonia Desktop Shell + Navigation/Layout Parity — Slice Plan

Date: 2026-06-16 · Branch: `avalonia-shell-navigation-layout-slice` (off `main` @ `47e9a72`, incl. PR #1–#8).

> **Status: IMPLEMENTED (Linux-verified, headless).** Display-only shell/layout slice. No clinical/domain
> change, no WPF change, no Android/iOS, no real mic, no persistence, no SmartCoach/progression, no
> safety-gate enforcement. See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Grow the minimal Avalonia shell (a fixed 980×700 DockPanel with two top nav buttons) into a desktop-friendly,
cross-platform-safe shell closer to the WPF `MainWindow` window-level UX — while keeping every behaviour
display-only. The WPF 14-window modeless model and right-sidebar clinical content are deliberately NOT ported.

## 2. Scope (implemented)
- **Window chrome:** `MainWindow.axaml` now sets `Width=1100 Height=760`, `MinWidth=900 MinHeight=620`,
  `WindowStartupLocation=CenterScreen`, resizable (Avalonia defaults; no WPF `WindowState`/`ResizeMode` types).
- **Layout:** header (top) · status strip (bottom) · 3-column body `[nav rail 220 | content * | info sidebar 260]`.
- **Navigation rail:** `ShellViewModel.NavItems` (a list of `ShellNavItem`) rendered as an `ItemsControl` of
  buttons. Two **implemented** destinations (Dashbord, Øvelsesguide) switch `CurrentPage`; seven **deferred**
  placeholders (Innstillinger, Analyse, Rapporter, Diagnostikk, Progresjon, SmartCoach, Mikrofonkalibrering)
  navigate ONLY to a static `DeferredSurfaceViewModel`.
- **Deferred placeholder page:** `DeferredSurfaceViewModel` is a purely static page (title + message, no
  services, not `IDisposable`, no side effects) shown via an inline `DataTemplate`.
- **Status strip:** display-only `MicStatusText` ("Mikrofon: syntetisk (kun visning)"), `ModeText`
  ("Kun visning · ingen lagring · ingen klinisk endring"), and `CurrentDestinationLabel`.
- **Right info sidebar:** static, display-only "Visning-bare modus" note. No live/clinical content.
- **Smoke:** `--shell-smoke` (behavioral; see §6).

## 3. Lifecycle safety (preserved + exercised)
`ShellViewModel.OnCurrentPageChanging` still disposes a transient, disposable outgoing page (the runtime VM)
while never disposing the retained `_dashboard`/`_guide` singletons or the inert deferred placeholders. This
preserves the PR #7/#8 fix (no orphaned synthetic capture, no duplicate runtime). `--shell-smoke` asserts it.

## 4. Deferred / not in this slice
Real Settings/Analysis/Reports/Diagnostics/Progression/SmartCoach/mic-calibration surfaces (all are clinical,
persisted, or frozen — see the parity audit); theme-resource + localization adapter (separate slice);
responsive collapse of the sidebar; packaging/RIDs; real mic; persistence; Android/iOS.

## 5. Forbidden systems avoided
No `System.Windows`/`Microsoft.Win32`/`MessageBox`/OxyPlot/`FemVoice.Audio.Windows`/NAudio/`NAudioCaptureService`/
`WaveInEvent`/`WasapiCapture`/WPF `ThemeManager`/`LocExtension`/`LocConverter`/`FeedbackConsistencyGuard`/
`ComfortZoneController`. No value converters (deferred items use "— senere" labels, not a converter). Avalonia
references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`; `Tmds.DBus.Protocol` stays pinned 0.21.3.

## 6. Smoke design (`--shell-smoke`)
Headless: shell constructs; lands on the dashboard; `NavItems` = 2 implemented + 7 deferred; implemented nav
switches `CurrentPage`; a deferred nav opens a `DeferredSurfaceViewModel` that is not `IDisposable` (inert);
navigating away from a running runtime via the rail disposes it (`IsRunning=false`); re-opening yields a fresh,
distinct, running instance while the first stays stopped (no orphan / no duplicate).

## 7. Gate
`dotnet build` (0 warnings) · all 8 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580) · leak guard clean · Windows CI green via PR.
