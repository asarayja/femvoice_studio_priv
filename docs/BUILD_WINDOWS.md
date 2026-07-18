# Building & signing FemVoice Studio for Windows

Self-contained Windows build of the Avalonia desktop head (`FemVoice.Avalonia`), code-signed with a
**self-created "Asarayja development" certificate**.

> The Avalonia desktop head can be **cross-published from Linux/macOS** (no Windows needed to *build*).
> A Windows machine is only needed to **sign** and **run/test** the `.exe`.

---

## 1. Publish the Windows executable

From the repo root, on any OS with the .NET 10 SDK:

```bash
# x64 (Intel/AMD). For ARM devices use -r win-arm64.
dotnet publish FemVoice.Avalonia/FemVoice.Avalonia.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:DebugType=None \
  -o dist/win-x64
```

Output: `dist/win-x64/FemVoice.Avalonia.exe` (self-contained — no .NET install required on the target).

- Trimming is intentionally **off** (Avalonia XAML/reflection would break); the exe is ~80–90 MB.
- To give the exe the app name/icon, it already uses `Assets/logo.ico` via `ApplicationIcon` in the csproj.

---

## 2. Create the self-signed "Asarayja development" certificate  *(Windows, PowerShell)*

Run PowerShell **as your normal user** (not admin) for a CurrentUser cert:

```powershell
# Create a code-signing certificate valid for 3 years
$cert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject "CN=Asarayja development" `
  -FriendlyName "Asarayja development" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -KeyUsage DigitalSignature `
  -KeyExportPolicy Exportable `
  -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(3)

# Export it to a password-protected .pfx (keep this file safe — it is your signing key)
$pwd = Read-Host "PFX password" -AsSecureString
Export-PfxCertificate -Cert $cert -FilePath "$env:USERPROFILE\AsarayjaDev.pfx" -Password $pwd

# Export the PUBLIC cert (.cer) — install this on any machine that should trust the signature
Export-Certificate -Cert $cert -FilePath "$env:USERPROFILE\AsarayjaDev.cer"
```

`$cert.Thumbprint` prints the thumbprint if you need it later.

---

## 3. Sign the executable

**Option A — PowerShell (no extra tools):**

```powershell
Set-AuthenticodeSignature `
  -FilePath "dist\win-x64\FemVoice.Avalonia.exe" `
  -Certificate $cert `
  -TimeStampServer "http://timestamp.digicert.com" `
  -HashAlgorithm SHA256
```

**Option B — signtool** (ships with the Windows SDK / Visual Studio), signing from the `.pfx`:

```powershell
signtool sign /f "%USERPROFILE%\AsarayjaDev.pfx" /p <PFX_PASSWORD> `
  /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  "dist\win-x64\FemVoice.Avalonia.exe"
```

The timestamp (`/tr`) keeps the signature valid after the cert expires.

Verify:

```powershell
signtool verify /pa /v "dist\win-x64\FemVoice.Avalonia.exe"
# or
Get-AuthenticodeSignature "dist\win-x64\FemVoice.Avalonia.exe" | Format-List
```

---

## 4. Make Windows trust the self-signed cert

A self-signed cert is **not trusted by default** — the signature shows as "unknown publisher" until the cert
is installed on the target machine. On each test machine, import the **public** `AsarayjaDev.cer` into two stores
(needs an elevated PowerShell for LocalMachine):

```powershell
Import-Certificate -FilePath "AsarayjaDev.cer" -CertStoreLocation Cert:\LocalMachine\Root              # Trusted Root
Import-Certificate -FilePath "AsarayjaDev.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPublisher  # Trusted Publisher
```

> **SmartScreen note:** even correctly signed, a self-signed cert has no Microsoft reputation, so Windows
> SmartScreen may still warn on first run ("More info → Run anyway"). Only an OV/EV code-signing cert from a
> public CA removes that. For development/internal distribution the self-signed cert is fine.

---

## 5. (Optional) MSIX package

For a proper installer, package as MSIX. The manifest **Publisher must exactly match the cert subject**:

```xml
<!-- Package.appxmanifest -->
<Identity Name="FemVoiceStudio" Publisher="CN=Asarayja development" Version="1.0.0.0" />
```

Build the MSIX (Windows Application Packaging Project or `makeappx`), then sign it the same way:

```powershell
signtool sign /f AsarayjaDev.pfx /p <PFX_PASSWORD> /fd SHA256 FemVoiceStudio.msix
```

Users install the same `AsarayjaDev.cer` into Trusted Root/Trusted Publisher before the MSIX will install.

---

## 6. Run / smoke-test

```powershell
dist\win-x64\FemVoice.Avalonia.exe            # launches the GUI
dist\win-x64\FemVoice.Avalonia.exe --shell-smoke   # headless self-check (exit code 0 = OK)
```

The Windows head uses the **real Windows audio backend** when available; if not, it falls back to the synthetic
source (same abstraction as Linux/Android). No source changes are needed vs. the Linux build — the same
`FemVoice.Avalonia` project targets `win-x64`/`win-arm64` via `RuntimeIdentifiers`.
