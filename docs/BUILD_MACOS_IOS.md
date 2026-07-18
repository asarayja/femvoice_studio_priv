# Building & signing FemVoice Studio for macOS and iOS

Covers the Avalonia **macOS desktop** head (cross-publishable from any OS) and the **iOS** head
(must be created + built on a Mac). Signed with a **self-created "Asarayja development" identity** where
Apple's rules allow it — see the honest limitations in each section.

---

## Part A — macOS desktop (`FemVoice.Avalonia`)

### A.1 Publish the macOS binary

From any OS with the .NET 10 SDK:

```bash
# Apple Silicon
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj \
  -c Release -r osx-arm64 --self-contained true -p:DebugType=None -o dist/osx-arm64

# Intel Macs
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj \
  -c Release -r osx-x64 --self-contained true -p:DebugType=None -o dist/osx-x64
```

Output: `dist/osx-arm64/FemVoice.Avalonia` (a raw Mach-O executable + native `.dylib`s).

### A.2 Wrap it in a `.app` bundle

macOS apps are folders. Create the structure (do this **on a Mac** so `codesign` is available):

```
FemVoice.app/
  Contents/
    Info.plist
    MacOS/            ← the published files from dist/osx-arm64/ go here
    Resources/
      logo.icns       ← convert Assets/logo.png → .icns (see below)
```

```bash
APP="dist/FemVoice.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R dist/osx-arm64/* "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/FemVoice.Avalonia"

# Icon: convert the shared logo to .icns
mkdir logo.iconset
sips -z 512 512 Assets/logo.png --out logo.iconset/icon_512x512.png
iconutil -c icns logo.iconset -o "$APP/Contents/Resources/logo.icns"
```

Minimal `Contents/Info.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>                 <string>FemVoice Studio</string>
  <key>CFBundleDisplayName</key>          <string>FemVoice Studio</string>
  <key>CFBundleIdentifier</key>           <string>com.femvoice.studio</string>
  <key>CFBundleVersion</key>              <string>1</string>
  <key>CFBundleShortVersionString</key>   <string>1.0</string>
  <key>CFBundleExecutable</key>           <string>FemVoice.Avalonia</string>
  <key>CFBundleIconFile</key>             <string>logo.icns</string>
  <key>CFBundlePackageType</key>          <string>APPL</string>
  <key>LSMinimumSystemVersion</key>       <string>11.0</string>
  <key>NSHighResolutionCapable</key>      <true/>
  <!-- The mic-based features need this consent string on macOS -->
  <key>NSMicrophoneUsageDescription</key> <string>FemVoice Studio uses the microphone for real-time voice analysis.</string>
</dict>
</plist>
```

> Tip: `dotnet tool install -g dotnet-bundle` (or the Avalonia `.app` templates) can generate the bundle for
> you instead of the manual steps above.

### A.3 Create the self-signed "Asarayja development" identity  *(on the Mac)*

```bash
# Create a self-signed code-signing identity in the login keychain
# (Keychain Access → Certificate Assistant → Create a Certificate:
#   Name = "Asarayja development", Identity Type = Self Signed Root,
#   Certificate Type = Code Signing.)
# Then confirm it is visible:
security find-identity -v -p codesigning
```

### A.4 Sign the `.app`

```bash
codesign --deep --force --options runtime \
  --sign "Asarayja development" \
  "dist/FemVoice.app"

# verify
codesign --verify --deep --strict --verbose=2 "dist/FemVoice.app"
```

### A.5 Honest Gatekeeper limitation

A **self-signed** identity is *not* accepted by Gatekeeper for distribution: on another Mac the app is
quarantined ("cannot be opened because the developer cannot be verified"). Options:

- **Local/your own Mac:** right-click → Open the first time, or clear quarantine:
  `xattr -dr com.apple.quarantine dist/FemVoice.app`.
