# Avalonia Exercise Runtime Lifecycle UI — Gate Results

Date: 2026-06-16 · Branch: `avalonia-runtime-lifecycle-ui-slice` · Host: Linux (.NET SDK 10, user-local `~/.dotnet`).

All commands from the repo root with `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (`dotnet run --project FemVoice.Avalonia --no-build -- --<smoke>`)
| Smoke | Result |
| --- | --- |
| `--smoke` | **OK** |
| `--dashboard-smoke` | **OK** |
| `--exercise-smoke` | **OK** |
| `--exercise-runtime-smoke` | **OK** (updated: explicit BeginCommand) |
| `--exercise-runtime-integration-smoke` | **OK** (updated: explicit BeginCommand) |
| `--exercise-coordinator-smoke` | **OK** (updated: explicit BeginCommand) |
| `--runtime-chart-feedback-smoke` | **OK** (updated: explicit BeginCommand) |
| `--shell-smoke` | **OK** (updated: explicit BeginCommand) |
| `--theme-loc-smoke` | **OK** |
| `--settings-smoke` | **OK** (updated: explicit BeginCommand) |
| `--runtime-lifecycle-smoke` | **OK** |

### `--runtime-lifecycle-smoke` output
```
[lifecycle] phases: inactive=True active=True stopped=True
[lifecycle] stream: active-samples=13 cleared-on-stop=True
[lifecycle] summary: 'Økt fullført (kun visning) · varighet 0:00 · beste hold 11 %. Økten lagres ikke — visning-bare syntetisk kjøring.'
[lifecycle] re-start: active=True flowing=True no-orphan-after-stop=True
[lifecycle] nav-away: ran=True disposed=True no-orphan-frames=True
[lifecycle] Runtime lifecycle smoke OK
```
Verifies: initial **Inactive** (no auto-start); Start → **Active** with a flowing synthetic stream; Stop →
**Stopped** with a cleared stream and a display-only session-ended summary (contains "lagres ikke"); re-Start
→ fresh Active (summary cleared) with **no orphan frames after a second Stop** (no duplicate subscription);
nav-away disposes the runtime (stops, no orphan frames). The 6 updated smokes confirm the explicit-start change
preserves their tested behaviour.

## Vulnerability scan
`dotnet list … --vulnerable --include-transitive` → **no vulnerable packages.** `Tmds.DBus.Protocol` pinned **0.21.3**.

## Project references
`FemVoice.Avalonia` references only **`FemVoice.Core`** + **`FemVoice.Audio.Abstractions`**.

## Leak guard
Base forbidden list → **CLEAN (zero real references)** (only the two pre-existing negation comments match & are excluded).

Lifecycle-specific list (`SQLite`, `IDatabaseService`, `ExerciseSessionRecorder`, `.Save(`, `Persist`,
`SmartCoachEngine`, `ProgressionSafetyGate`, `VoiceHealth`, `VocalHealthSupervisor`, `RecoveryScorer`,
`RecoveryIntelligenceService`, `MicrophoneCalibration`, `NAudio`, `Wasapi`, `WaveIn`) → **CLEAN (zero real references)**.
(The `--runtime-lifecycle-smoke` deliberately does NOT embed these token strings in a reflection check, to keep
the grep unambiguous; absence of persistence APIs on the runtime VM is covered by the source leak guard.)

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **Passed: 1570, Failed: 10, Total: 1580** (known baseline; 1569/1580 is
the documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake variant). No new failures.

## Windows CI
Pending PR (`windows-wpf-verification.yml`). Avalonia-only changes; WPF build unaffected.

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** The only change is display-only (runtime no longer
auto-starts; new display-only lifecycle states) confined to the Avalonia runtime screen.
