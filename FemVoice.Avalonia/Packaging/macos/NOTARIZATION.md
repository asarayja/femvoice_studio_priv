# macOS Codesigning / Notarization Readiness (future)

**Status: readiness only. No real codesigning or notarization is performed today; unsigned local macOS publish
remains fully supported.** This document describes what a *future* release pipeline would need. It introduces no
Apple credentials, no certificates, and no changes to the normal publish.

## Current state
- `publish-macos.sh osx-x64` (and `osx-arm64`) publishes the framework-dependent app bits. It does **not** sign,
  notarize, or build a `.app`/`.dmg`, and requires **no** Apple Developer account. Unchanged by this readiness work.
- Codesigning/notarization is **optional and deferred** — not wired into the publish, never required for local
  development or CI smoke.

## Readiness check (no secrets)
```
./FemVoice.Avalonia/Packaging/macos/notarization-readiness.sh --check     # validate the readiness surface, exit 0
./FemVoice.Avalonia/Packaging/macos/notarization-readiness.sh --dry-run   # print the planned FUTURE steps, exit 0
./FemVoice.Avalonia/Packaging/macos/notarization-readiness.sh --help      # usage, exit 0
```
The script performs **no signing/notarization**, requires **no secrets/Apple account**, and exits `0` in
`--check`/`--dry-run` even when `codesign`/`xcrun` are absent (e.g. on Linux — reported as optional, future-only).
It never prints env-var **values** and never runs `notarytool submit`.

## Future requirements (choose later; none implemented here)
1. **Apple Developer account** + a **Developer ID Application** certificate in the signing keychain.
2. Build a `.app` bundle (consuming `Packaging/macos/Info.plist`) — a separate deferred bundling slice.
3. **Codesign with hardened runtime:**
   `codesign --force --options runtime --timestamp --sign "$FEMVOICE_MACOS_SIGNING_IDENTITY" [--entitlements <file>] <App>.app`
4. **Entitlements (only if needed later):** a minimal entitlements plist; this app is display-only with no real
   microphone capture, so a mic entitlement is NOT added now.
5. **Notarize:** `xcrun notarytool submit <App>.zip --keychain-profile "$APPLE_NOTARY_PROFILE" --wait`
   (or `--apple-id`/`--team-id`/`--password` from secret storage).
6. **Staple:** `xcrun stapler staple <App>.app`.

## Where a future CI/release pipeline injects secrets
- Apple credentials (certificate `.p12`, app-specific password / notary profile, API key) come from **CI secret
  storage only**, imported into an **ephemeral keychain** in the release job, and **never** written to the repo
  or to build artifacts. The signing/notarization step runs **only** in a dedicated release job (not PR/local/smoke).

## Optional env vars (documented only — never required, never committed)
| Var | Purpose (future pipeline) |
| --- | --- |
| `APPLE_TEAM_ID` | Apple Developer Team ID |
| `APPLE_DEVELOPER_ID_APPLICATION` | "Developer ID Application: …" certificate name |
| `APPLE_NOTARY_PROFILE` | `notarytool` keychain profile name |
| `APPLE_ID` | Apple ID used for notarization (if not using a profile) |
| `APPLE_APP_SPECIFIC_PASSWORD` | app-specific password for notarization (secret) |
| `FEMVOICE_MACOS_SIGNING_IDENTITY` | signing identity passed to `codesign --sign` |
| `FEMVOICE_MACOS_ENTITLEMENTS` | path to an entitlements plist, if one is added later |

These are read (presence only) by `notarization-readiness.sh`; they are **not** consumed by `publish-macos.sh`
and do not affect local publishes.

## Must NEVER be committed
Apple ID, app-specific passwords, `.p12`/`.cer` certificates, private keys, provisioning profiles, App Store
Connect API keys/tokens, or any credential **values**. `.gitignore` already excludes build output under `/artifacts/`.

## How unsigned local publish stays supported
`publish-macos.sh` is unchanged and produces unsigned app bits. Nothing here makes signing/notarization a
prerequisite for building the app.

> The repository is private/proprietary (see `linux/debian-copyright`, `License: Proprietary`). No open-source
> license is assumed; if a `LICENSE` file is added later, update packaging metadata to match.
