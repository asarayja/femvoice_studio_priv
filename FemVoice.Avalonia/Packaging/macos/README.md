# macOS Packaging (`FemVoice.Avalonia`)

**Unsigned, behavior-neutral packaging readiness for macOS.** No real codesigning or notarization is performed
here; no Apple Developer account or secrets are required or committed. Future signing/notarization is documented
separately in `NOTARIZATION.md`.

## Pipeline (unsigned)
```
publish-macos.sh <rid>     # framework-dependent publish -> artifacts/publish/<rid>   (rid: osx-x64 | osx-arm64)
package-app.sh   <rid>     # assemble an UNSIGNED "FemVoice Studio.app" -> artifacts/dist/<rid>/
package-dmg.sh   <rid>     # build a .dmg from the .app — ONLY when hdiutil is available (macOS); else skips
```
`package-app.sh <rid>` publishes first (for a deterministic bundle) and is **pure file assembly** — it runs on any
OS (including Linux/CI) and never signs.

## `.app` bundle layout
```
artifacts/dist/<rid>/FemVoice Studio.app/
  Contents/
    Info.plist            (from Packaging/macos/Info.plist — CFBundleExecutable = FemVoice.Avalonia)
    MacOS/                (the published apphost FemVoice.Avalonia + FemVoice.Avalonia.dll + all managed/native bits)
    Resources/            (reserved for icons; no production branding invented here)
```
The bundle is **framework-dependent** (a compatible .NET 10 runtime must be present on the target) and **unsigned**.

## `.dmg` (optional, macOS-only)
`package-dmg.sh <rid>` wraps the `.app` with `hdiutil create … -format UDZO`. **`hdiutil` is macOS-only**, so on
Linux / where it is absent the script reports the limitation and **exits 0** (it never fails the build/smoke). Run
it on macOS to actually produce `artifacts/dist/<rid>/FemVoice Studio.dmg`.

## Readiness checks (no secrets, any OS)
```
./package-app.sh --help | --check | --dry-run     # exit 0; --check validates surface, --dry-run prints steps
./package-dmg.sh --help | --check | --dry-run     # exit 0; build mode skips gracefully without hdiutil
./notarization-readiness.sh --check               # future codesign/notarytool readiness (see NOTARIZATION.md)
```
Unknown options exit non-zero (2). The scripts are POSIX `sh`, write only under `artifacts/dist/<rid>/`
(gitignored), perform no destructive operations, require no secrets, and print no credential values.

## What is NOT done here (deferred)
**No `codesign`, no `notarytool submit`, no `stapler staple`, no Apple credentials.** Real signing/notarization
is a **future credentialed CI release step** — see `NOTARIZATION.md` for the requirements and the optional env
vars a future pipeline would inject from secret storage. The `.app` produced here is the input that step would
sign. Unsigned local development/packaging remains fully supported.

> The repository is private/proprietary (`../linux/debian-copyright`, `License: Proprietary`). No open-source
> license is assumed; if a `LICENSE` file is added later, update packaging metadata to match.
