# macOS App Icon / `.icns` Readiness — Slice Report

Date: 2026-06-17 · Branch: `macos-app-icon-icns-readiness-slice` (off `main` @ `8a820a7`).

> **Icon readiness only — no production icon or branding is invented or committed.** No real signing/notarization,
> no Apple credentials/secrets. Behavior-neutral, packaging-only — no clinical/domain/WPF change. Existing Linux
> `.deb` and unsigned macOS publish/`.app`/`.dmg` flows are unchanged; an icon is **not required** for packaging.

## Expected future icon path
```
FemVoice.Avalonia/Packaging/macos/AppIcon.icns      <- a real .icns is dropped here later (NOT committed yet)
```
Only the readiness note `AppIcon.icns.README.md` is committed (documents the path + how to produce a real `.icns`
with `sips`/`iconutil` on macOS). No `.icns`/`.ico`/`.png` image asset is committed.

## Info.plist icon wiring
`Info.plist` now sets `CFBundleIconFile = AppIcon` (macOS resolves `Contents/Resources/AppIcon.icns`). This is
**safe when the icon is absent** — macOS falls back to the generic app icon and packaging/launch are unaffected
(no error). The pre-existing bundle metadata (name/identifier/version) is unchanged; no branding was added.

## `package-app.sh` icon behavior
- **Present** (`Packaging/macos/AppIcon.icns` exists): copied into `Contents/Resources/AppIcon.icns`; logs
  `icon: bundled AppIcon.icns -> Contents/Resources/`.
- **Absent** (current state): logs `icon: AppIcon.icns absent — bundle uses the system default (production icon
  deferred; not an error)` and **continues** — no failure, icon not required.
- The script **never fabricates** an icon (no `iconutil`/`sips` synthesis) — it only copies an externally-provided
  asset. Writes only under `artifacts/dist/<rid>/`. No signing, no secrets.
Both branches were verified on Linux: with a throwaway `.icns` present → bundled into `Contents/Resources/`; with
it absent → graceful note, bundle still produced, `Contents/Resources/AppIcon.icns` correctly absent. (The
throwaway file was removed; nothing fake is committed.)

## Smoke coverage
**New `--macos-icon-readiness-smoke`** (23rd, read-only): verifies the icon path is documented; `Info.plist` wires
`CFBundleIconFile=AppIcon`; `package-app.sh` bundles the icon **conditionally** (`if [ -f "$ICON"` → `Contents/
Resources`) and handles absence gracefully; the packaging does **not** fabricate an icon (`no iconutil/sips`);
existing macOS packaging readiness is intact; and no key material is committed. Reports `icns-committed=false`
(deferred state, informational — not gated, so a future real icon won't break it). Inspects the source tree (like
`--packaging-smoke`); from the **published DLL** it cleanly **skips and returns 0**.

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
no forbidden WPF/audio/database/clinical references; no persistence/DB/analytics; no clinical/domain or WPF
behaviour change; no runtime platform implementation. **Secret-safety:** no PEM/key blocks. **Branding-safety:**
no `.icns`/image asset committed; no logo/brand colors/placeholder-as-final invented; production icon deferred.

## Real signing/notarization performed
**None.** The icon is unrelated to code signing; real `codesign`/`notarytool`/`stapler` remain a future
credentialed slice (see `NOTARIZATION.md`). No Apple credentials used or committed.

> The repository is private/proprietary (`linux/debian-copyright`, `License: Proprietary`). No open-source license
> is assumed; adjust only if a `LICENSE` file is added later.
