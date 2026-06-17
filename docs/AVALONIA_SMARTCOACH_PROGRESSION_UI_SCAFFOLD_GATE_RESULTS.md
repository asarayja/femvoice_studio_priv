# SmartCoach & Progression UI Scaffold — Gate Results

Date: 2026-06-17 · Branch: `avalonia-smartcoach-progression-ui-parity-scaffold-slice` (off `main` @ `77f3ed3`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> UI scaffold only — display-only, synthetic, deferred. No engines/scoring/safety-gate/analytics/persistence; no
> clinical/domain or WPF behaviour change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (25 — all OK, all exit 0)
`--smoke` … `--exercise-guide-filter-search-smoke` (24 prior) + **`--smartcoach-progression-ui-scaffold-smoke` (new, 25th)** → **25/25 OK.**
- `--smartcoach-progression-ui-scaffold-smoke`: `progNav=True coachNav=True navIntact=True backToDash=True noServiceDeps=True coachDeferred=True progDeferred=True`.
- `--shell-smoke` (extended): nav 9 (implemented=6, deferred=3); Mikrofonkalibrering → static placeholder (inert); progression/smartcoach scaffolds open inert; runtime lifecycle intact. OK.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Forbidden WPF/audio/database/clinical tokens (non-comment) incl. **`SmartCoachEngine`, `ProgressionSafetyGate`,
  `VoiceHealth`, `VocalHealthSupervisor`, `RecoveryScorer`, `RecoveryIntelligenceService`, `SessionAnalyticsStore`,
  `ExerciseSessionRecorder`, `IDatabaseService`**: **clean**. (A forbidden token initially appeared in a runtime
  SafetyNote string and was reworded; doc-comment mentions are excluded by the guard's comment filter.)

## Packaging verification (readiness intact)
- `publish-linux.sh linux-x64` → OK; **11/11 published smokes exit 0** (incl. `--smartcoach-progression-ui-scaffold`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK; `package-app.sh osx-x64` → unsigned `.app`; `package-dmg.sh osx-x64` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures); **1569/1580**
acceptable when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake fires. No new
failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Two deferred surfaces (Progresjon, SmartCoach) gained
display-only scaffold pages (cards/tiles/disabled controls/synthetic "—" placeholders) replacing the bare generic
placeholder. No engine/scoring/safety-gate/progression/analytics/persistence.
