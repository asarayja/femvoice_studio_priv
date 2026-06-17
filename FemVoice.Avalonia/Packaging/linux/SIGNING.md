# Linux `.deb` Signing Readiness (future)

**Status: readiness only. No real signing is performed today; unsigned local `.deb` builds remain fully
supported.** This document describes what a *future* release pipeline would need to sign the Linux package. It
introduces no required secrets, no GPG keys, and no changes to the normal build.

## Current state
- `package-deb.sh linux-x64` builds an **unsigned** `.deb` under `artifacts/packages/deb/` (framework-dependent,
  `root:root`, no maintainer hook scripts). This is the supported local flow and is unchanged by this readiness work.
- Signing is **optional and deferred** — it is NOT wired into `package-deb.sh` and is never required for local
  development or CI smoke.

## Readiness check (no secrets)
```
./FemVoice.Avalonia/Packaging/linux/signing-readiness.sh --check     # validate the readiness surface, exit 0
./FemVoice.Avalonia/Packaging/linux/signing-readiness.sh --dry-run   # print the planned FUTURE steps, exit 0
./FemVoice.Avalonia/Packaging/linux/signing-readiness.sh --help      # usage, exit 0
```
The script performs **no signing**, requires **no secrets**, and exits `0` in `--check`/`--dry-run` even when
`gpg`/`dpkg-sig` are absent (they are reported as optional, future-only). It never prints env-var **values**.

## Future signing options (choose later)
A future real-signing slice would pick one of these; none is implemented here:
- **apt-repository signing (recommended for distribution):** publish the `.deb` into an apt repo and sign the
  repository `Release` file with a repo GPG key (`gpg --clearsign`/`InRelease`). Clients trust the repo key. The
  individual `.deb` need not be detached-signed.
- **Detached package signing (optional):** `dpkg-sig --sign builder femvoice-studio_<ver>_<arch>.deb`, or a
  detached `gpg --detach-sign` over the `.deb`, distributed alongside it.
- **Checksums (always cheap):** generate `SHA256SUMS` for the release artifacts (`sha256sum *.deb > SHA256SUMS`)
  and optionally sign that file.

## Where a future CI/release pipeline injects keys
- Keys/passphrases come from **CI secret storage only** (e.g. encrypted CI secrets / a KMS), imported into an
  ephemeral keyring inside the signing job, and **never** written to the repo or to build artifacts.
- The signing step runs **only** in a dedicated release job — never in PR builds, local builds, or smoke tests.

## Optional env vars (documented only — never required, never committed)
| Var | Purpose (future pipeline) |
| --- | --- |
| `FEMVOICE_DEB_SIGNING_KEY_ID` | GPG key id the release job signs with |
| `FEMVOICE_DEB_SIGNING_KEYRING` | path to the ephemeral keyring the job imports the key into |
| `FEMVOICE_RELEASE_CHECKSUMS` | set to `1` to request `SHA256SUMS` generation in the release job |

These are read (presence only) by `signing-readiness.sh`; they are **not** consumed by `package-deb.sh` and do
not affect local builds.

## Must NEVER be committed
Private keys, keyrings (`*.gpg`/`*.asc` private material), passphrases, GPG `--armor` private exports, API
tokens, or any `*_KEY_ID`/passphrase **values**. `.gitignore` already excludes build output under `/artifacts/`.

## How unsigned local packages stay supported
`package-deb.sh` is unchanged and produces an unsigned `.deb`. Nothing in this readiness work makes signing a
prerequisite for building or running the app locally.

> The repository is private/proprietary (see `linux/debian-copyright`, `License: Proprietary`). No open-source
> license is assumed; if a `LICENSE` file is added later, update the packaging copyright to match.
