# Ferdige bygg (Linux + Android)

Bygget fra `main`. Selvstendige — trenger ingen separat .NET-installasjon.

## Android — `FemVoice-Android.apk`
Signert APK (arm64 + x64), pakke `com.femvoice.studio`.
Kopier til telefonen, tillat «installer fra ukjente kilder», og åpne fila.

## Linux (x64) — `femvoice-studio_0.1.2_amd64.deb`
Debian/Ubuntu-pakke. Installer:
```
sudo apt install ./femvoice-studio_0.1.2_amd64.deb
```
Deretter finnes «FemVoice Studio» i appmenyen, eller kjør `femvoice-studio` i terminalen.
Avinstaller: `sudo apt remove femvoice-studio`.

Windows/macOS/iOS bygges på sine egne verter — se `docs/BUILD_WINDOWS.md` og `docs/BUILD_MACOS_IOS.md`.

## Arch Linux (x86_64) — `femvoice-studio-0.1.2-1-x86_64.pkg.tar.zst`
Ferdig pacman-pakke. Installer:
```
sudo pacman -U femvoice-studio-0.1.2-1-x86_64.pkg.tar.zst
```
(pacman kan advare om manglende MTREE — pakka installeres likevel.) Appen havner i menyen,
og `femvoice-studio` finnes på PATH. Avinstaller: `sudo pacman -R femvoice-studio`.

Vil du heller bygge selv på Arch (fra en klone av repoet): kjør `makepkg -si` i `builds/`
(bruker `PKGBUILD`; krever en .NET 10 SDK).
