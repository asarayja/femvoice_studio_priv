# Avalonia Desktop Shell + Navigation/Layout Parity — Slice Report

Date: 2026-06-16 · Branch: `avalonia-shell-navigation-layout-slice` (off `main` @ `47e9a72`).

> **Display-only shell/layout slice.** No clinical/domain behaviour changed · no WPF behaviour changed · no
> Android/iOS started · no real mic · no persistence · no SmartCoach/progression · no safety-gate enforcement ·
> deferred placeholders are static-only (non-functional).

## 1. What this slice does
Turns the minimal Avalonia shell into a desktop-friendly, cross-platform-safe shell: real window chrome,
a header, a left navigation rail (implemented destinations + deferred placeholders), a central content area,
a static right info sidebar, and a display-only bottom status strip — preserving the existing navigation
lifecycle safety. The four existing destinations (Dashboard, Exercise Guide, Detail, Runtime) keep working.

## 2. Files changed
- **New** `FemVoice.Avalonia/ViewModels/DeferredSurfaceViewModel.cs` — static, display-only "deferred" page
  (title + message; no services; not `IDisposable`; no side effects).
- **Edit** `FemVoice.Avalonia/ViewModels/ShellViewModel.cs` — `ShellNavItem` model; `NavItems` (2 implemented +
  7 deferred); `ShowDeferred` (opens the static placeholder); display-only `MicStatusText`/`ModeText`/
  `CurrentDestinationLabel`; preserved the transient-page disposal in `OnCurrentPageChanging`.
- **Edit** `FemVoice.Avalonia/MainWindow.axaml` — window min-size + startup placement; header; left nav rail
  (`ItemsControl` over `NavItems`); content `ContentControl` (+ inline `DeferredSurfaceViewModel` template);
  static right info sidebar; bottom status strip. Converter-free.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--shell-smoke`.
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/`, or `FemVoice.Audio.Windows/` were touched.

## 3. Navigation surface
| Destination | Kind | Behaviour |
| --- | --- | --- |
| Dashbord | Implemented | `ShowDashboardCommand` → `MainDashboardViewModel` (singleton) |
| Øvelsesguide | Implemented | `ShowGuideCommand` → `ExerciseGuideViewModel` (singleton; opens Detail → Runtime) |
| Innstillinger, Analyse, Rapporter, Diagnostikk, Progresjon, SmartCoach, Mikrofonkalibrering | Deferred | open a static `DeferredSurfaceViewModel` — no services, no side effects |

Deferred items are reachable but inert (the prompt's allowed "static deferred page with no side effects"
option). They are NOT wired to any real service and create no clinical/persistence behaviour.

## 4. Lifecycle safety
`OnCurrentPageChanging` disposes transient disposable outgoing pages (the runtime VM) and never disposes the
retained `_dashboard`/`_guide` singletons or the inert deferred placeholders. `--shell-smoke` verifies:
runtime runs → nav-away disposes it (`IsRunning=false`) → re-open yields a fresh distinct running instance
while the first stays stopped (no orphaned synthetic capture, no duplicate runtime/subscription).

## 5. Display-only limitations
The status strip and info sidebar show static text (synthetic mode); the mic/signal status is a placeholder
(no real capture). Deferred surfaces are static placeholders only. No theme-resource/localization adapter
(separate slice); strings remain inline Norwegian; no packaging/RIDs added.

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 8 smokes OK (incl. `--shell-smoke`) · no vulnerable packages · leak guard clean (zero
real references) · Avalonia refs only Core + Audio.Abstractions · Tmds 0.21.3 · portable 1570/1580 · Windows
CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched.** All additions are display-only shell/layout.
