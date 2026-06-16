# Avalonia Exercise Runtime Lifecycle UI — Slice Plan

Date: 2026-06-16 · Branch: `avalonia-runtime-lifecycle-ui-slice` (off `main` @ `8f464ac`, incl. PR #1–#11).

> **Status: IMPLEMENTED (Linux-verified, headless).** Display-only runtime lifecycle UI. No clinical/domain
> behaviour changed · no WPF behaviour changed · no Android/iOS · no real mic · no persistence · no session
> saving · no SmartCoach/progression · no safety-gate enforcement · no Voice-Health/recovery decisions ·
> runtime lifecycle is synthetic/display-only. See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Bring the Avalonia Exercise Runtime screen's lifecycle toward WPF parity: an explicit inactive → Start →
active → Stop → session-ended flow, with display-only readouts — without persistence, real mic, clinical
enforcement, SmartCoach/progression, or Voice-Health/recovery.

## 2. Key change: explicit start (no auto-start)
Previously the runtime VM auto-started in its constructor. It now starts in an **Inactive** phase; the user
presses **Start** (`BeginCommand`) to run the synthetic session. This delivers the "inactive state before
start" + "explicit Start" the slice asks for and matches the WPF `ExerciseWindow` lifecycle. The
`FrameAvailable` handler is still subscribed once in the ctor and only fires while capture is started (in
Begin); re-Start does not re-subscribe (no duplicate subscriptions).

Because 6 existing smokes constructed a runtime and expected it already running, each was updated to call
`BeginCommand` explicitly after construction/navigation (mechanical; behaviour they test is unchanged).

## 3. Phase model
`RuntimePhase { Inactive, Active, Stopped }` (`[ObservableProperty] Phase`), with derived `IsInactive` /
`IsStopped` / `PhaseText`, and `OnPhaseChanged` forwarding. Begin → Active (resets + clears summary);
Stop → Stopped (builds the session-ended summary, then clears the live stream); re-Begin → fresh Active.

## 4. New display-only readouts
- **RecommendedDurationText**: from `Exercise.DurationMinutes` (read-only) — "Anbefalt varighet: N min (veiledende)".
- **SessionEndedSummary**: built on Stop from the last live values — duration (`ElapsedText`), best hold
  (`_peakHoldPercent`, tracked per frame), plus the not-saved note. Display-only string.
- **NotSavedNote**: static "Økten lagres ikke — visning-bare syntetisk kjøring."
- **Lifecycle bar** + **Inactive hint panel** (IsVisible=IsInactive) + **Session-ended panel** (IsVisible=IsStopped).

## 5. View structure
`ExerciseRuntimeView.axaml`: the live content (pitch / chart / feedback / hold / coordinator / guidance) is
wrapped in a `StackPanel IsVisible="{Binding IsRunning}"` so it shows only while Active. Below it, a lifecycle
control `Border` (theme-brushed): phase text + recommended duration + elapsed + Start/Stop buttons, an inactive
hint, a session-ended summary, and the not-saved note. Converter-free; new chrome uses the shell theme brushes.

## 6. Lifecycle safety (preserved + strengthened)
Stop stops synthetic capture + clears the stream; the `FrameAvailable` handler is unsubscribed only in Dispose;
re-Start is fresh with no duplicate subscription; nav-away disposes the transient runtime (stops capture, no
orphan frames). Dashboard/Guide/Settings behaviour unchanged. Verified by `--runtime-lifecycle-smoke` (and the
prior runtime/shell/settings smokes still pass).

## 7. Forbidden / not done
No session persistence, `ExerciseSessionRecorder`, SQLite/`IDatabaseService`, SmartCoach/progression, safety
gates, Voice-Health/recovery, real mic/NAudio/Wasapi/WaveIn, microphone calibration; no exercise-definition/
target-profile/WPF change.

## 8. Smoke (`--runtime-lifecycle-smoke`)
Headless: initial Inactive (no auto-start); Start → Active + flowing synthetic stream; Stop → Stopped +
cleared stream + session-ended summary (contains "lagres ikke"); re-Start → fresh Active (summary cleared) +
no orphan frames after a second Stop (no duplicate subscription); nav-away disposes (stops, no orphan frames).

## 9. Gate
`dotnet build` (0 warnings) · all 11 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580; 1569 acceptable due to the known ComfortZone flake) · leak guard clean (base +
lifecycle-specific) · Windows CI via PR.
