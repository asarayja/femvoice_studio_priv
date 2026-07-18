# FemVoice.iOS — Avalonia iOS head

The 5th platform head. Reuses the **same shared UI library** (`FemVoice.Avalonia.UI`) as the desktop and Android
heads: `AppDelegate` bootstraps the shared `App`, whose single-view lifetime branch sets the shared `ShellView` as
the root `MainView` — so iPhone/iPad get the identical navigation, pages, theme and first-run onboarding.

Files:
- `FemVoice.iOS.csproj` — `net10.0-ios`, references `FemVoice.Avalonia.UI` + `Avalonia.iOS` 11.2.1, `MtouchLink=None`.
- `AppDelegate.cs` — `AvaloniaAppDelegate<App>` (mirrors the Android `MainActivity`).
- `Main.cs` — iOS managed entry point (`UIApplication.Main`).
- `Info.plist` — bundle id `com.femvoice.studio`, `NSMicrophoneUsageDescription`, orientations, launch screen.
- `Entitlements.plist` — empty (no special capabilities yet).

## Requires a Mac

Building/running iOS **requires macOS + Xcode + the iOS workload**. It is deliberately **not** in
`FemVoiceStudio.slnx` or `scripts/linux-portable-gate.sh`, so the Linux cross-platform gate stays green on machines
without the iOS toolchain (same rationale as the Android head).

```bash
# one-time, on the Mac
dotnet workload install ios

# Simulator (no signing needed) — best for validating full UI parity
dotnet build FemVoice.iOS/FemVoice.iOS.csproj -c Release -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64

# Physical device / .ipa (needs signing — see below)
dotnet publish FemVoice.iOS/FemVoice.iOS.csproj -c Release -f net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true -o ../dist/ios
```

## Signing reality (honest)

- **Simulator:** runs **unsigned**.
- **Your own iPhone:** free Apple ID → a **7-day personal** provisioning profile (set the Team in Xcode, or pass
  `-p:CodesignKey=...` / `-p:CodesignProvision=...`).
- **TestFlight / App Store:** require a **paid Apple Developer Program** membership. There is **no** self-signed
  path for iOS device distribution (an Apple restriction — the self-signed "Asarayja development" cert used for
  Windows/macOS does not apply here).

Full guide: `docs/BUILD_MACOS_IOS.md`.

## Audio

iOS has no ALSA, so the app uses the synthetic capture backend until a real iOS microphone backend is added behind
`IAudioCaptureService` in `FemVoice.Audio.Abstractions` (same abstraction/pattern as the Linux ALSA backend). All
non-audio features are identical to the other heads via the shared UI library.

## App icon

Add an `Assets.xcassets/AppIcon.appiconset` (or set `AppIcon` in the csproj) using the repo-root `logo.png` when
you have a Mac to generate the icon set; the app runs without it (default icon) in the meantime.
