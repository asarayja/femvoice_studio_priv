# Avalonia — Android head bootstrap — Slice Report

Date: 2026-07-17 · Branch: `avalonia-android-head-bootstrap-slice` (off `main` @ `e69808f`) · Host: Linux (.NET 10 SDK `10.0.110`, `android` workload `36.1.2`, JRE-only Java, no Android SDK).

## Goal

Stand up the **4th platform**: an Avalonia **Android** head that reuses the same shared UI as the desktop head,
plus the desktop-verifiable enablers needed for single-view (mobile) hosting. Scope expanded by the user on
2026-07-17 to explicitly include Android (the earlier tracker prime directive said "No Android yet").

Because building an APK needs an Android SDK + a full JDK (neither installable in this sandbox — no root), this is
a **readiness/bootstrap** slice (same shape as the merged macOS packaging-readiness slices): the head is real and
restores cleanly; the shared enablers are fully verified on desktop; the APK build is documented as a provisioning
follow-up.

## What changed (files)

**Shared, desktop-verified (FemVoice.Avalonia):**
- **`Program.cs`** — `Services` is now **lazily** built (`_services ??= BuildServices()`) so the Android head, whose
  entry point is its `MainActivity` (not `Main`), still gets the shared DI container on first access.
- **`App.axaml.cs`** — added an **`ISingleViewApplicationLifetime`** branch: on mobile/single-view it sets the shared
  `ShellView` as the root `MainView` with the same `ShellViewModel`. Desktop branch (`MainWindow`) unchanged.
- **`Views/ShellView.axaml(.cs)` (new)** — the shell body (header · status strip · nav rail | content | info
  sidebar + all page `DataTemplate`s) **extracted from `MainWindow.axaml`** into a reusable `UserControl`, so BOTH
  heads render the identical shell.
- **`MainWindow.axaml`** — now a thin `Window` (chrome only, keeps its `ShellWindowBackgroundBrush`) hosting
  `<views:ShellView/>`. Code-behind unchanged (still sets `DataContext = ShellViewModel`, inherited by `ShellView`).
- **`Program.cs`** — new **`--android-readiness-smoke` (39th)**.

**New Android head (FemVoice.Android/, NOT in `FemVoiceStudio.slnx`):**
- `FemVoice.Android.csproj` — `net10.0-android`; references `Avalonia.Android` 11.2.1 + `FemVoice.Avalonia`.
- `MainActivity.cs` — `AvaloniaMainActivity<App>` (launcher) hosting the shared `App`.
- `Properties/AndroidManifest.xml` — declares **`RECORD_AUDIO`** for the future real mobile mic capture.
- `README.md` — provisioning + build/run steps and deferred follow-ups.

## Verification

- **Desktop build:** 0 errors. **Shell extraction proven** by build + **39/39 smokes** + a real 5 s GUI boot (window
  stayed alive, zero XAML load exceptions).
- **`--android-readiness-smoke`:** `diOk=True` (shared `ShellViewModel` resolves from the lazy container — the exact
  Android path) `headOk=True sharedOk=True gateIsolated=True` (head targets net10.0-android + refs Avalonia.Android +
  shared UI; MainActivity is `AvaloniaMainActivity<App>` launcher; manifest declares RECORD_AUDIO; App has the
  single-view branch hosting `ShellView`; head kept out of the Linux solution gate).
- **Android head restores cleanly** (`dotnet restore` resolves Avalonia.Android + the shared graph incl. the Android
  SQLite RID variant). APK build reaches the Android SDK stage and stops on **provisioning only** (`XA5300` no SDK;
  JRE missing `jar`) — not a project defect.
- **Portable tests:** 1570/1580 (documented baseline; 0 regressions). Core untouched.

## What did NOT change

No clinical/DSP/SmartCoach/recovery/Core/WPF change · no DB · no real capture wired · runtime still synthetic
display-only · `FemVoice.Avalonia` still references only Core + Audio.Abstractions · Android head kept out of the
Linux gate so cross-platform CI stays green without the Android SDK.

## Follow-up (deferred)

1. **Provision Android SDK + full JDK → build the APK → run on emulator/device** (steps in `FemVoice.Android/README.md`).
2. **Extract shared UI into a platform-neutral library** so the mobile head doesn't pull `Avalonia.Desktop`.
3. **Android real mic capture** behind `IAudioCaptureService` (AudioRecord/AAudio) + runtime permission request.
4. **Mobile-responsive layout** (collapsible nav for phones; the current shell is the desktop 3-column layout).
