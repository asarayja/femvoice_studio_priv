# macOS `.app` / `.dmg` Packaging Readiness — Gate Results

Date: 2026-06-17 · Branch: `macos-app-dmg-packaging-readiness-slice` (off `main` @ `27e5041`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Unsigned packaging/readiness only. No real signing/notarization; no secrets required or committed; existing
> Linux `.deb` + unsigned macOS publish flows unchanged.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (22 — all OK, all exit 0)
`--smoke` … `--signing-readiness-smoke` (21 prior) + **`--macos-packaging-readiness-smoke` (new, 22nd)** → **22/22 OK.**
- `--macos-packaging-readiness-smoke` (source): `docs=True scripts(app+dmg)=True app-flags=True dmg-flags=True dmg-hdiutil=True app-uses-plist=True no-real-signing=True unsigned+notarization-flows-intact=True no-secrets-committed=True`.
- From the published DLL: cleanly **skips** ("no source tree") and returns 0 (same source-tree-inspection nature as `--packaging-smoke`/`--signing-readiness-smoke`).

## New scripts (verified directly; POSIX `sh`, dash-clean)
`package-app.sh` and `package-dmg.sh`: `--help`/`--check`/`--dry-run` all **exit 0**; unknown option → **exit 2**.
- `package-app.sh osx-x64` (actual build, on Linux): published + assembled an **unsigned** `FemVoice Studio.app`
  with `Contents/Info.plist` + `Contents/MacOS/FemVoice.Avalonia` (executable apphost) + `FemVoice.Avalonia.dll`
  + 46 managed/native libs. No codesign.
- `package-dmg.sh osx-x64` (Linux, no hdiutil): graceful **skip**, exit 0 (DMG creation is macOS-only).

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Forbidden WPF/audio/database/clinical tokens (non-comment): **clean**.
- The new smoke embeds only allowed strings (script/tool names, `codesign -`/`notarytool ` as detection patterns) — no forbidden token literal.

## Secret-safety
- No PEM/private-key blocks (`-----BEGIN`) in any new file.
- No credential-value assignments; scripts use no env-var secrets and print no values; docs invent no branding/credentials.

## Packaging verification
- `publish-linux.sh linux-x64` → OK; published `--theme-loc`/`--packaged-theme`/`--visual-baseline`/`--visual-interaction-chart`/`--exercise-layout-parity`/`--exercise-flow-parity`/`--signing-readiness`/`--macos-packaging-readiness` smokes → **8/8 exit 0**.
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK (unsigned, unchanged).

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** typical (10 known localization-data baseline failures);
**1569/1580** acceptable when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake
fires (it did on the final run). No new failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Unsigned `.app`/`.dmg` packaging scripts + docs + a
read-only smoke. No real signing/notarization, no secrets, no persistence. Existing flows unchanged.
