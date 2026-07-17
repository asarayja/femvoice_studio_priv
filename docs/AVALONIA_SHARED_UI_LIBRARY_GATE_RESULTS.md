# Avalonia — Shared UI library extraction — Gate Results

Date: 2026-07-18 · Branch: `avalonia-shared-ui-library-slice` · Host: Linux (.NET 10 `10.0.110`).

## Build
`dotnet build FemVoice.Avalonia` → **Build succeeded, 0 Error(s)** (thin Exe → FemVoice.Avalonia.UI → Core + Abstractions).

## Smokes (41 — all OK)
41/41 OK. Retargeted after the move: `--packaging-smoke` (leak-guard now on the UI library: exactly Core + Abstractions, **no Avalonia.Desktop**), the visual source-inspection smokes + `--android-readiness-smoke` + `--runtime-real-audio-activation-smoke` (repointed at `FemVoice.Avalonia.UI/` source + `AppServices.cs`). Offscreen snapshot renders (XAML + `avares://FemVoice.Avalonia.UI/...` load).

## Android APK
`dotnet build FemVoice.Android -c Release -p:AndroidSdkDirectory=… -p:JavaSdkDirectory=…` → **Build succeeded**; produces `com.femvoice.studio-Signed.apk` (~88 MB; `AndroidManifest.xml` + `classes.dex` + AOT `FemVoice.Avalonia.UI`/`Core`/`Audio.Abstractions` for arm64-v8a + x86_64). Running needs an emulator/device (not in this env).

## Desktop packaging
`win-x64` cross-publish still produces `FemVoice.Avalonia.exe`. `FemVoice.Android` kept out of the default Linux gate (needs the Android SDK).

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (documented baseline; Core untouched).

## Behaviour change
None. Pure structural refactor (shared UI → library) that unblocks the Android APK build. Namespaces/`x:Class` unchanged; no clinical/DSP/Core/WPF change.
