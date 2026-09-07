# Building & signing FemVoice Studio for macOS and iOS

Covers the Avalonia **macOS desktop** head (cross-publishable from any OS) and the **iOS** head
(must be created + built on a Mac). Signed with a **self-created "Asarayja development" identity** where
Apple's rules allow it — see the honest limitations in each section.

---

## 0. Get the source

```bash
git clone https://github.com/asarayja/femvoice_studio_priv.git
cd femvoice_studio_priv
git checkout main          # the default branch; all platform heads (incl. FemVoice.iOS) live here
```

Prerequisite: the **.NET 10 SDK**. The macOS `.app` can be *published* from any OS, but bundling/signing and all
iOS work require a **Mac + Xcode** (and, for iOS, `dotnet workload install ios`).

## Part A — macOS desktop (`FemVoice.Avalonia`)

### A.0 The easy route — let GitHub Actions build it (recommended)

You do **not** need a Mac to *build* the Mac app; you only need one to *run* it. The **Release**
workflow builds it on a real macOS runner and publishes it, together with the Linux and Android
packages, as one GitHub Release on the public distribution repo.

1. Open the repo on GitHub → **Actions** → **Release (macOS build + GitHub Release)** → **Run workflow**.
2. Pick the architecture (`both` is the default; `arm64-only` is right for any Mac from 2020 onward) and
   the release mode (`draft` is the default, so the Windows installer can be added before publishing).
3. Download the `.dmg` for your Mac's architecture from the release page (or from the run's artifacts
   when you used `release_mode: none`), then drag **FemVoice Studio** to Applications.

The build is **self-contained**: the .NET runtime is inside the app, so the target Mac needs nothing
installed. It runs from a manual trigger only.

> Publishing to the distribution repo needs a `RELEASE_TOKEN` secret — a fine-grained PAT with
> *Contents: Read and write* on `asarayja/FemVoice-Studio`. The automatic `GITHUB_TOKEN` cannot write to
> another repository. Without it the build still succeeds and the assets are attached to the run.

> **Why a `.dmg` and not the `.app`?** GitHub's artifact upload zips whatever you give it and drops the
> executable permission bit. A zipped `.app` arrives with a non-executable apphost and simply refuses to
> start. A disk image preserves permissions, so the `.dmg` is the artifact.

### A.0.1 Opening it the first time (the app is unsigned)

The app is **not code-signed or notarized** — that requires a paid Apple Developer ID (see
`FemVoice.Avalonia/Packaging/macos/NOTARIZATION.md`). macOS quarantines anything downloaded from the
internet, so on first launch you will get *"FemVoice Studio cannot be opened because the developer
cannot be verified"* — or, on Sonoma and later, *"...is damaged and can't be opened"*, which is
Gatekeeper's misleading wording for *unsigned*, **not** a corrupted download.

Two ways to open it:

- **Right-click** the app in Applications → **Open** → **Open** in the dialog. macOS remembers the choice.
- Or clear the quarantine flag in Terminal:

  ```bash
  xattr -dr com.apple.quarantine "/Applications/FemVoice Studio.app"
  ```

If neither works, check **System Settings → Privacy & Security**; a blocked launch leaves an
**"Open Anyway"** button there for about an hour.

**Microphone.** macOS capture runs through **AudioQueue** (`CoreAudioCaptureService`, AudioToolbox
P/Invoke — the macOS counterpart to ALSA on Linux and winmm on Windows). The first time you start a
recording, macOS asks for access, using the `NSMicrophoneUsageDescription` string from the bundle.

Granting it is required. If you refuse, **macOS does not return an error** — it delivers digital silence
instead, so the meter simply never moves and nothing explains why. The backend watches for that: about
two seconds of perfectly zero samples raises a device-lost with an actionable message, because a working
microphone always carries some noise floor. Re-enable it under **System Settings → Privacy & Security →
Microphone**.

This is also why the CI bundle is **ad-hoc code-signed** (`codesign --sign -`). macOS remembers a
microphone decision against the app's code identity; a completely unsigned bundle has none and is
identified by path alone, so the permission can be forgotten or re-prompted on every launch. Ad-hoc
signing costs nothing and needs no Apple account, and it makes "allow" stick. It does **not** notarize
the app — Gatekeeper still warns on first open.

### A.0.2 Building it locally on a Mac instead

```bash
SELF_CONTAINED=true ./FemVoice.Avalonia/Packaging/macos/package-app.sh osx-arm64
./FemVoice.Avalonia/Packaging/macos/package-dmg.sh osx-arm64
```

`package-app.sh` substitutes the version from the csproj into `Info.plist` and refuses to emit a bundle
whose `CFBundleExecutable` does not name a real file in `Contents/MacOS` — the mismatch that previously
produced a bundle macOS could not launch at all.

---

### Manual reference (what the scripts above do)

### A.1 Publish the macOS binary

From the repo root (any OS with the .NET 10 SDK):

```bash
# Apple Silicon
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj \
  -c Release -r osx-arm64 --self-contained true -p:DebugType=None -o dist/osx-arm64

# Intel Macs
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj \
  -c Release -r osx-x64 --self-contained true -p:DebugType=None -o dist/osx-x64
```

Output: `dist/osx-arm64/FemVoice.Studio` (a raw Mach-O executable + native `.dylib`s). The file is
named after `<AssemblyName>FemVoice.Studio</AssemblyName>`, **not** after the project.

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
