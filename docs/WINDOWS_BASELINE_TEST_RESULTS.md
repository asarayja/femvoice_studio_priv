# Windows Baseline Test Results (Agent 2)

Status: **⛔ PENDING — must run on a Windows host. NOT run on this Linux machine; no results invented.**

## Why pending
The WPF app (`FemVoiceStudio`, `net10.0-windows` + `UseWPF`) and `FemVoiceStudio.Tests` (`net10.0-windows`) require the Windows Desktop/WPF runtime + targeting packs, which do not exist on Linux. This workstation is Ubuntu with a user-local `~/.dotnet` SDK and no WPF workload. Per the work order, Windows verification "must happen on a Windows host" — it has not, and these fields are left blank rather than fabricated.

## Runbook (fill in on Windows)
```
OS:                 <Windows version>
dotnet --info:      <paste>
SDK version:        <e.g. 10.0.3xx>  (must include Desktop/WPF workload)
Workloads:          <paste dotnet workload list>
Branch:             linux-portable-core
Commit:             7abf648 (or HEAD at run time)
Restore result:     <dotnet restore FemVoiceStudio.slnx>
Build result:       <dotnet build FemVoiceStudio.slnx -c Debug>
Test result:        <dotnet test FemVoiceStudio.slnx -c Debug — totals>
Failing tests:      <list>
Skipped tests:      <list>
Warnings:           <notable>
Manual notes:       <e.g. app launches, mic capture works>
```

## What to scrutinize first (must be green or explained)
SafetyOverrideInvariantTests, SafetyPriorityEngineTests, ManualOverrideClampTests, FeedbackPriorityMatrixTests, FeedbackConsistencyGuardTests, ProgressionSafetyGateTests, RecoveryAwareTargetZoneTests, ReportAssemblerTests, ExportWriterTests, ResearchNoPiiTests, SmartCoach* tests, Recovery* tests, FemVoiceScore* tests.

> Note: the **portable** subset of these already runs **green on Linux** (1570/1580 in `FemVoice.Tests.Portable`; the 10 failures are pre-existing localization-data issues unrelated to the port — see `LINUX_PORTABLE_GATE_RESULTS.md`). On Windows they execute against the same `FemVoice.Core` assembly, so the safety invariants are already exercised cross-platform.

## Expected outcome (prediction, to be confirmed on Windows)
Build should succeed: WPF references `FemVoice.Core` / `FemVoice.Audio.Abstractions` / `FemVoice.Audio.Windows`; namespaces were preserved (no call-site changes); RESX moved with `RootNamespace=FemVoiceStudio` preserving the manifest base name; the WPF app uses **no** `internal` members of moved Core types (verified statically). The 30 Windows-only tests should behave as before. Any deviation is a mechanical compat issue → record it and route to `WPF_SHARED_CORE_COMPATIBILITY_NOTES.md`.
