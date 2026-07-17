# FemVoice.Android — Avalonia Android head

The **4th platform** head. It is a thin platform host: it references `FemVoice.Avalonia` and launches the **shared**
Avalonia `App`, whose single-view lifetime branch sets the shared `ShellView` as the root `MainView`. So the phone
renders the same navigation, pages, and theme as the desktop head — no UI or domain logic is duplicated here.

## Files
- `FemVoice.Android.csproj` — `net10.0-android`; references `Avalonia.Android` + `FemVoice.Avalonia`.
- `MainActivity.cs` — `AvaloniaMainActivity<App>` (the launcher activity).
- `Properties/AndroidManifest.xml` — declares `RECORD_AUDIO` for the future real cross-platform microphone capture.

## Status (2026-07-17)
- **Restores cleanly** on this Linux box (`dotnet restore` resolves `Avalonia.Android` + the shared project graph,
  including the Android SQLite RID variant). The shared single-view enablers are verified by the desktop gate.
- **APK build is blocked here on toolchain provisioning only** (not on this project):
  1. **Android SDK** not installed (`error XA5300`).
  2. The installed Java is a **JRE without full JDK tools** (`jar`/`javac` missing); a full JDK is required.
  Both need machine-level setup (root / large downloads) not available in the current sandbox.

## Provision + build/run (on a machine with root / Android tooling)
```bash
# 1. .NET Android workload (already installed on the dev box):
dotnet workload install android

# 2. A full JDK (provides jar/javac). E.g. on Debian/Ubuntu:
sudo apt-get install -y openjdk-21-jdk

# 3. Android SDK — let the .NET Android SDK fetch it + accept licenses:
dotnet build FemVoice.Android/FemVoice.Android.csproj -t:InstallAndroidDependencies \
  -p:AcceptAndroidSDKLicenses=True \
  -p:AndroidSdkDirectory="$HOME/android-sdk" -p:JavaSdkDirectory="$JAVA_HOME"

# 4. Build the APK:
dotnet build FemVoice.Android/FemVoice.Android.csproj -c Debug \
  -p:AndroidSdkDirectory="$HOME/android-sdk" -p:JavaSdkDirectory="$JAVA_HOME"

# 5. Run on an emulator/device:
dotnet run --project FemVoice.Android/FemVoice.Android.csproj \
  -p:AndroidSdkDirectory="$HOME/android-sdk" -p:JavaSdkDirectory="$JAVA_HOME"
```

## Deliberately deferred (follow-up slices)
- **Extract the shared UI into a library** (`FemVoice.Avalonia` currently references `Avalonia.Desktop`, which the
  mobile head does not need). Splitting the shared views/VMs/`App` into a platform-neutral library that both the
  desktop Exe and this Android head reference is the clean long-term structure; this bootstrap references the desktop
  head directly to prove the shell reuse first.
- **Android real microphone capture** behind `IAudioCaptureService` (AudioRecord/AAudio), mirroring the Linux/ALSA
  backend, plus the runtime `RECORD_AUDIO` permission request.
- **Mobile-responsive layout** (the current shell is the desktop 3-column layout; phones need a collapsible nav).
- App icon / splash / signing for Play distribution.

This head is intentionally **not** in `FemVoiceStudio.slnx` or `scripts/linux-portable-gate.sh`, so the
cross-platform Linux gate keeps building/testing without requiring the Android SDK.
