# Avalonia — Offscreen UI snapshot harness — Slice Report

Date: 2026-07-17 · Branch: `avalonia-headless-ui-snapshot-slice` (off `main` @ `2202305`) · Host: Linux (.NET 10 `10.0.110`).

## Goal

Give the project a way to **render the Avalonia UI to a PNG with no visible window** — so UI work can be verified
visually (and against the WPF design) even when the desktop session is locked, on a headless box, or in CI. Requested
by the user (2026-07-17): "when something in the UI is worked on, can we take screenshots too?".

Live screen-capture (spectacle/PIL) was tried first but only works with an **unlocked** X session; on a locked
session it captures the lock screen. Offscreen rendering removes that dependency entirely.

## What changed (files)

- **`FemVoice.Avalonia/FemVoice.Avalonia.csproj`** — added the `Avalonia.Headless` 11.2.1 package (managed-only;
  used solely by the snapshot utility, not by the normal GUI or the smokes).
- **`FemVoice.Avalonia/Program.cs`**:
  - `--snapshot [outPath] [--page <name>]` utility — sets up the **headless Skia** platform
    (`UseHeadless(UseHeadlessDrawing=false)`), shows the shared `ShellView` in an offscreen `Window`, runs a layout
    + render pass, and saves the frame via `CaptureRenderedFrame()`. Pages: dashboard (default), guide, settings,
    analysis, reports, diagnostics, smartcoach, progression.
  - `--snapshot-smoke` (41st) — renders the shell and asserts a **valid, non-trivial PNG** (>20 KB; a blank render
    is ~3 KB, the real dashboard ~110 KB) with a correct PNG header. Guards the capability in CI.
  - `NavigateShell` helper — case-insensitive nav so `--page guide` etc. select the right destination.

`RenderTargetBitmap` on a detached control was tried first but produced a blank frame (styles/theme aren't applied
to an unattached control); the headless `Window` + `CaptureRenderedFrame` path applies the full theme/style pass.

## Verification

- `--snapshot-smoke`: `rendered=True pngHeader=True size=112461 nonTrivial=True` → **OK**.
- Rendered the dashboard, exercise guide, and settings pages to PNG (dark theme, nav rail, cards, chart, category
  chips, exercise list all render correctly). The dashboard snapshot also **visually confirms the real-mic
  activation** — the status strip reads "Mikrofon: enheter funnet: 1".
- Full gate: build 0 err, **41/41 smokes**, portable 1570/1580 (baseline; the transient 11th was the documented
  `ComfortZoneController` timing flake — Core untouched).

## Notes / follow-up

- The PNGs are reproducible via the tool, so they are **not committed** (regenerate with `--snapshot`).
- `Avalonia.Headless` currently sits in the app project for convenience; moving the snapshot utility to a dedicated
  dev/tool project (so the shipped app carries no headless dependency) is an optional follow-up.
- Display-only copy ("kun visning · ingen mikrofon") is now slightly stale on the dashboard since the real mic is
  active — a small truthfulness follow-up.
