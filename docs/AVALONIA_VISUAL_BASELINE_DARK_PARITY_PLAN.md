# Avalonia Cross-platform Visual Baseline / Dark Theme Parity — Slice Plan

Date: 2026-06-17 · Branch: `avalonia-visual-baseline-dark-parity-slice` (off `main` @ `d04e823`).

> **Visual styling / layout parity only.** No new product behaviour, no real mic, no persistence, no
> clinical/progression/SmartCoach, no signing/notarization, no Android/iOS. WPF untouched. Localization semantics
> unchanged. No theme/settings persistence.

## 1. Goal
Make the Avalonia/Linux UI look intentionally like FemVoice Studio (matching the Windows/WPF **dark-mode**
reference identity) at a safe baseline level, instead of an unstyled light default. Not pixel-perfect; same design
language: dark navy slate background, purple primary accent, green success, red danger, blue secondary, rounded
cards, subtle borders, consistent spacing, white headings, muted gray secondary text, intentionally-disabled
deferred controls.

## 2. Approach
- **Dark-first**: `App.axaml` `RequestedThemeVariant="Dark"` (Avalonia head only; documented). This alone makes
  FluentTheme render dark controls (fixing the "light-gray button wall"); the Light variant is retained for
  completeness so every brush resolves in both variants.
- **Theme foundation** (`Themes/ShellTheme.axaml`): keep the existing 14 `Shell*` brushes (retuned to the navy
  palette) and add surface + semantic + chart/chip keys (28 total, Dark+Light): window/header/status/panel/card
  backgrounds, border, heading/body/subtle/muted/faint text, accent (purple), primary/primaryHover/secondary/
  success/warning/danger, ok/okBorder, deferredTitle/deferredBorder, chart bg/trace/targetBand/marker, chip bg/text.
- **App control styles** (`App.axaml`, layered on FluentTheme): `Button.primary` (purple), `Button.secondary`
  (blue), `Button.nav` (flat rail) + `Button.nav.deferred` (de-emphasised), `Border.card` (elevated surface),
  `Border.chip`. Purely cosmetic — no triggers that change behaviour, no commands.
- **Views**: replace hardcoded hex with theme brushes; apply the `.card` surface; promote Start/primary actions to
  `Button.primary`; add purple icon badges (Exercise Guide/Detail). Older views (Dashboard, Exercise Guide/Detail,
  Runtime) had hardcoded greys; scaffold views (Settings/Analysis/Reports/Diagnostics) already used brushes and
  were upgraded to the `.card` style. All bindings and Norwegian text preserved exactly.

## 3. Scope (implemented destinations only)
Shell/nav/sidebar/status, Dashboard, Exercise Guide, Exercise Detail, Exercise Runtime, Settings, Analysis,
Reports, Diagnostics. Deferred placeholders (Progression / SmartCoach / Microphone calibration) stay deferred —
their nav buttons are visually de-emphasised (`.nav.deferred`) but not implemented.

## 4. New diagnostic smoke: `--visual-baseline-smoke`
Read-only, no screenshots, no UI change. Verifies: dark-first (`RequestedThemeVariant=Dark`); the semantic/surface
palette resolves to brushes in Dark; deferred nav surfaces still deferred (6 implemented / 3 deferred); Settings
still inert (not IDisposable, no `IRelayCommand` → no actions/persistence wired); and (source-only) implemented
views reference theme brushes and contain no old light-grey hardcoded defaults. The runtime theme checks need an
Avalonia platform (a display); when none is present they are cleanly **skipped, not failed** (like
`--theme-loc-smoke`). `--packaged-theme-smoke`'s key set + AXAML cross-check were expanded to the new 28 keys.

## 5. Crash robustness (discovered during the gate)
The platform-initializing smokes (`--theme-loc-smoke`, `--packaged-theme-smoke`, `--visual-baseline-smoke`) call
`SetupWithoutStarting()` + `UsePlatformDetect()`, which initializes the X11/GL platform when a display is present.
On the NVIDIA proprietary GL driver this **intermittently segfaults during the `atexit` GL teardown at process
exit (~1/12 runs)** — *after* the smoke has produced the correct result — turning a passing smoke into a spurious
SIGSEGV/139 exit. Fix: once a smoke has its result, flush output and terminate via POSIX `_exit()` (skips `atexit`
handlers, hence the buggy driver teardown). Linux-only; the real GUI path is untouched.

## 6. Outcome / deferred
A coherent dark FemVoice baseline across all implemented surfaces; signing/notarization unblocked from a visual
standpoint but NOT started. Deferred: pixel-perfect parity, real theme switching/persistence, visual redesign
beyond baseline, the deferred clinical surfaces.
