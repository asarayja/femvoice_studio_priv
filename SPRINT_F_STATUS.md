# Sprint F — Predictive Voice Intelligence (Live Voice Load Monitor) — Status

**Date:** 2026-06-11
**Scope built:** the conservative, non-medical MVP core (VoiceLoadScore, PauseRecommendationLevel, HydrationContextLevel, SessionTrendSummary, RecoveryReadiness, anti-spam gentle messaging, evidence logging) + live-session wiring. Reads EXISTING signals only; no ML; no new low-level signal processing; no medical/dehydration claims.
**Environment:** Linux/headless — the WPF app and `dotnet test` CANNOT run here. C# was **compile-verified** (`dotnet build -p:EnableWindowsTargeting=true`, both projects, 0 errors) and the pure engine was **behaviour-verified** against all 7 validation scenarios via a standalone net10.0 harness compiling the real source. **Build/test/runtime are NOT validated on Windows — the user must do that.**

> Boundaries respected: no change to audio capture, pitch detection, resonance extraction, signal-level classification, clinical scoring, the SmartCoach core, Voice Health policy, the hydration feedback guard, RC-0 diagnostics, or the Sprint E professional layer. Sprint F only READS their outputs and adds advisory guidance. It never overrides a safety lock — it only recommends. Priority order Safety > Voice Health > Recovery > Comfort > Voice Development > Reporting is honoured (the band jumps straight to PauseRecommended only when a safety/health rule has already fired).

## Sprint F Status: **Needs Additional Work** (engine Implemented & wired; UI/report surface deferred)

- **Implemented:** VoiceLoadScore (0–100) + bands, PauseIntelligenceEngine, SessionTrendAnalyzer, RecoveryReadiness, HydrationContextLevel, anti-spam GentleCoachMessaging (localization keys), VoiceLoadEvidence, the stateful LiveVoiceLoadMonitor, and live per-tick wiring in ExerciseSessionRecorder with throttled evidence logging. nb source strings added.
- **Missing (deferred, documented):** user-facing UI display of the messages/score (Agent 9) and the report section (Agent 10) — both decoupled from the live engine and unvalidatable on Linux. Cross-session recovery feed and a real pause/resume UI event (NotePauseTaken hook) are exposed but not yet called from the UI.
- **Blocked:** nothing.

## Files changed
**New:**
- `FemVoiceStudio/Models/VoiceLoad/VoiceLoadModels.cs`
- `FemVoiceStudio/Services/VoiceLoad/VoiceLoadScoreEngine.cs`
- `FemVoiceStudio/Services/VoiceLoad/VoiceLoadDecisions.cs`
- `FemVoiceStudio/Services/VoiceLoad/LiveVoiceLoadMonitor.cs`
- `FemVoiceStudio.Tests/VoiceLoadTests.cs`

**Modified:**
- `FemVoiceStudio/Services/ExerciseSessionRecorder.cs` — field `LiveVoiceLoadMonitor _voiceLoadMonitor` + `LatestVoiceLoadState`; `Reset` in `BeginSession`; new `ObserveVoiceLoad(state, decision, hydrationAdvice)` called per tick after `SubmitHealthFeedback`; throttled evidence logging to the `VoiceLoad` runtime-log area.
- `FemVoiceStudio/Resources/Strings.resx` — 10 nb-source keys (`VoiceLoad_*`).

## New models (Models/VoiceLoad)
- `VoiceLoadInputs` — immutable per-tick snapshot (Timestamp, SessionElapsedSeconds, StabilityScore, ResonanceScore, UsesResonanceSignal, PitchHz, Intensity, IsHoldingCorrectly, IsInComfortZone, IsSafetyLocked, FatigueScore, FatigueDetected, StrainScore, StrainDetected, PauseRecommendedByHealth, HydrationSuggestedByHealth, HydrationSuggestedByAdvisor, HealthStateRank).
- `VoiceLoadState` — VoiceLoadScore, VoiceLoadBand, ActiveVoicedSeconds, TimeSinceLastPauseSeconds, ExerciseCountInSession, TrendDirection, PrimaryLoadDrivers, Confidence, IsDataSufficient.
- `VoiceLoadRecommendation` — Pause, Hydration, Message?, SuppressionReason, Reasons.
- `GentleCoachMessage` — Category, LocalizationKey.
- `SessionTrendSummary` — TrendCategory, TrendConfidence, MainChanges, RecommendedAdjustmentKey.
- `RecoveryReadinessResult` — Readiness, MessageKey, Reasons.
- `VoiceLoadEvidence` — all evidence fields below.
- Enums: `VoiceLoadBand`, `VoiceLoadTrendDirection`, `PauseRecommendationLevel`, `HydrationContextLevel`, `RecoveryReadiness`, `SessionTrendCategory`, `GentleCoachCategory`.

