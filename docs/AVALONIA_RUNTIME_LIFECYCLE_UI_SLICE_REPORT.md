# Avalonia Exercise Runtime Lifecycle UI — Slice Report

Date: 2026-06-16 · Branch: `avalonia-runtime-lifecycle-ui-slice` (off `main` @ `8f464ac`).

> **Display-only runtime lifecycle UI.** No clinical/domain behaviour changed · no WPF behaviour changed ·
> no Android/iOS · no real mic · no persistence · no session saving · no SmartCoach/progression · no
> safety-gate enforcement · no Voice-Health/recovery decisions · runtime lifecycle is synthetic/display-only.

## 1. What this slice does
Adds an explicit inactive → Start → active → Stop → session-ended lifecycle to the Avalonia Exercise Runtime
screen, with display-only readouts (phase indicator, recommended duration, elapsed, session-ended summary,
not-saved note). The synthetic stream, chart, live feedback, coordinator readout, and nav-away disposal are
all preserved.

## 2. Behaviour change: explicit start (no auto-start)
The runtime VM no longer auto-starts in its constructor; it begins **Inactive** and the user presses **Start**
(`BeginCommand`). This is a display-only Avalonia-head change (no clinical/WPF change) matching the WPF
`ExerciseWindow` start/stop lifecycle. The 6 existing smokes that constructed a runtime and expected it
running were updated to call `BeginCommand` explicitly — the behaviour they test is unchanged and they pass.

## 3. Files changed
- **Edit** `FemVoice.Avalonia/ViewModels/ExerciseRuntimeViewModel.cs` — `RuntimePhase { Inactive, Active, Stopped }`
  + `Phase`/`IsInactive`/`IsStopped`/`PhaseText`; `RecommendedDurationText`, `SessionEndedSummary`, `NotSavedNote`;
  `_peakHoldPercent` tracking; ctor no longer auto-starts (Inactive); Begin → Active (+ reset summary); Stop →
  Stopped (+ build summary before clearing). Disposal/lifecycle unchanged otherwise.
- **Edit** `FemVoice.Avalonia/Views/ExerciseRuntimeView.axaml` — live content wrapped in `IsVisible="{Binding IsRunning}"`;
  new lifecycle control bar (phase + recommended duration + elapsed + Start/Stop) + inactive hint panel
  (`IsVisible=IsInactive`) + session-ended panel (`IsVisible=IsStopped`) + not-saved note, using shell theme brushes.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--runtime-lifecycle-smoke`; added explicit `BeginCommand` to the
  6 existing runtime-constructing smokes (`--exercise-runtime-smoke`, `--exercise-runtime-integration-smoke`,
  `--exercise-coordinator-smoke`, `--runtime-chart-feedback-smoke`, `--shell-smoke`, `--settings-smoke`).
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/`, or `FemVoice.Audio.Windows/`.

## 4. Lifecycle safety (verified)
Stop stops synthetic capture + clears the stream; the `FrameAvailable` handler is subscribed once (ctor) and
unsubscribed only in Dispose; re-Start is fresh with no duplicate subscription (verified: no orphan frames
after a second Stop); nav-away disposes the transient runtime (stops capture, trace stops growing). The prior
PR #7–#11 smokes still pass. Dashboard/Guide/Settings behaviour unchanged.

## 5. Display-only / forbidden behaviour
No persistence, `ExerciseSessionRecorder`, SQLite/`IDatabaseService`, SmartCoach/progression, safety gates,
Voice-Health/recovery, real mic/NAudio/Wasapi/WaveIn, or microphone calibration. The session-ended summary is
a display-only string built from the last live values; it is never saved. Recommended duration is read-only
from the exercise definition.

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 11 smokes OK (incl. `--runtime-lifecycle-smoke`) · no vulnerable packages · leak guard
clean (base + lifecycle-specific) · refs only Core + Audio.Abstractions · Tmds 0.21.3 · portable 1570/1580
(1569 known flake) · Windows CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched.** The only behaviour change is display-only and confined
to the Avalonia runtime screen: it no longer auto-starts (explicit Start), with new display-only lifecycle states.
