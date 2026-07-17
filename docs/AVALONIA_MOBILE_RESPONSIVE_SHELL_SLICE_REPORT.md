# Avalonia — Mobile-responsive shell — Slice Report

Date: 2026-07-18 · Branch: `avalonia-mobile-responsive-shell-slice` · Host: Linux (.NET 10 `10.0.110`).

## Goal
Make the shared `ShellView` adapt to width so it works on phones (Android) as well as desktop, advancing "supports
Android + design". Display-only layout; no behaviour/clinical change.

## What changed
- `FemVoice.Avalonia.UI/Views/ShellView.axaml`: the fixed 3-column body Grid → a **`SplitView`** nav pane +
  `[content | info sidebar]`, plus a **hamburger** button in the header. Nav buttons + hamburger wired to code-behind.
- `FemVoice.Avalonia.UI/Views/ShellView.axaml.cs`: `SizeChanged`-driven `ApplyResponsive(width)`:
  - **wide (≥900px):** nav rail inline + info sidebar visible + no hamburger (desktop, unchanged look).
  - **tablet (620–899px):** nav rail inline, info sidebar hidden, no hamburger.
  - **phone (<620px):** nav pane **overlays** behind a **hamburger** (closed by default), info sidebar hidden,
    content full-width; tapping a nav item closes the overlay.
- `FemVoice.Avalonia/Program.cs`: the `--snapshot` utility gains `--size WxH` to preview any width (e.g. a phone).

## Verification
- Offscreen snapshots: **desktop (1100×760)** unchanged (3-column, no hamburger); **phone (400×820)** shows the
  hamburger + collapsed nav + hidden sidebar + full-width content.
- Gate: build 0 err, **41/41 smokes** (shell/visual/theme all green — ShellView still uses `Shell*` brushes),
  portable **1570/1580**. No VM/clinical/Core/WPF change; namespaces unchanged.

## Deferred
Per-page mobile tuning (touch targets, phone-optimised dashboards), and running on a real device/emulator.
