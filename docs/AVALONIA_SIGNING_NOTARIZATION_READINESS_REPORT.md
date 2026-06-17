# Desktop Package Signing / Notarization Readiness — Slice Report

Date: 2026-06-17 · Branch: `desktop-package-signing-notarization-readiness-slice` (off `main` @ `d6947b2`).

> **Readiness / documentation / tooling slice only.** No real signing or notarization is performed; no secrets,
> certificates, GPG keys, or Apple account are required or committed. Behavior-neutral, packaging-only. No
> clinical/domain/WPF change; unsigned local `.deb` and macOS publish flows are unchanged and still supported.

## Scope
Add the readiness surface (docs + dry-run/check scripts + smoke) for a FUTURE Linux `.deb` signing and macOS
codesigning/notarization workflow, without requiring credentials or changing the build.

## Current state (unchanged)
- `package-deb.sh linux-x64` → **unsigned** `.deb` (supported local flow). Signing is **not** wired into it.
- `publish-macos.sh osx-x64`/`osx-arm64` → unsigned app bits; no codesign/notarize, no Apple account needed.

## Files added/changed
- **New** `FemVoice.Avalonia/Packaging/linux/SIGNING.md` — Linux `.deb` signing readiness (apt-repo signing /
  detached signing / checksums; where a future CI injects keys; optional env vars; never-commit list; unsigned
  stays supported).
- **New** `FemVoice.Avalonia/Packaging/linux/signing-readiness.sh` — POSIX `sh`, `--check`/`--dry-run`/`--help`;
  validates docs + reports optional env-var **presence** (never the value) + tool availability; exits `0` without
  secrets; never signs.
- **New** `FemVoice.Avalonia/Packaging/macos/NOTARIZATION.md` — macOS codesign/notarization readiness (Developer ID
  cert, hardened runtime, `codesign`, `notarytool submit`, `stapler staple`; CI secret injection; optional env
  vars; never-commit list; unsigned publish stays supported).
- **New** `FemVoice.Avalonia/Packaging/macos/notarization-readiness.sh` — POSIX `sh`, same flags/behaviour; never
  runs `notarytool`, never signs.
- **Edit** `FemVoice.Avalonia/Packaging/README.md` — signing/notarization readiness section pointing to the above.
- **Edit** `FemVoice.Avalonia/Program.cs` — new read-only `--signing-readiness-smoke` (21st smoke).
- **Docs** this report + `_GATE_RESULTS.md` + tracker.

## Script behaviour (verified)
For BOTH scripts: `--help` → usage, exit 0; `--check` → validates the readiness surface (no secrets), exit 0;
`--dry-run` → prints the planned FUTURE steps, exit 0; unknown option → exit 2. Missing tools (`gpg`/`dpkg-sig` on
the deb side; `codesign`/`xcrun` on the macOS side, absent on Linux) are reported as **optional / future-only** and
the check still **passes**. Env-var **values are never printed** (only "set (value hidden)" / "not set (optional)").

## Smoke coverage
`--signing-readiness-smoke` (read-only, no script execution, no secrets) verifies: both readiness docs exist; both
scripts exist and expose `--check`/`--dry-run`/`--help` and hide secret values; the unsigned `publish-linux.sh`/
`package-deb.sh`/`publish-macos.sh` flows are intact; signing is **not wired into the build** (the package/publish
scripts don't auto-run the readiness scripts nor contain a signing invocation); no key material (`-----BEGIN`) is
committed; and the optional env vars are documented. It inspects the source tree (like `--packaging-smoke`); from
the **published DLL** (where the `Packaging/` docs/scripts are not shipped) it **cleanly skips and returns 0**.

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
no forbidden WPF/audio/database/clinical references introduced; no persistence/DB/session-analytics; no
clinical/domain or WPF behaviour change. **Secret-safety:** no PEM/private-key blocks and no credential-value
assignments in the new files (scripts only read env-var presence; docs only *name* credential types in a "never
commit" warning).

## Deferred (what a future real-signing slice needs)
A real Linux signing slice would inject a GPG key from CI secret storage (apt-repo `Release` signing and/or
detached `.deb` signing + `SHA256SUMS`). A real macOS slice would need an Apple Developer account, a Developer ID
Application certificate, hardened-runtime `codesign`, `notarytool submit`, and `stapler staple`, with credentials
from CI secret storage into an ephemeral keychain. **Neither is implemented or required here.**

## Real signing/notarization performed
**None.** No `gpg`/`dpkg-sig`/`codesign`/`notarytool` was run; no credentials used or committed.

> The repository is private/proprietary (`linux/debian-copyright`, `License: Proprietary`). No open-source license
> is assumed; add/adjust only if a `LICENSE` file is introduced later.
