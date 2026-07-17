# Avalonia — Shared UI library extraction (unblocks the Android APK) — Slice Report

Date: 2026-07-18 · Branch: `avalonia-shared-ui-library-slice` · Host: Linux (.NET 10 `10.0.110`; android workload + user-local JDK 21 + Android SDK).

## Goal

Extract the shared Avalonia UI into a platform-neutral **library** so BOTH heads (desktop Exe + Android) reference
it, and **the Android APK builds**. This is the architectural unblock identified in PR #46: a self-contained Android
app cannot consume the desktop **Exe** (`Avalonia.Desktop`, non-android RID).

## What changed (files)

- **New `FemVoice.Avalonia.UI/` (net10.0 library)** — `RootNamespace=FemVoice.Avalonia`, `AssemblyName=FemVoice.Avalonia.UI`,
  references `Avalonia` + `Avalonia.Themes.Fluent` + `Microsoft.Extensions.DependencyInjection` and **only**
  `FemVoice.Core` + `FemVoice.Audio.Abstractions` — **no `Avalonia.Desktop`, no `Avalonia.Headless`**. Holds all the
  moved UI (`git mv`, history preserved): `App.axaml(.cs)`, `MainWindow.axaml(.cs)`, `Views/`, `ViewModels/`,
  `Themes/`, `Localization/`, `Platform/`, `Preferences/`, `Theming/`, `Accessibility/`, `Audio/`.
- **New `FemVoice.Avalonia.UI/AppServices.cs`** — the DI composition (`BuildServices` + lazy `Services`) moved out of
  `Program.cs` so `App`/`MainWindow` reach it without depending on the Exe (resolves the old `App`↔`Program.Services`
  coupling). `App.axaml.cs` + `MainWindow.axaml.cs` now use `AppServices.Services`.
- **`App.axaml`** — the `avares://` theme URI updated to the new assembly: `avares://FemVoice.Avalonia.UI/Themes/ShellTheme.axaml`.
- **`FemVoice.Avalonia` (desktop Exe)** — now a **thin Exe**: `Program.cs` (entry + 41 smokes + snapshot) +
  `Packaging/`. References only `FemVoice.Avalonia.UI` (Core/Abstractions come transitively) + keeps
  `Avalonia.Desktop`/`Avalonia.Headless`/Fluent/DI/Tmds. `Services` delegates to `AppServices.Services`.
- **`FemVoice.Android`** — references `FemVoice.Avalonia.UI` (the library) instead of the desktop Exe.
- **`FemVoiceStudio.slnx`** — adds the UI library.
- **Smokes** — `InternalsVisibleTo("FemVoice.Avalonia")` on the library (smokes exercise a few internals);
  `--packaging-smoke` retargeted to assert the invariant on the **UI library** (exactly Core + Abstractions refs,
  **no `Avalonia.Desktop`**); the visual source-inspection smokes + `--android-readiness-smoke` +
  `--runtime-real-audio-activation-smoke` repointed at the UI-library source dir / `AppServices.cs`.

## Verification

- **Android APK builds** ✅ — `com.femvoice.studio-Signed.apk` (~88 MB), valid Android package, containing
  `AndroidManifest.xml`, `classes.dex`, and AOT `FemVoice.Avalonia.UI`/`FemVoice.Core`/`FemVoice.Audio.Abstractions`
  for arm64-v8a + x86_64. (Running still needs an emulator/device — not in this environment.)
- **Desktop unaffected**: build 0 err, **41/41 smokes**, portable **1570/1580** (baseline). Offscreen dashboard
  snapshot renders identically (XAML + `avares://` resources load from the new assembly). Windows `win-x64`
  cross-publish still produces `FemVoice.Avalonia.exe`.

## Invariants

The "Core + Abstractions only, no `Avalonia.Desktop`" leak-guard now lives on the **UI library** (the real shared
surface); the desktop Exe keeps `Avalonia.Desktop`/`Avalonia.Headless` (desktop-only concerns). No clinical/DSP/
scoring/Core/WPF change. Namespaces unchanged (`RootNamespace=FemVoice.Avalonia`), so every `x:Class` is intact.

## Follow-up

Android real-mic capture (AudioRecord/AAudio), mobile-responsive `ShellView`, app icon/splash/signing-for-Play,
and running the APK on an emulator/device remain deferred.