## New services (Services/VoiceLoad)
- `LiveVoiceLoadMonitor` — stateful per-session monitor; rolling EMAs/counters, `Observe()` → state + gated recommendation, `Reset()`, `NotePauseTaken()`, `EvaluateRecoveryReadiness()`, `BuildSessionTrendSummary()`, `BuildEvidence()`.
- `VoiceLoadScoreEngine` — pure, explainable 0–100 score + drivers + band resolution (capped contributions; no single non-severe metric dominates; safety/health-only instant jump to PauseRecommended).
- `PauseIntelligenceEngine` — pure pause-level decision (None/Soon/Now/EndSession), defers to safety/health, never forces.
- `RecoveryReadinessEvaluator` — pure before/after-pause practice-readiness (Ready/ContinueGently/WaitLonger/EndForToday/InsufficientData).
- `GentleCoachComposer` + `VoiceLoadMessageKeys` — pure mapping to ONE message category + nb localization key.

## UI changes
- None wired. `ExerciseSessionRecorder.LatestVoiceLoadState` is exposed for a future subtle load panel / coach-message line (Agent 9). Default users should see only simple guidance; professional mode may later show score + drivers. **Deferred — WPF/runtime, unvalidatable on Linux.**

## Evidence fields (logged to the `VoiceLoad` runtime-log area; `VoiceLoadEvidence` record)
VoiceLoadScore, VoiceLoadBand, PauseRecommendationLevel, HydrationContextLevel, RecoveryReadiness, SessionTrend, VoiceLoadDrivers, MessageShown(category), MessageSuppressed, SuppressionReason, TimeSinceLastPauseSeconds, ActiveVoicedSeconds, ExerciseCountInSession, TrendConfidence. No free-text, no medical claims — bands/levels are enum names, drivers are stable codes.

## Reports
- **Not wired (deferred).** The professional reports are built from the stored `OutcomeProfile`, decoupled from the live session, so attaching a voice-load summary needs a persistence hop (store the session's voice-load summary, then the report reads it) — out of MVP scope and unvalidatable here. Documented as the next step; `BuildSessionTrendSummary()` + `VoiceLoadEvidence` already produce the content a report section would render.

## Tests
- **Added (source; NOT executed here):** `VoiceLoadTests.cs` — the 7 validation scenarios, band mapping (Theory), safety/health instant-jump, anti-spam rate-limiting, "engine emits only `VoiceLoad_` keys / no free-text", message-key prefix, and evidence-populated.
- **Updated:** none (all additive; both projects compile with 0 errors).
- **Passed/Failed:** UNKNOWN via xUnit (Windows runtime required). The underlying engine behaviour was **verified ALL-PASS** against the real source via a standalone net10.0 harness on Linux.

## Validation scenarios (harness result against real engine)
| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | Short stable session | PASS | band=LOW, pause=NONE, data sufficient |
| 2 | Long session without pause | PASS | band≥MODERATE, pause≥SOON |
| 3 | Increasing instability | PASS | trend=MildDecline/ClearDecline + Worsening, pause≥NOW |
| 4 | Pause taken | PASS | time-since-pause resets, score does not rise, recovery evaluates |
| 5 | Hydration reminder | PASS | GentleReminder shown, anti-spam (≤2), key-only (no dehydration text) |
| 6 | High load persists after pause | PASS | pause=END_SESSION, safe wording |
| 7 | Insufficient data | PASS | band=InsufficientData, no recommendation, no message |

## Known limitations
- Linux/headless: no WPF runtime, no `dotnet test`. Engine behaviour verified via harness; UI rendering + xUnit pass/fail must be validated on Windows.
- UI display (Agent 9) and report section (Agent 10) are NOT wired — deferred with a documented plan.
- `NotePauseTaken`/`EvaluateRecoveryReadiness` are exposed but not yet called from a pause/resume UI event, so "time since last pause" currently runs from session start until that hook is wired.
- ExerciseCountInSession is effectively 1 (the recorder persists one exercise per session).
- A few brief signals are off the per-tick path (pitch detection success rate, resonance rejected ratio); the engine derives load from the on-path signals and does not fabricate them.
- Recovery readiness uses the within-session before/after-pause comparison; the cross-session RecoveryScorer feed is available to pass in at session start but is not yet wired.

## Release recommendation: **Needs Additional Work**
The predictive engine MVP is implemented, conservative, boundary-safe, wired to the live session, and logging evidence — and all 7 validation scenarios pass against the real source. Before a Predictive Voice Intelligence Beta: wire the gentle-coach message into the live coach-message UI surface, add the report section (with the small persistence hop), optionally wire the pause/resume hook + cross-session recovery feed, and run `dotnet test` + manual WPF validation on Windows.