- **Distribute to others without warnings:** requires a paid **Apple Developer Program** membership → a
  **Developer ID Application** certificate → `codesign` with it → **notarize** with
  `xcrun notarytool submit … --wait` → `xcrun stapler staple`. Self-signing can't do this; it's an Apple
  policy, not a project limitation.

### A.6 Run / smoke-test (on the Mac)

```bash
open dist/FemVoice.app
# headless self-check:
dist/FemVoice.app/Contents/MacOS/FemVoice.Avalonia --shell-smoke   # exit 0 = OK
```

---

## Part B — iOS (`FemVoice.iOS` — must be created)

There is **no iOS head in the repo yet** (only `FemVoice.Android`). iOS needs its own head project that reuses
the shared `FemVoice.Avalonia.UI` library, exactly like the Android head does. **Building for iOS requires a Mac
with Xcode.**

### B.1 Create the iOS head (mirror the Android head)

```bash
# On the Mac, with the Avalonia templates + iOS workload installed:
dotnet workload install ios
dotnet new install Avalonia.Templates

# Create the head, then edit it to reference the shared UI library:
dotnet new avalonia.ios -o FemVoice.iOS -n FemVoice.iOS
```

Then, in `FemVoice.iOS/FemVoice.iOS.csproj`:

- `<TargetFramework>net10.0-ios</TargetFramework>`
- Add `<ProjectReference Include="..\FemVoice.Avalonia.UI\FemVoice.Avalonia.UI.csproj" />`
- Set `<ApplicationId>com.femvoice.studio</ApplicationId>` and `<ApplicationTitle>FemVoice Studio</ApplicationTitle>`
- In `Info.plist` add **`NSMicrophoneUsageDescription`** (required or the app is rejected/crashes on mic use).
- The `App` single-view lifetime + `Program.Services` DI are already in `FemVoice.Avalonia.UI` (the same code
  the Android head uses) — the iOS `AppDelegate` just hosts `AppBuilder.Configure<App>().UseiOS()`.
- Audio: iOS has no ALSA; it will use the synthetic backend until a real iOS capture backend is added behind
  `IAudioCaptureService` in `FemVoice.Audio.Abstractions` (same pattern as the Linux ALSA backend).

### B.2 Build

```bash
# Simulator (no signing needed):
dotnet build FemVoice.iOS/FemVoice.iOS.csproj -c Release -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64

# Device (.ipa) — requires signing, see B.3:
dotnet publish FemVoice.iOS/FemVoice.iOS.csproj -c Release -f net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true -o dist/ios
```

### B.3 Signing — honest limitation

Unlike macOS/Windows, iOS **device** deployment **cannot** use a self-signed "Asarayja development" cert:

- **Simulator:** runs **unsigned** — great for testing the full UI without any Apple account.
- **Physical device (free Apple ID):** Xcode can provision a **7-day personal** signing profile — enough to
  side-load onto your own device for testing. Set the Team in Xcode, or:
  `-p:CodesignKey="Apple Development: <your Apple ID>" -p:CodesignProvision="<profile>"`.
- **TestFlight / App Store / long-lived device builds:** require the paid **Apple Developer Program** +
  Distribution certificate + provisioning profile. There is no self-signed path for iOS device distribution —
  it's an Apple restriction.

### B.4 Recommendation

For now, test iOS in the **Simulator** (unsigned) to validate parity; use the **free personal profile** to try
it on your own iPhone; move to a paid Apple Developer account only when you need TestFlight/App Store.

---

## Summary of what needs a Mac / an Apple account

| Target | Build host | Self-signed "Asarayja development" enough? |
|---|---|---|
| macOS desktop `.app` (your own Mac) | any OS to publish; Mac to bundle+sign | ✅ (clear quarantine locally) |
| macOS desktop distributed to others | Mac | ❌ needs Apple Developer ID + notarization |
| iOS Simulator | Mac | ✅ (runs unsigned) |
| iOS on your own device | Mac | ⚠️ free personal profile (7-day), not the self-signed cert |
| iOS TestFlight / App Store | Mac | ❌ needs paid Apple Developer Program |
