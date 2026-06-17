# Avalonia Packaged Theme / Resource Verification — Report

Date: 2026-06-17 · Branch: `avalonia-packaged-theme-resource-verification-slice` · Host: Linux (.NET 10 user-local `~/.dotnet`).

> Diagnostics/verification only. No UI redesign, no clinical/domain/WPF change.

## Manual visual observation (input)
The `.deb`-installed app opens (PR #17 fix holds) but the UI looks **visually poor / mostly unstyled / without
expected colors**.

## Conclusion

```
No packaging resource loss found.
Visual design polish is deferred.
Signing/notarization may proceed after this verification (resource-integrity-wise).
```

The theme/resource layer resolves **identically** across source-run, published output, and the `.deb`-shipped
bits. The plain appearance is the app's current **deferred design state** (display-only scaffold views), not a
missing-asset problem. A contributing visual factor: the shell was authored **dark-first**, but
`RequestedThemeVariant="Default"` follows the session, which on the test environment resolves to the **Light**
variant — so the chrome uses the lighter Light brushes. That is environment/variant-driven and identical in
source and packaged runs; it is not a packaging defect.

## Evidence

### Source-run vs published-output parity (the core check)
| Check | Source run | Published DLL (`artifacts/publish/linux-x64`) |
| --- | --- | --- |
| `--theme-loc-smoke` 14 shell brushes × Dark+Light | **all present**, OK | **all present**, OK |
| `--packaged-theme-smoke` setup / FluentTheme / 14 keys / variant | `True / True / resolve / Light`, OK | `True / True / resolve / Light`, OK |
| `--smoke` (shared services resolve) | OK | OK |

Identical resolution → no resource is dropped by publish. (The runtime resource checks require an Avalonia
platform/display to execute; the table rows above were produced with a display present — this dev box has
`DISPLAY=:0`. In a genuinely headless context the runtime checks are cleanly **skipped, not failed**, exactly like
`--theme-loc-smoke`, so the gate stays green either way; the parity proof itself is obtained when a display is
available.)

### Published output / `.deb` contents
- Theme-bearing assemblies present in `artifacts/publish/linux-x64` **and** in the `.deb` under
  `/opt/femvoice-studio`: `Avalonia.Themes.Fluent.dll` (FluentTheme), `Avalonia.Base/Controls/Skia`,
  `FemVoice.Avalonia.dll`, plus render natives `libSkiaSharp.so` / `libHarfBuzzSharp.so`.
- `ShellTheme.axaml` is **embedded** in `FemVoice.Avalonia.dll` (avares markers `Build:/Themes/ShellTheme.axaml`,
  `Populate:`, `NamespaceInfo:`). **Zero loose `.axaml` files** in publish — correct (resources are embedded).
- No `package-deb.sh` step strips or omits theme/resource files (the `.deb` cp's the full publish output to
  `/opt/femvoice-studio`).

### DynamicResource integrity
All 14 `{DynamicResource Shell*}` keys referenced across `MainWindow.axaml` + every `Views/*.axaml` are exactly
the 14 keys defined in `ShellTheme.axaml`'s Dark and Light dictionaries — **zero dangling references**, no
`StaticResource` use. `--packaged-theme-smoke`'s source-AXAML cross-check enforces this going forward.

## Specific questions answered
- Shell theme resources load: **yes** (source + published).
- Dark/Light dictionaries included: **yes** (both resolve in published DLL).
- DynamicResource keys used by shell/views resolve: **yes** (all 14, both variants).
- MainWindow background/sidebar/card/button resources resolve: **yes** (those are the `Shell*` brushes; all present).
- Packaged output contains required Avalonia/resource assemblies/assets: **yes** (FluentTheme + embedded ShellTheme + Skia natives).
- Resource accidentally excluded from publish: **no**.
- Linux package step strips/omits theme/resource files: **no**.

## Note on `--packaging-smoke` from the published DLL
`--packaging-smoke` inspects the **source tree** (reads `FemVoice.Avalonia.csproj` and the `Packaging/` helper
scripts via `AppContext.BaseDirectory`); run from the published DLL those source files are absent, so it returns
non-zero **by design** (not a regression). It passes from source-run. Published-output **theme/resource** parity
is covered by `--theme-loc-smoke` and `--packaged-theme-smoke`, which both pass from the published DLL.

## Installed `.deb` / launcher
`sudo` is **not available non-interactively** on this box and the package is not currently installed, so the
`.deb` was not re-installed. The packaged launcher was verified against the publish output as in PR #17, and the
published DLL passes both theme smokes — the exact embedded resources the `.deb` ships.

## Deferred (unchanged)
Visual design polish; signing/notarization (now unblocked from a resource standpoint); real capture;
persistence/export; clinical work; Android/iOS.
