# Avalonia — Windows-via-Avalonia RID readiness — Slice Report

Date: 2026-07-17 · Branch: `avalonia-windows-rid-readiness-slice` (off `main` @ `<post-#41>`) · Host: Linux (.NET 10 `10.0.110`).

## Goal

Make the Avalonia head the cross-platform **Windows** path. Previously its `RuntimeIdentifiers` listed only
linux/osx, so the only Windows story was the frozen WPF app. Adding the Windows RIDs lets the Avalonia app build
and run a real Windows executable (the WPF app stays the frozen Windows *reference*).

## What changed (files)

- **`FemVoice.Avalonia/FemVoice.Avalonia.csproj`** — `RuntimeIdentifiers` extended to
  `linux-x64;linux-arm64;osx-x64;osx-arm64;win-x64;win-arm64` (plural → default `build`/`run` stay portable and
  unchanged; only `publish -r <rid>` targets a platform). Behavior-neutral.
- **`FemVoice.Avalonia/Program.cs`** — `--packaging-smoke` RID assertion extended to require the two `win-*` RIDs.
- **`FemVoice.Avalonia/Packaging/README.md`** — documents the Windows RIDs + `win-x64`/`win-arm64` publish commands,
  and a follow-up note (`WinExe` output to avoid a console window on the Windows GUI launch).

## Verification

- **Cross-published a Windows executable from Linux**: `dotnet publish -r win-x64 --self-contained false` produced
  `FemVoice.Avalonia.exe` → `file` reports `PE32+ executable for MS Windows … x86-64`, alongside `Avalonia.Win32.dll`.
- Default portable build unchanged; **41/41 smokes** (packaging-smoke now asserts `RIDs(linux/osx/win x64+arm64)=True`).
- Portable tests 1570/1580 (baseline; Core untouched).

## Notes / follow-up

- The published `.exe` is currently **console-subsystem** (`OutputType=Exe`, needed for the `--*-smoke` console
  output). A follow-up can switch to `WinExe` (with console reattach for smokes) so the Windows GUI launch doesn't
  open a console window.
- Actually running the Windows binary needs a Windows host (not available here); the cross-publish proves it builds
  and links the Win32 backend.
