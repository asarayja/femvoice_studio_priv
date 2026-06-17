# macOS App Icon / `.icns` Readiness — Gate Results

Date: 2026-06-17 · Branch: `macos-app-icon-icns-readiness-slice` (off `main` @ `8a820a7`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Icon readiness only. No production icon/branding committed or invented; no real signing/notarization; no
> secrets. Existing Linux `.deb` + unsigned macOS publish/`.app`/`.dmg` flows unchanged; icon is not required.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (23 — all OK, all exit 0)
`--smoke` … `--macos-packaging-readiness-smoke` (22 prior) + **`--macos-icon-readiness-smoke` (new, 23rd)** → **23/23 OK.**
- `--macos-icon-readiness-smoke` (source): `icon-docs=True path-documented=True CFBundleIconFile=AppIcon=True conditional-bundle=True graceful-when-absent=True no-fabrication(iconutil/sips)=True existing-readiness-intact=True no-secrets=True icns-committed=False (deferred)`.
- From the published DLL: cleanly **skips** ("no source tree") and returns 0.

## `package-app.sh` icon behavior (verified directly on Linux)
- Icon **absent** (current): logs the deferred-readiness note and builds the unsigned `.app` anyway (exit 0); no
  `Contents/Resources/AppIcon.icns` produced.
- Icon **present** (throwaway test `.icns`, since removed): copied into `Contents/Resources/AppIcon.icns` (exit 0).
- `--check`/`--dry-run`/`--help` → exit 0; unknown arg → exit 2; dash-clean (POSIX). Never fabricates an icon.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Forbidden WPF/audio/database/clinical tokens (non-comment): **clean**.
- New smoke embeds only allowed strings (icon/script names, `iconutil`/`sips ` as detection patterns) — no forbidden token literal.

## Secret-safety / branding-safety
- No PEM/private-key blocks (`-----BEGIN`) in any new/changed file.
- **No `.icns`/`.ico`/`.png` image asset committed** anywhere under `Packaging/` — only the docs note
  `AppIcon.icns.README.md`. No logo/brand colors/placeholder-as-final invented. Production icon deferred.

## Packaging verification
- `publish-linux.sh linux-x64` → OK; **9/9 published smokes exit 0** (incl. `--macos-icon-readiness`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK; `package-app.sh osx-x64` → unsigned `.app` (icon deferred); `package-dmg.sh osx-x64` → graceful skip on Linux (no hdiutil).

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures); **1569/1580**
acceptable when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake fires. No new
failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Info.plist `CFBundleIconFile` wiring + conditional icon copy
in `package-app.sh` + a docs placeholder + a read-only smoke. No real icon/branding, no signing, no secrets.
