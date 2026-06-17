# Desktop Package Signing / Notarization Readiness — Gate Results

Date: 2026-06-17 · Branch: `desktop-package-signing-notarization-readiness-slice` (off `main` @ `d6947b2`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Readiness/docs/tooling only. No real signing/notarization; no secrets required or committed; unsigned local
> packaging flows unchanged.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (21 — all OK, all exit 0)
`--smoke` … `--exercise-flow-parity-smoke` (20 prior) + **`--signing-readiness-smoke` (new, 21st)** → **21/21 OK.**
- `--signing-readiness-smoke` (source-run): `docs=True scripts=True flags(--check/--dry-run/--help)=True hides-values=True unsigned-flows-intact=True signing-not-mandatory=True no-secrets-committed=True env-vars-documented=True`.
- From the published DLL: cleanly **skips** ("no source tree") and returns 0 (the `Packaging/` docs/scripts are not shipped — same source-tree-inspection nature as `--packaging-smoke`).

## Readiness scripts (verified directly)
`signing-readiness.sh` and `notarization-readiness.sh` (POSIX `sh`): `--help`/`--check`/`--dry-run` all **exit 0**
without secrets; unknown option → **exit 2**; env-var **values never printed** (only "set (value hidden)"/"not
set (optional)"); missing tools (`gpg`/`dpkg-sig`/`codesign`/`xcrun`) reported as optional/future-only and the
check still passes.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` project references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Forbidden WPF/audio/database/clinical tokens (non-comment) in `.cs`/`.axaml`/`.csproj`: **clean**.
- The new `--signing-readiness-smoke` embeds only allowed strings (script/tool/env-var names) — no forbidden token literal.

## Secret-safety
- No PEM/private-key blocks (`-----BEGIN`) in any new file.
- No credential-value assignments — the scripts only **read** env-var presence (`${VAR:-}` / name-only `report_var`);
  the docs only *name* credential types/vars (incl. a "must never be committed" list). No keys/certs/tokens committed.

## Packaging verification
- `publish-linux.sh linux-x64` → OK; published `--theme-loc`/`--packaged-theme`/`--visual-baseline`/
  `--visual-interaction-chart`/`--exercise-layout-parity`/`--exercise-flow-parity`/`--signing-readiness` smokes → all exit 0.
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK (unsigned, unchanged).

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** typical (10 known localization-data baseline failures);
**1569/1580** acceptable variant when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate`
flake fires. No new failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Packaging readiness docs + dry-run/check scripts + a
read-only smoke. No real signing/notarization, no secrets, no persistence. Unsigned local packaging unchanged.
