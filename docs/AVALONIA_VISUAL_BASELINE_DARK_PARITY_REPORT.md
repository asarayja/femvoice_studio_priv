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

---

## Follow-up polish (same PR #19): Exercise Guide row-click + Dashboard chart parity

Two parity/UX gaps from manual screenshots, addressed on the same branch. Visual/UX-only; no clinical/domain/WPF
behaviour change, no data/pitch/target-profile change, no persistence, no real mic, no new charting dependency.

### 1. Exercise Guide row/card click
The whole exercise row is now a `Button.guideCard` bound to the existing `OpenExerciseCommand` (same command the
old "Åpne" button used) — clicking/tapping anywhere on the card, or Enter/Space when it is keyboard-focused, opens
the exercise detail. Hover (accent border) / pressed (purple border) / `Cursor=Hand` make it feel clickable. The
inner button was replaced by a non-interactive "Åpne ›" chevron affordance (avoids a nested button). No exercise
data or selection semantics changed.

### 2. Dashboard pitch chart parity
The dashboard chart was a crude Hz≈px bar fill. It now uses the same converter-free Canvas geometry as the runtime
chart, via the shared display-only `RuntimeChartDisplay` + portable `PitchChartAxisRangeCalculator`: a fixed axis
windowed around the comfort zone, a green comfort-zone band, subtle horizontal grid lines, a current-pitch marker
line, y-axis frequency labels (max/min Hz), a properly-scaled pitch trace (px-from-bottom), and a centered subtle
empty-state hint when not recording. The dashboard VM gained display-only `DashboardChart` (geometry snapshot) +
`PitchTracePx` (px heights) derived from the existing stabilized pitch — **the synthetic/audio pipeline, pitch
detection, and target-profile behaviour are unchanged**; `PitchSamples` (Hz) is retained.

### New smoke `--visual-interaction-chart-smoke` (read-only)
Verifies: Exercise Guide exposes the row/card open command path and opening the first card reaches the SAME
`ExerciseDetailViewModel` (`Title == card.Name`); the guide lists its 15 exercises; the dashboard exposes chart
geometry (height/band/axis) + a px trace; no charting-library dependency is referenced (detected via "Plot"/"Chart"
in referenced-assembly names — no forbidden literal embedded); chart brush keys resolve in Dark (platform-gated,
skipped-not-failed headless); and (source-only) the guide card is a `Button.guideCard` bound to `OpenExerciseCommand`
and the dashboard binds the new geometry.

### Follow-up gate
Build 0/0; **18/18 smokes OK (all exit 0)** incl. the new one; vuln clean; Tmds 0.21.3; refs Core + Audio.Abstractions
only; leak guard clean (no OxyPlot/charting dep — csproj unchanged); portable 1570/1580; published-output smokes +
`.deb` + macOS publish OK.

### Manual Linux visual verification (follow-up)
Launched the dark UI again for screenshot; the Exercise Guide rows are clickable and the dashboard chart shows the
comfort band + grid + axis labels + marker + trace (recorded in the report turn).
