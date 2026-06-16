# Dependency Security Notes

Tracks security-driven dependency changes. No clinical/domain behaviour is changed by anything here.

## NU1903 — Tmds.DBus.Protocol (GHSA-xrw6-gwf8-vvr9)

Date: 2026-06-16 · Branch: `deps-nu1903-tmds-dbus` (off `main`). Isolated dependency-only change — not combined with clinical/audio/dashboard/scoring/SmartCoach/recovery/reporting/diagnostics/localization/persistence work.

```
Package:                 Tmds.DBus.Protocol
Old resolved version:    0.20.0  (transitive, High severity — GHSA-xrw6-gwf8-vvr9)
New resolved version:    0.21.3  (direct pin; lowest patched version per advisory)
Dependency path:         FemVoice.Avalonia
                           -> Avalonia.Desktop 11.2.1
                             -> Avalonia.FreeDesktop 11.2.1   (Linux DBus integration)
                               -> Tmds.DBus.Protocol 0.20.0   (now overridden by the direct 0.21.3 pin)
Fix applied:             Option B — added a direct <PackageReference Include="Tmds.DBus.Protocol"
                         Version="0.21.3" /> to FemVoice.Avalonia/FemVoice.Avalonia.csproj. A direct
                         reference wins over the transitive resolution, so the patched 0.21.3 is used.
Why this fix was chosen: Lowest safe version (advisory lists 0.21.3 and 0.92.0+ as fixed). Surgical and
                         isolated: Avalonia 11.2.x still ships Tmds 0.20.0 (the Avalonia-side fix only
                         arrives in the 11.3.x line), so pinning the single transitive avoids a wider
                         Avalonia minor bump and touches no unrelated packages. Only FemVoice.Avalonia
                         (the Linux Avalonia head) is affected; the WPF app does not reference Avalonia
                         or Tmds.
Linux build result:      FemVoice.Avalonia — Build succeeded, 0 warnings (NU1903 gone), 0 errors.
Avalonia smoke result:   --smoke OK (shared FemVoice.Core services resolve via DI).
Portable test result:    FemVoice.Tests.Portable — 1570/1580 (10 pre-existing localization-data failures; no regression).
Windows build/test:      Not run on this Linux host. The WPF solution does not reference Avalonia/Tmds, so it
                         is unaffected; the Windows WPF Verification workflow re-runs on any PR.
Behavior changed:        no
Remaining warnings:      none from restore/build of FemVoice.Avalonia. (`dotnet list --vulnerable
                         --include-transitive` → "has no vulnerable packages".) The GitHub Actions Node-20
                         action-deprecation notice is a CI runner notice, unrelated to this package.
```

### Verification commands
```bash
dotnet restore FemVoice.Avalonia/FemVoice.Avalonia.csproj          # no NU1903
dotnet list FemVoice.Avalonia/FemVoice.Avalonia.csproj package --vulnerable --include-transitive   # none
dotnet build  FemVoice.Avalonia/FemVoice.Avalonia.csproj           # 0 warnings / 0 errors
dotnet run    --project FemVoice.Avalonia --no-build -- --smoke     # OK
dotnet test   FemVoice.Tests.Portable/FemVoice.Tests.Portable.csproj
```

### Caveat / NEEDS REVIEW
`--smoke` builds the DI container but does **not** start the Avalonia GUI, so it does not exercise the X11/FreeDesktop DBus path at runtime. Compatibility is confirmed at **build/assembly-resolution** level (FemVoice.Avalonia links against Tmds.DBus.Protocol 0.21.3 alongside Avalonia.FreeDesktop 11.2.1 with no errors). Full GUI/DBus runtime validation needs a display (manual or a Linux GUI CI). 0.21.3 is the patched continuation of the 0.20.0 line and is the version Avalonia's community guidance pins for this advisory, so runtime risk is low.

### Longer-term (separate, optional)
When the project later moves to the Avalonia **11.3.x** line (which already references a patched Tmds.DBus.Protocol), remove this direct pin (it becomes redundant).
