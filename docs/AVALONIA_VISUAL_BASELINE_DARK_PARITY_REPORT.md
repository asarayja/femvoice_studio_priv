# Avalonia Cross-platform Visual Baseline / Dark Theme Parity — Report

Date: 2026-06-17 · Branch: `avalonia-visual-baseline-dark-parity-slice` · Host: Linux (.NET 10 user-local `~/.dotnet`).

> Visual styling/layout only. No clinical/domain/WPF behaviour changed. No real mic, persistence, export,
> SmartCoach/progression, safety-gate, voice-health. No signing/notarization. No theme/settings persistence.

## Visual reference
The Windows/WPF **dark-mode** screenshots (Dashboard, Exercise Guide, Exercise Detail, Settings) were used as the
design reference. The Avalonia baseline is **not pixel-perfect** — it follows the same dark visual identity:
navy slate background, purple primary accent, green success / red danger / blue secondary accents, rounded cards,
subtle borders, consistent spacing, white headings, muted gray secondary text, intentionally-disabled deferred
controls.

## What changed (visual only)
- **Dark-first**: `App.axaml` `RequestedThemeVariant="Dark"` (Avalonia head only). The app now resolves the Dark
  variant by default regardless of the OS/session theme (previously `Default` → Light on this box, which read as
  washed-out). FluentTheme renders dark controls, removing the light-gray button wall.
- **Theme palette** (`Themes/ShellTheme.axaml`): the 14 existing `Shell*` brushes retuned to navy/purple, plus
  new surface + semantic + chart/chip keys (28 total, defined in **both** Dark and Light).
- **Control styles** (`App.axaml`): `Button.primary` (purple), `Button.secondary` (blue), `Button.nav` flat rail
  + `.nav.deferred` (de-emphasised), `Border.card` (elevated surface), `Border.chip`. Cosmetic only.
- **Shell** (`MainWindow.axaml`): navy window background; heading-coloured title; nav rail buttons use `.nav`,
  deferred items de-emphasised but still navigable to their deferred placeholder page.
- **Dashboard**: cards; large purple **Start**; accent live-pitch number; green comfort-zone text; themed
  feedback + pitch-trace (chart background + cyan trace via theme keys); disabled pro-tools chips.
- **Exercise Guide**: dark cards with purple circular icon badge, category chip, blue **Åpne ›**.
- **Exercise Detail**: hero header with purple icon badge; cards; purple step bullets; purple **Start øvelse**;
  warning-coloured safety card.
- **Exercise Runtime**: all hardcoded hex → theme brushes; cards; primary/secondary Start/Stop; themed chart
  (background/target-band/trace/marker keys).
- **Settings / Analysis / Reports / Diagnostics**: upgraded existing brush-driven cards to the elevated `.card`
  style; Analysis mini-chart background themed. Inert/disabled actions unchanged.

All bindings and Norwegian display text were preserved exactly; no view logic changed.

## Behaviour
**None changed.** No clinical/domain/WPF change; deferred surfaces remain deferred; Settings stays inert (no
actions, no theme/settings persistence). Project references unchanged (Core + Audio.Abstractions only);
`Tmds.DBus.Protocol` pinned 0.21.3; no forbidden runtime/platform references introduced.

## Crash robustness fix (NVIDIA GL atexit teardown)
During the gate, the platform-initializing smokes (`--theme-loc-smoke`, `--packaged-theme-smoke`,
`--visual-baseline-smoke`) were found to **intermittently segfault on process exit (~1/12 runs, SIGSEGV/139,
core dumped)**. Root cause (from the coredump): the NVIDIA proprietary GL driver crashes in its `atexit` teardown
(`__run_exit_handlers` → `libGLX_nvidia.so`/`libnvidia-glcore.so`) at exit, *after* the smoke already produced
its correct result — `SetupWithoutStarting()` + `UsePlatformDetect()` initializes X11/GL when a display is
present. Not our logic, not a packaging issue. Fix: once a smoke has its result, flush output and terminate via
POSIX `_exit()` (skips `atexit` handlers, hence the buggy driver teardown); Linux-only; the real GUI path is
untouched. Verified: **90 consecutive runs (30× each of the 3 smokes) → 0 non-zero exits, 0 new coredumps.**

## Manual Linux visual verification
The packaged/published path (`cd artifacts/publish/linux-x64 && DISPLAY=:0 dotnet FemVoice.Avalonia.dll`) was
launched for screenshot inspection; result recorded in the slice report turn (UI opens dark-themed and stays
visible). `sudo` is unavailable non-interactively, so the `.deb` was not re-installed; the published path is the
same runtime the `.deb` launcher uses.

## Signing/notarization
Unblocked from a visual standpoint, but **NOT started** (separate, deferred slice).

## Deferred
Pixel-perfect parity, real theme switching/persistence, visual redesign beyond baseline, and the deferred clinical
surfaces (Progression / SmartCoach / Microphone calibration).
