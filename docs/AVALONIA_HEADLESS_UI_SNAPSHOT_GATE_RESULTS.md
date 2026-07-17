# Avalonia — Offscreen UI snapshot harness — Gate Results

Date: 2026-07-17 · Branch: `avalonia-headless-ui-snapshot-slice` (off `main` @ `2202305`) · Host: Linux (.NET 10 `10.0.110`).

> Dev tooling: render Avalonia pages to PNG offscreen (headless Skia), no visible window needed. Adds the
> `Avalonia.Headless` package + a `snapshot` utility + a guarding smoke. No product-UI/behaviour change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Error(s).**

## Smokes (41 — all OK, all exit 0)
40 prior + **`--snapshot-smoke` (new, 41st)** → **41/41 OK.**
- `--snapshot-smoke`: `rendered=True pngHeader=True size=112461 nonTrivial=True`.
- All prior smokes remain green (each smoke runs in its own process; the snapshot's headless platform setup does not affect the others).

## Reference / leak guard
- `FemVoice.Avalonia` project references unchanged: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`. The
  new `Avalonia.Headless` is a PackageReference (managed-only), used solely by the `snapshot` utility.
- No product-UI change; no clinical/DSP/Core/WPF change. Diff scope: `FemVoice.Avalonia/Program.cs`,
  `FemVoice.Avalonia/FemVoice.Avalonia.csproj`, `docs/`.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** documented baseline (a transient extra failure in one run was
the known `ComfortZoneControllerTests` timing flake; Core untouched). **0 regressions.**

## Behaviour change
None to the product. Adds a developer capability to render any page of the shared Avalonia UI to a PNG offscreen
(works when the session is locked / headless / in CI), used to visually verify UI slices against the WPF design.
