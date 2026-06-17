# Avalonia Packaged Theme / Resource Verification — Gate Results

Date: 2026-06-17 · Branch: `avalonia-packaged-theme-resource-verification-slice` (off `main` @ `901d682`) · Host: Linux (.NET 10 user-local `~/.dotnet`).

All commands from the repo root with `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**
(`--packaged-theme-smoke` added; default build/run unchanged.)

## Smokes (`dotnet run --project FemVoice.Avalonia --no-build -- --<smoke>`)
| Smoke | Result |
| --- | --- |
| `--smoke` … `--packaging-smoke` (15 prior) | **all OK** |
| `--packaged-theme-smoke` (new, 16th) | **OK** — with a display: `runtime: FluentTheme=True shellKeys=14×(Dark+Light) keysResolve=True variant='Light'`; AXAML cross-check OK (source) |

**16/16 OK** (gate run with `DISPLAY=:0`, so the runtime resource checks executed).

### `--packaged-theme-smoke` headless behaviour (verified)
The smoke is **headless-safe**: the runtime checks need an Avalonia platform/display. Verified all four modes:
| Mode | Runtime checks | Result |
| --- | --- | --- |
| source, `DISPLAY=:0` | run | OK (exit 0) |
| source, no `DISPLAY`/`WAYLAND` | **skipped (not failed)** | OK (exit 0) |
| published DLL, no display | **skipped** | OK (exit 0) |
| published DLL, `DISPLAY=:0` | run (parity proof) | OK (exit 0) |

A genuine missing-FluentTheme / unresolved-key (with the platform up) is still a real FAIL — only the no-display
case maps to skip (mirrors `--theme-loc-smoke`). This was the one finding from adversarial review (a headless
false-FAIL), fixed before commit.

## Published-output theme/resource verification (the slice's core)
From `dotnet artifacts/publish/linux-x64/FemVoice.Avalonia.dll`:
- `--theme-loc-smoke` → 14 shell brushes all present × Dark+Light, **OK**.
- `--packaged-theme-smoke` → setup/FluentTheme/14 keys/variant=Light, **OK** (AXAML cross-check skipped — no source).
- `--smoke` → shared services resolve, **OK**.
- `--packaging-smoke` → non-zero **by design** (source-tree inspection; csproj/scripts absent from publish). Passes from source-run.

→ **Source-run and published-output resolve identical theme/resources. No resource loss.**

## Vulnerable packages
`dotnet list … --vulnerable --include-transitive` → **none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Leak guards
- Base leak guard (non-comment forbidden tokens in `FemVoice.Avalonia` .cs/.axaml/.csproj): **clean**.
- Packaging/platform leak guard: **clean**. New `--packaged-theme-smoke` introduces only allowed tokens
  (theme/resource/DynamicResource/FluentTheme/Shell* keys) — no runtime platform implementation.

## Packaging verification
- `publish-linux.sh linux-x64` → published; published `--smoke` OK.
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` clean (root:root); `.deb` ships
  `Avalonia.Themes.Fluent.dll`, `FemVoice.Avalonia.dll` (embedded ShellTheme), `libSkiaSharp.so` under `/opt/femvoice-studio`.
- `publish-macos.sh osx-x64` → **OK**.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed 1570, Failed 10, Total 1580** (known localization-data baseline;
1569/1580 acceptable variant when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate`
flake fires). No new failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched. No UI redesign.** A read-only diagnostic smoke + docs only;
default build/run unchanged. Signing/notarization remains **not started** (now unblocked from a resource standpoint).
