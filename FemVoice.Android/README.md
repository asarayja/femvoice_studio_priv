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
## Build investigation (2026-07-17) — toolchain provisions WITHOUT root; one architectural blocker remains

The toolchain was provisioned entirely to **user-local directories, no root**, and the APK build was driven until
it hit a single, well-understood architectural blocker (below). Steps that worked here:

```bash
# 1. .NET Android workload (user-local under ~/.dotnet):
dotnet workload install android

# 2. A FULL JDK to a user dir (JRE-only Java lacks jar/javac). No root — just extract a tarball:
curl -sL -o jdk.tgz "https://api.adoptium.net/v3/binary/latest/21/ga/linux/x64/jdk/hotspot/normal/eclipse?project=jdk"
tar -xzf jdk.tgz -C "$HOME/femvoice-toolchain"      # -> $HOME/femvoice-toolchain/jdk-21.x
export JAVA_HOME="$HOME/femvoice-toolchain/jdk-21.0.11+10"

# 3. Android SDK to a user dir via Google cmdline-tools + sdkmanager (no root):
#    download commandlinetools-linux zip -> $SDK/cmdline-tools/latest, then:
export SDK="$HOME/femvoice-toolchain/android-sdk"
yes | "$SDK/cmdline-tools/latest/bin/sdkmanager" --sdk_root="$SDK" --licenses
"$SDK/cmdline-tools/latest/bin/sdkmanager" --sdk_root="$SDK" "platform-tools" "platforms;android-34" "build-tools;34.0.0"

# 4. Build the APK (reaches the blocker below):
dotnet build FemVoice.Android/FemVoice.Android.csproj -c Release \
  -p:AndroidSdkDirectory="$SDK" -p:JavaSdkDirectory="$JAVA_HOME"
```

**Blocker (NETSDK1150 → NETSDK1047):** this head references `FemVoice.Avalonia`, which is a **net10.0 desktop Exe**
that pulls in `Avalonia.Desktop`. A self-contained Android app cannot consume a non-self-contained desktop Exe, and
that Exe cannot be re-targeted to an `android-*` RID (its restore has no android target, and `Avalonia.Desktop` is
desktop-only). Suppressing the self-contained check + disabling the apphost for android RIDs only surfaces the next
symptom — the real fix is structural:

**Required next step — extract the shared UI into a platform-neutral LIBRARY** (`FemVoice.Avalonia.UI`, net10.0,
references `Avalonia` + `Avalonia.Themes.Fluent` but **not** `Avalonia.Desktop`; holds `App`, `ShellView`, all
views/VMs, themes, localization, and the DI composition). Both heads then reference the library: the desktop Exe
(`Program.cs` + `Avalonia.Desktop`) and this Android head (`Avalonia.Android`). Only after that will the APK build.
Note the App↔`Program.Services` coupling must move into the library's DI as part of the extraction.

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
