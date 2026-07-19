# Ferdige bygg (Linux + Android)

Bygget fra `main`. Selvstendige — trenger ingen separat .NET-installasjon.

## Android — `FemVoice-Android.apk`
Signert APK (arm64 + x64), pakke `com.femvoice.studio`.
Kopier til telefonen, tillat «installer fra ukjente kilder», og åpne fila.

## Linux (x64) — `femvoice-studio_1.0.0_amd64.deb`
Debian/Ubuntu-pakke. Installer:
```
sudo apt install ./femvoice-studio_1.0.0_amd64.deb
```
Deretter finnes «FemVoice Studio» i appmenyen, eller kjør `femvoice-studio` i terminalen.
Avinstaller: `sudo apt remove femvoice-studio`.

Windows/macOS/iOS bygges på sine egne verter — se `docs/BUILD_WINDOWS.md` og `docs/BUILD_MACOS_IOS.md`.
