# Avalonia Packaged Theme / Resource Verification — Slice Plan

Date: 2026-06-17 · Branch: `avalonia-packaged-theme-resource-verification-slice` (off `main` @ `901d682`).

> **Diagnostics / verification slice only.** No UI redesign, no new visual design, no clinical/domain change,
> no WPF change. No signing/notarization, no real capture, no Android/iOS, no persistence/export/clinical work.

## 1. Problem
After the PR #17 launcher fix the `.deb`-installed app **starts** (no longer flashes-and-dies), but manual
visual testing reported the UI looks **visually poor / mostly unstyled / without expected colors**. Before any
signing/notarization we must determine whether this is **packaging resource loss** (theme/resource assets missing
from the published/installed build) or simply **deferred design polish** (the views are intentionally plain
display-only scaffolds).

## 2. How the theme is wired (facts)
- `App.axaml` merges `avares://FemVoice.Avalonia/Themes/ShellTheme.axaml` (a `ResourceDictionary` with `Dark` +
  `Light` `ThemeDictionaries`, 14 named `Shell*` brushes) and applies `<FluentTheme/>` in `Application.Styles`.
  `RequestedThemeVariant="Default"` → the running session selects the actual variant.
- The csproj uses the **default Avalonia SDK resource handling**: `.axaml` files are compiled as Avalonia
  resources and **embedded into `FemVoice.Avalonia.dll`** (`avares://`). There are **no loose `.axaml` files** at
  runtime, so a framework-dependent publish that ships the DLL ships the theme.
- Views reference theme colours only via `{DynamicResource Shell*Brush}` (no `StaticResource`, no custom
  FluentTheme-key references).

## 3. Method (compare three launch modes, read-only, no display)
1. `dotnet run --project FemVoice.Avalonia -- --theme-loc-smoke` / `--packaged-theme-smoke` (source).
2. `dotnet artifacts/publish/linux-x64/FemVoice.Avalonia.dll --theme-loc-smoke` / `--packaged-theme-smoke` /
   `--smoke` (published output — the exact bits the `.deb` ships).
3. `.deb`-installed `femvoice-studio --theme-loc-smoke` etc. (only if installable; otherwise verify the packaged
   launcher against the publish output, as PR #17 did — `sudo` is unavailable non-interactively on this box).

Plus inspect the published tree / `.deb` contents for the theme-bearing assemblies and embedded resource.

## 4. New diagnostic smoke: `--packaged-theme-smoke`
Read-only (`SetupWithoutStarting()`, no window, no screenshots, no UI change). **Headless-safe**: it never
*requires* a display to run. Asserts, in whatever build it runs from (source OR published DLL):
- `FluentTheme` is registered in `Application.Styles` (base control styling present).
- Every `{DynamicResource Shell*}` key the views reference resolves to an `IBrush` in **both** Dark and Light.
- A theme variant is resolvable (reports it — diagnostic).
- (Source-run only) cross-checks the view AXAML so no view references a key outside the defined set; cleanly
  skipped from the published DLL (no source AXAML present).

The runtime resource checks need an Avalonia platform (a display: X11/Wayland) to execute. When none is present
(genuinely headless CI/SSH/build server), the smoke **cleanly skips** those checks — reporting them SKIPPED, NOT
failed — exactly like `--theme-loc-smoke` (a missing display is not a packaging defect). So the **source↔packaged
parity proof** (resolving the embedded resources from the published DLL) is obtained by running it where a display
is available; headless it stays green by skipping.

It complements `--theme-loc-smoke` (which checks the 14 *defined* keys resolve) by additionally asserting the
*FluentTheme registration* and the *view-referenced* key set, and by being explicitly runnable from the published
DLL to prove source↔packaged parity.

## 5. Outcome criteria
- If source-run and published/installed runs resolve the **same** resources → **no packaging resource loss**;
  document the plain look as deferred polish; signing/notarization may proceed (resource-integrity-wise).
- If source is styled but packaged is missing styles → fix packaging/resource inclusion (csproj
  `AvaloniaResource`, theme `.axaml` inclusion, publish output, `package-deb.sh` omission, launcher cwd) — only
  if proven. Do not guess.

## 6. Deferred (unchanged by this slice)
Visual design polish, signing/notarization, real capture, persistence/export, clinical work, Android/iOS.
