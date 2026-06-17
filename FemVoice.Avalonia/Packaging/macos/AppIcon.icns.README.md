# macOS App Icon Readiness — `AppIcon.icns` (future, not committed)

**Status: readiness only. No production icon or branding is committed or invented.** This documents the slot a
*future* real macOS app icon will occupy. The unsigned `.app`/`.dmg` packaging works today **without** an icon.

## Expected future asset
```
FemVoice.Avalonia/Packaging/macos/AppIcon.icns      <- drop a real .icns here (NOT committed yet)
```
- `Info.plist` already wires `CFBundleIconFile = AppIcon`, so macOS looks for `Contents/Resources/AppIcon.icns`.
- `package-app.sh` copies `AppIcon.icns` into `Contents/Resources/` **only if the file exists**; when it is absent
  (the current state), the bundle uses the generic system icon and packaging/launch are unaffected (no error).

## Why no icon is committed
A real app icon is **production branding** and is intentionally **deferred** — this slice must not invent a logo,
brand colors, or a placeholder masquerading as final art. So no `.icns` (real or fake) is committed; only this
readiness note exists.

## How to produce a real `AppIcon.icns` later (on macOS, when branding is ready)
Given a 1024×1024 master PNG of the finished icon:
```
mkdir AppIcon.iconset
sips -z 16 16     icon.png --out AppIcon.iconset/icon_16x16.png
sips -z 32 32     icon.png --out AppIcon.iconset/icon_16x16@2x.png
sips -z 32 32     icon.png --out AppIcon.iconset/icon_32x32.png
sips -z 64 64     icon.png --out AppIcon.iconset/icon_32x32@2x.png
sips -z 128 128   icon.png --out AppIcon.iconset/icon_128x128.png
sips -z 256 256   icon.png --out AppIcon.iconset/icon_128x128@2x.png
sips -z 256 256   icon.png --out AppIcon.iconset/icon_256x256.png
sips -z 512 512   icon.png --out AppIcon.iconset/icon_256x256@2x.png
sips -z 512 512   icon.png --out AppIcon.iconset/icon_512x512.png
sips -z 1024 1024 icon.png --out AppIcon.iconset/icon_512x512@2x.png
iconutil -c icns AppIcon.iconset -o AppIcon.icns
```
Place the resulting `AppIcon.icns` at the path above. `package-app.sh` and `package-dmg.sh` need no changes —
the icon is bundled automatically on the next `package-app.sh <rid>`. (`sips`/`iconutil` are macOS-only; this repo
never generates or fabricates an icon.)

## Boundaries
No signing/notarization, no Apple credentials, no secrets are involved in icon handling. The icon is unrelated to
code signing — a future credentialed slice still performs `codesign`/`notarytool`/`stapler` separately
(see `NOTARIZATION.md`).

> The repository is private/proprietary (`../linux/debian-copyright`, `License: Proprietary`). No open-source
> license is assumed; if a `LICENSE` file is added later, update packaging metadata to match.
