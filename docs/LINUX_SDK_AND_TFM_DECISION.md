# Linux SDK & Target-Framework Decision (Phase L1)

Date: 2026-06-16 · Host: Ubuntu 26.04, linux-x64.

## Decision summary
- **Shared/portable projects target `net10.0`** (plain, no `-windows`).
- **WPF head stays `net10.0-windows` + `UseWPF=true`** — Windows-only reference, **not built on Linux**.
- **`FemVoice.Audio.Windows` (future) stays `net10.0-windows`** — NAudio capture.

This was **not guessed** — it is backed by the installed SDK below.

## Installed SDK (`dotnet --info`)
The host had **no usable .NET SDK** initially (`dotnet` not on PATH; `~/.dotnet` held only a stray `corefx/` dir). Network was reachable, so the official `dotnet-install.sh` was used to install a user-local SDK under `~/.dotnet` (no root, reversible via `rm -rf ~/.dotnet`):

```
$ ~/.dotnet/dotnet --version
10.0.301

$ ~/.dotnet/dotnet --list-sdks
10.0.301 [/home/snalpy/.dotnet/sdk]

$ ~/.dotnet/dotnet --list-runtimes
Microsoft.AspNetCore.App 10.0.9
Microsoft.NETCore.App 10.0.9

.NET SDK: 10.0.301 | MSBuild 18.6.4 | OS: ubuntu 26.04 | RID: linux-x64
.NET workloads installed: (none)
```

### Consequences
- **`net10.0` is available** — the exact target the audit's portable core wants. **No TFM downgrade is needed.**
- **No WPF workload** (and none exists for Linux) → `net10.0-windows`/`UseWPF` projects **cannot build here**, as expected and as instructed ("Do not attempt to build the WPF app on Linux"). They remain the frozen Windows reference.
- **No Windows Desktop runtime** → no `Microsoft.Win32`, no WPF types on Linux.

## To use the SDK in this environment
The SDK is user-local; every shell must export:
```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
```
(The `scripts/linux-portable-gate.sh` gate script sets these.)

## TFM rationale
| Project | TFM | Reason |
| --- | --- | --- |
| `FemVoice.Core` | `net10.0` | UI-free domain; matches installed SDK; runs on Linux/macOS/Windows |
| `FemVoice.Audio.Abstractions` | `net10.0` | pure interfaces + DTOs + DSP |
| `FemVoice.Tests.Portable` | `net10.0` | portable xUnit tests; runnable on Linux |
| `FemVoiceStudio` (WPF) | `net10.0-windows`, `UseWPF` | unchanged frozen reference; built only on Windows |
| `FemVoice.Audio.Windows` (future) | `net10.0-windows` | NAudio capture |
| `FemVoice.Avalonia` (later) | `net10.0` | Avalonia desktop; cross-platform |

No `netstandard` fallback is required: `net10.0` is present, so shared libraries use it directly (richest API surface, matches the product's existing language/runtime level).
