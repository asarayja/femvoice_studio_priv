# Exercise Guide Filter / Search Parity — Gate Results

Date: 2026-06-17 · Branch: `avalonia-exercise-guide-filter-search-parity-slice` (off `main` @ `9dc1a65`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Display/UI-only. No persistence/analytics/DB/session writes; no clinical/domain or WPF behaviour change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (24 — all OK, all exit 0)
`--smoke` … `--macos-icon-readiness-smoke` (23 prior) + **`--exercise-guide-filter-search-smoke` (new, 24th)** → **24/24 OK.**
- `--exercise-guide-filter-search-smoke`: `total=15 chips=6 chipsExist=True defaultAll=True categorySubset=True oneChipSelected=True clearCatAll=True searchByName=True combined=True emptyState=True clearSearchAll=True opensExercise=True noTargetHzInRows=True`.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Forbidden WPF/audio/database/clinical/analytics tokens (non-comment) incl. `SessionAnalyticsStore`,
  `IDatabaseService`, `ExerciseSessionRecorder`: **clean**.

## Packaging verification (readiness intact)
- `publish-linux.sh linux-x64` → OK; **10/10 published smokes exit 0** (incl. `--exercise-guide-filter-search`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK; `package-app.sh osx-x64` → unsigned `.app` (icon deferred); `package-dmg.sh osx-x64` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures); **1569/1580**
acceptable when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake fires. No new
failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Exercise Guide list-level UI: category-filter chips + a
name/description search + empty state, all display-only over the in-memory list. No persistence/analytics/DB.
