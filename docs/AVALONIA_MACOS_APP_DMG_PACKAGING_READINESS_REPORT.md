# macOS `.app` Bundle / `.dmg` Packaging Readiness — Slice Report

Date: 2026-06-17 · Branch: `macos-app-dmg-packaging-readiness-slice` (off `main` @ `27e5041`).

> **Unsigned packaging / readiness only.** No real codesigning or notarization is performed; no Apple Developer
> account, certificates, or secrets are required or committed. Behavior-neutral, packaging-only — no
> clinical/domain/WPF change. Existing Linux `.deb` and unsigned macOS publish flows are unchanged.

## Current state
- `publish-macos.sh <rid>` → framework-dependent, unsigned publish to `artifacts/publish/<rid>` (unchanged).
- **New** `package-app.sh <rid>` → assembles an **unsigned** `FemVoice Studio.app` under `artifacts/dist/<rid>/`.
- **New** `package-dmg.sh <rid>` → builds a `.dmg` from the `.app` **only when `hdiutil` is available** (macOS);
  off macOS it reports the limitation and exits 0 (never fails).

## `.app` bundle readiness (`package-app.sh`)
- Accepts a RID (default `osx-x64`; `osx-arm64` supported); publishes first for a deterministic bundle.
- Produces:
  ```
  artifacts/dist/<rid>/FemVoice Studio.app/Contents/
    Info.plist   (from Packaging/macos/Info.plist; CFBundleExecutable = FemVoice.Avalonia)
    MacOS/       (published apphost FemVoice.Avalonia + FemVoice.Avalonia.dll + all managed/native bits)
    Resources/   (reserved for icons — no production branding invented)
  ```
- **Pure file assembly** — runs on any OS (verified on Linux: built a valid `.app` with the apphost + 46
  managed/native libs in `Contents/MacOS/`). **Never runs `codesign`/`notarytool`.** Framework-dependent + unsigned.

## `.dmg` readiness (`package-dmg.sh`)
- Build mode wraps the `.app` with `hdiutil create … -format UDZO` → `artifacts/dist/<rid>/FemVoice Studio.dmg`.
- **`hdiutil` is macOS-only**: on Linux / where it is absent, build mode prints the limitation and **exits 0**
  (does not fail the build or smoke). Verified on Linux: graceful skip, exit 0. Actual `.dmg` creation is performed
  only on macOS.

## Script behaviour (verified, POSIX `sh`, dash-clean)
Both scripts: `--help` → usage/0; `--check` → validate surface (no build, no secrets)/0; `--dry-run` → print
planned steps/0; unknown option → exit 2. They write only under `artifacts/dist/<rid>/` (gitignored), perform no
destructive operations, require no secrets, and print no credential values.

## Smoke coverage
**New `--macos-packaging-readiness-smoke`** (22nd, read-only): verifies the macOS README + NOTARIZATION docs exist;
`package-app.sh`/`package-dmg.sh` exist and expose `--check`/`--dry-run`/`--help`; `package-app.sh` uses
`Info.plist`; `package-dmg.sh` handles `hdiutil`; neither contains a `codesign`/`notarytool` invocation (no real
signing); the unsigned `publish-macos.sh` + `notarization-readiness.sh` flows are intact; and no key material is
committed. Inspects the source tree (like `--packaging-smoke`); from the **published DLL** it cleanly **skips and
returns 0** (the `Packaging/` scripts/docs are not shipped).

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
no forbidden WPF/audio/database/clinical references; no persistence/DB/analytics; no clinical/domain or WPF
behaviour change; no runtime platform implementation. **Secret-safety:** no PEM/key blocks, no credential-value
assignments in the new files.

## Deferred (future credentialed slice)
Real `codesign --options runtime`, `xcrun notarytool submit`, and `stapler staple` — with an Apple Developer ID
certificate and credentials injected from CI secret storage into an ephemeral keychain. None implemented or
required here; see `Packaging/macos/NOTARIZATION.md`. The unsigned `.app` this slice builds is the input that
future step would sign.

## Real signing/notarization performed
**None.** No `codesign`/`notarytool`/`stapler` run; no Apple credentials used or committed.

> The repository is private/proprietary (`linux/debian-copyright`, `License: Proprietary`). No open-source license
> is assumed; adjust only if a `LICENSE` file is added later.
