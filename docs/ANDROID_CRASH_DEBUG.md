# Android APK crashes on launch — rebuild + debug guide (for a Windows AI assistant)

**Situation:** the signed APK (`builds/FemVoice-Android.apk`, package `com.femvoice.studio`) crashes
immediately when opened on a physical Android phone.

**A very likely cause was already found and fixed on `main`** (see “What was already fixed” below):
the SQLite database path used `Environment.SpecialFolder.MyDocuments`, which resolves to an **empty
string on Android**, so the app tried to create its database in a non‑writable directory and threw at
startup. The fix falls back to the app‑private writable folder on Android.

So the first thing to do is **pull `main` and rebuild the APK** — that alone will very likely fix the
crash. If it still crashes, this guide also shows how to capture the exact crash log with `adb logcat`.

> The user is on a **Windows PC**, not the main Linux dev box. All commands below are for Windows
> (PowerShell). Install the prerequisites, rebuild, install on the phone, test. Only if it still
> crashes, capture logcat and report the stack trace back.

---

## 1. Install prerequisites (Windows, PowerShell as needed)

Use `winget` (built into Windows 10/11). Run PowerShell and:

```powershell
# .NET 10 SDK (the app targets net10.0 / net10.0-android)
winget install Microsoft.DotNet.SDK.10

# Java (required to build Android apps) — Microsoft OpenJDK 21
winget install Microsoft.OpenJDK.21

# Android platform-tools (gives `adb`, for installing + reading crash logs)
winget install Google.PlatformTools

# Git (to get the repo, if not already installed)
winget install Git.Git
```

Then install the .NET **Android workload** (brings the Android build tooling; it can auto‑acquire the
Android SDK on first build):

```powershell
dotnet workload install android
```

Open a **new** PowerShell window afterwards so PATH updates take effect. Verify:

```powershell
dotnet --version        # should be 10.x
java -version           # should be 21.x
adb version             # Android Debug Bridge ...
```

If `dotnet build` later complains it cannot find the Android SDK or Java, set these (adjust paths):

```powershell
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"     # or wherever the SDK landed
$env:JAVA_HOME    = "C:\Program Files\Microsoft\jdk-21..."
```

(Installing **Android Studio** — `winget install Google.AndroidStudio` — is the easy way to get a
complete Android SDK + `adb` if the workload’s auto‑acquire is not enough. Run it once, let it install
the SDK, then use its SDK path for `ANDROID_HOME`.)

---

## 2. Get the code and build the fixed APK

```powershell
# Clone (or pull if you already have it)
git clone https://github.com/asarayja/femvoice_studio_priv.git
cd femvoice_studio_priv
git checkout main
git pull

# Build the signed Android APK (Release)
dotnet build FemVoice.Android\FemVoice.Android.csproj -c Release
```

Output APK:

```
FemVoice.Android\bin\Release\net10.0-android\com.femvoice.studio-Signed.apk
```

If the build cannot find the SDK/JDK automatically, pass them explicitly (adjust paths):

```powershell
dotnet build FemVoice.Android\FemVoice.Android.csproj -c Release `
  -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" `
  -p:JavaSdkDirectory="C:\Program Files\Microsoft\jdk-21..."
```

---

## 3. Put the app on the phone and test

On the **phone**: Settings → About phone → tap **Build number** 7× to unlock Developer options →
Settings → System → Developer options → enable **USB debugging**. Connect the phone to the PC by USB and
accept the “Allow USB debugging?” prompt.

```powershell
adb devices        # should list your phone (not "unauthorized")

# Install the freshly built APK (-r = replace existing)
adb install -r FemVoice.Android\bin\Release\net10.0-android\com.femvoice.studio-Signed.apk
```

Open the app on the phone. **If it now launches, you are done** — the fix worked. (Optionally rebuild
the other packages too; see the repo’s `builds/` folder and `docs/BUILD_*` for desktop.)

---

## 4. If it STILL crashes — capture the exact crash log

With the phone connected (USB debugging on):

```powershell
adb logcat -c                      # clear old logs
# now open the app on the phone so it crashes, then:
adb logcat -d *:E > crash.txt      # dump errors to a file
```

Or watch it live, filtered to the app + runtime:

```powershell
adb logcat -c
# launch the app, then:
adb logcat | Select-String -Pattern "AndroidRuntime|mono-rt|Avalonia|com.femvoice|FATAL|Exception"
```

Look for the block that starts with **`FATAL EXCEPTION`** (Java side) and/or an
**`Unhandled managed exception`** / .NET stack trace. That stack trace names the failing type/method.

**Report back:** paste the `FATAL EXCEPTION` block and the managed stack trace (the ~30 lines after it).
That pinpoints the cause precisely.

Useful extras:

```powershell
adb logcat -d *:E                              # all errors since boot
adb logcat -d | Select-String "com.femvoice"   # anything from our package
adb shell pm path com.femvoice.studio          # confirm it installed
```

---

## What was already fixed (context for the AI)

Commit on `main`: **DatabaseService is now Android‑safe.** `FemVoice.Core/Data/DatabaseService.cs`
gained `ResolveAppDataDir()`, used by the constructor and `ResetDatabase()`:

- Desktop (Windows/Linux/macOS): `MyDocuments` is non‑empty → path unchanged
  (`<Documents>/FemVoiceStudio/femvoice.db`).
- Android/mobile: `MyDocuments` is empty → falls back to `LocalApplicationData` → `Personal` →
  `AppContext.BaseDirectory` (all app‑private and **writable**), so `Directory.CreateDirectory` +
  SQLite init no longer throw at startup.

The Avalonia backup/diagnostics paths (`SettingsDataService.DefaultDbPath`,
`DiagnosticsViewModel.DataFolderPath`) were pointed at the same resolver.

### If the crash is something ELSE (per logcat)

Common Avalonia‑Android startup crashes to check against the stack trace:

- **File/permission** at another path (audio profile, preferences JSON) using `MyDocuments`/absolute
  paths — same class of bug; fall back to app‑private storage.
- **Missing microphone permission** at runtime — `AndroidManifest.xml` already declares
  `RECORD_AUDIO`, but Android 6+ needs a **runtime** permission request before capture; if the crash is
  in audio init, guard capture until permission is granted (it should not start at launch — capture only
  begins when the user presses Start).
- **Linker/AOT stripping** a reflection‑bound type — the project sets `AndroidLinkMode=None`
  (no trimming), so this is unlikely, but a `TypeLoadException`/`MissingMethodException` in the trace
  would point here.
- **Skia/native load** failure — would show as a native library load error in logcat.

Capture the logcat first; the stack trace tells you which of these (if any) it is. Then fix in the
shared code (`FemVoice.Avalonia.UI` / `FemVoice.Core`), rebuild per section 2, and re‑test.
