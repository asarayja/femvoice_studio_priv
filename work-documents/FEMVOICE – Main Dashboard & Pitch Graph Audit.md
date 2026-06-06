# FEMVOICE - Main Dashboard & Pitch Graph Audit

Status: 2026-06-06

Scope: `MainWindow`, `MainViewModel`, main-page pitch graph, start/stop recording flow, displayed values, localization, and clinical safety of dashboard feedback.

No engine/scoring code was modified during this audit.

## 1. UI Wiring Map

Main page files:

- `FemVoiceStudio/Views/MainWindow.xaml`
- `FemVoiceStudio/Views/MainWindow.xaml.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Audio/AudioAnalysisEngine.cs`
- `FemVoiceStudio/Audio/AudioAnalyzerService.cs`
- `FemVoiceStudio/Audio/PitchDetectionService.cs`
- `FemVoiceStudio/Services/PitchTargetZonePolicy.cs`
- `FemVoiceStudio/Services/PitchTraceStabilizer.cs`
- `FemVoiceStudio/Services/PitchChartAxisRangeCalculator.cs`
- `FemVoiceStudio/Services/FeedbackService.cs`
- `FemVoiceStudio/Services/LiveMetricsService.cs`
- `FemVoiceStudio/Services/FemVoiceScore.cs`
- `FemVoiceStudio/Resources/Strings*.resx`

Visible main page controls:

| UI area | Control/source | Binding/callback | Live-updated |
| --- | --- | --- | --- |
| Header | `TextBlock` title | hardcoded `FemVoice Studio` | No |
| Header subtitle | `TextBlock` | `{loc:Loc Main_TrainingForFeminineVoice}` | On localization refresh |
| Pitch chart title | `TextBlock` | `{loc:Loc Main_PitchGraph}` | On localization refresh |
| Target badge | `Run` values | `TargetMinPitch`, `TargetMaxPitch` plus hardcoded `Hz` | Yes when target zone changes |
| Stability badge | named controls | code-behind `UpdateStabilityIndicator()` from `PitchStability` | Yes |
| Health badge | named controls | code-behind `UpdateHealthIndicator()` from `HealthIndicator` | Yes |
| OxyPlot pitch graph | `PitchPlotView` | model created/updated in code-behind | Yes |
| Mini score panel | `Border` | `Visibility={Binding IsRecording}` | Recording only |
| Overall score | `TextBlock` | `OverallScore` as percent | Yes |
| Coach explanation | `TextBlock` | `CoachExplanation` | Yes, throttled |
| Resonance score | bar + text | `ResonanceScore` | Yes |
| Pitch score | bar + text | `PitchScore` | Yes |
| Intonation score | bar + text | `IntonationScore` | Yes |
| Voice health score | bar + text | `VoiceHealthScore` | Yes |
| Difficulty buttons | `Button` | `SetDifficultyCommand` with `Nybegynner`, `Middels`, `Avansert` | On click |
| Navigation buttons | `Button` | code-behind click handlers | On click |
| Mic status | `Run` | `StatusText` | Yes |
| Start button | `Button` | `StartRecordingCommand`; hidden when recording | On click |
| Stop button | `Button` | `StopRecordingCommand`; visible when recording | On click |
| Progress sidebar | text/progressbar | `ProgressionStatus`, `DifficultyText`, `CurrentStreak`, `TotalSessions` | After settings/session updates |
| Realtime feedback | `TextBlock` | `RealtimeFeedback`; visible when recording | Yes |
| Debug info | `TextBlock` | `DebugInfo`; visible only when non-empty | Debug setting only |
| Post-session feedback | `TextBlock` | `Feedback`; visible when not recording | After stop |
| Exercise text | `TextBox` | `CurrentExerciseText` | On exercise/language/difficulty |
| Error message | `TextBlock` | `ErrorMessage` | On errors |

Static vs localized:

- Localized: most labels, navigation buttons, start/stop, status labels, stability/health text, realtime feedback, axis titles in code-behind.
- Hardcoded: window title `FemVoice Studio`, header `FemVoice Studio`, `FemVoiceScore`, target separators, `Hz`, mic emoji, icon glyphs.
- Live-updated values: pitch graph, target min/max, realtime feedback, score bars, overall score, coach explanation, status, progression after session.

## 2. Start Session Flow

Start button flow:

```text
MainWindow.xaml Start button
  -> Command="{Binding StartRecordingCommand}"
  -> MainViewModel.StartRecording()
  -> DebugSettingsService.Reload()
  -> reset PitchHistory/RecentPitches/ScoreHistory/Feedback/OverallScore/live metrics/score values
  -> AudioAnalysisEngine.Initialize()
  -> AudioAnalysisEngine.Start()
  -> AudioAnalyzerService.StartAnalysis()
  -> IsRecording = true
  -> _uiUpdateTimer.Start()
  -> StatusText = UI_Recording
  -> MainWindow.OnViewModelPropertyChanged(IsRecording)
  -> ClearChart()
  -> reset PitchTraceStabilizer/chart timers
  -> _chartUpdateTimer.Start()
```

Live update flow after start:

```text
AudioAnalysisEngine audio frame
  -> FFT pitch detection
  -> confidence/RMS threshold
  -> PitchUpdated / SmoothedPitchUpdated events
  -> MainViewModel.OnPitchUpdated / OnSmoothedPitchUpdated
  -> CurrentPitch, SmoothedPitch, LivePitchUpdateSequence
  -> FeedbackService.GetRealtimeFeedback(...)
  -> CalculateLiveScore(...)
  -> MainWindow.OnViewModelPropertyChanged(...)
  -> RenderLatestPitchPoint()
  -> PitchTraceStabilizer.Filter(...)
  -> OxyPlot series/axis update
```

Parallel session-analysis flow:

```text
AudioAnalyzerService.StartAnalysis()
  -> AudioCaptureService.StartRecording()
  -> PitchDetectionService.DetectPitch(samples)
  -> PitchAnalyzed event
  -> MainViewModel.OnPitchAnalyzed()
  -> CurrentPitch/SmoothedPitch/CurrentIntensity/CurrentResonance/PitchStability/HealthIndicator
  -> RealtimeFeedback and live score
  -> history for StopAnalysis()
```

Stop flow:

```text
MainWindow.xaml Stop button
  -> StopRecordingCommand
  -> MainViewModel.StopRecording()
  -> _uiUpdateTimer.Stop()
  -> IsRecording = false
  -> AudioAnalysisEngine.Stop()
  -> DebugSettingsService.CloseLogs()
  -> AudioAnalyzerService.StopAnalysis()
  -> CalculateSessionAnalysis()
  -> FeedbackService.GenerateFeedback(...)
  -> DatabaseService.SaveTrainingSession(...)
  -> ProgressionService.EvaluateProgression(...)
  -> update StatusText/ProgressionStatus/streak/total sessions
  -> LoadNextExercise()
  -> MainWindow.OnViewModelPropertyChanged(IsRecording)
  -> _chartUpdateTimer.Stop()
  -> timeline pan/zoom enabled
```

Pause behavior:

- Main dashboard has no explicit pause command.
- Stop ends the session, saves analysis, evaluates progression, and loads the next exercise.
- The chart remains visible after stop and allows timeline pan/zoom.

## 3. Pitch Graph Logic

Pitch value source:

- Primary graph source is `MainViewModel.CurrentPitch` and fallback `SmoothedPitch`.
- `CurrentPitch` is written by both `AudioAnalysisEngine.PitchUpdated` and `AudioAnalyzerService.PitchAnalyzed`.
- `AudioAnalysisEngine` uses FFT peak/parabolic detection, confidence threshold, frame RMS threshold, calibration-aware minimum RMS, EMA/median smoothing.
- `AudioAnalyzerService` uses `PitchDetectionService`, which uses YIN with autocorrelation fallback and calibrated `VoicedRmsThreshold`.

Unit:

- Internal pitch unit is Hz.
- Main graph y-axis is labeled `Frekvens (Hz)`.
- Target badge displays raw Hz values.
- Realtime feedback includes raw Hz in under/inside/over-zone messages.

Normalization:

- Main graph does not normalize pitch; it plots Hz.
- Score bars are 0-100 values from `FemVoiceScore`.
- Stability is enum-derived from recent pitch standard deviation.
- Health is enum-derived from simplified pitch/intensity strain thresholds.

Graph range:

- X-axis is seconds, default visible window 30 seconds, review maximum 600 seconds.
- Y-axis absolute range is 60-500 Hz.
- `PitchChartAxisRangeCalculator` chooses visible range from current visible pitch values plus target min/max with padding.
- Minimum y-axis range is 50 Hz.
- Target zone is a green `RectangleAnnotation` using `ActivePitchTargetZone`.

Target zone:

- `PitchTargetZonePolicy.ForDifficulty()`:
  - Beginner: 160-230 Hz
  - Intermediate: 165-230 Hz
  - Advanced: 175-240 Hz
- `ClampForDifficulty()` caps requested exercise target zones and prevents advanced max above 240 Hz.
- This is clinically safer than the older 165-255/300 style ranges.

Smoothing/filtering:

- `AudioAnalysisEngine` smooths pitch internally.
- `LiveMetricsService.CalculateSmoothedPitch()` applies another EMA.
- `MainWindow.RenderLatestPitchPoint()` uses `PitchTraceStabilizer.Filter()` on the raw graph value.
- `PitchTraceStabilizer` rejects values below 60 Hz, rejects first values above 340 Hz, corrects likely 2x/3x/4x harmonic spikes, and blocks very fast jumps above 90 Hz within 250 ms.

Silence/unvoiced handling:

- `AudioAnalysisEngine` skips frames under calibrated `_minimumFrameRms` and emits no pitch event.
- `PitchDetectionService` returns `IsVoiced=false`, `Pitch=0`, `Confidence=0` when RMS is under `VoicedRmsThreshold`.
- `RenderLatestPitchPoint()` does not add graph points for `rawPitch <= 0`.
- Realtime feedback can show signal/mic calibration text when `OnPitchAnalyzed` receives unvoiced low-RMS frames.

Update model:

- Graph updates are both event-driven and timer-assisted.
- Property changes for `CurrentPitch`, `SmoothedPitch`, and `LivePitchUpdateSequence` call `RenderLatestPitchPoint()`.
- `_chartUpdateTimer` also calls `RenderLatestPitchPoint()` every 33 ms while recording.
- Duplicate rendering is throttled by sequence, last pitch, and 100 ms timing.

## 4. Clinical Correctness Review

What is clinically good:

- Pitch target policy now caps advanced target max at 240 Hz.
- Realtime feedback no longer tells the user to force pitch upward.
- Over-zone feedback explicitly says to ease down and avoid pressure.
- In-zone feedback mentions comfort and resonance.
- Graph colors distinguish in-zone, out-of-zone, and health warning states.
- Harmonic spike filtering reduces false visual pressure from pitch detection errors.
- Score algorithm weights resonance above pitch and penalizes high pitch without resonance support.

Risks:

1. Main page is visually pitch-first.
   - The largest central element is the pitch graph.
   - Raw Hz axis, target badge, and Hz-based realtime text can still invite number watching.
   - This is not automatically unsafe, but it is a production risk for a voice feminization tool unless framed clearly as supportive biofeedback.

2. Resonance score on the main page is a proxy, not true formant analysis.
   - `MainViewModel.CalculateLiveScore()` supplies fixed `AverageF1=500`, `AverageF3=2500`, and estimates F2 from `RmsValue * 5000`.
   - `LiveMetricsService.EstimateResonance()` also lets pitch brighten the resonance proxy.
   - Showing this as `ResonanceScore` may overstate clinical meaning.

3. Health on the main page is simplified.
   - `HealthIndicator` is derived from intensity and pitch thresholds in `LiveMetricsService`, not from the full `VocalHealthSupervisor`.
   - It is useful as a lightweight warning, but should not be presented as full vocal health assessment.

4. Two pitch pipelines can disagree.
   - FFT/event pitch and YIN/session pitch both write `CurrentPitch`.
   - Realtime feedback may come from `ActivePitchTargetZone` in one path and `ComfortZone` in another.
   - This can create inconsistent feedback if the two event streams interleave.

5. Raw Hz remains visible.
   - Target badge and axis display exact Hz.
   - Existing project guidance generally wants pitch as comfort/context, not a primary success metric.

Clinical conclusion:

- The current pitch graph is safer than before, but not production-perfect.
- It is acceptable as a measurement/debug-style dashboard if the app frames it as supportive feedback.
- Before production, main dashboard should reduce number-chasing risk and avoid overstating proxy resonance/health values.

## 5. Value Validation

| Displayed value | Source | Scale | Meaning | Processing | Appropriate to show directly? |
| --- | --- | --- | --- | --- | --- |
| Pitch graph line | `CurrentPitch` fallback `SmoothedPitch`; mostly `AudioAnalysisEngine` | Hz | Detected fundamental frequency over time | FFT/YIN sources, smoothing, graph-level harmonic filtering | Caution. Useful, but raw Hz can encourage number chasing |
| Target zone | `ActivePitchTargetZone` | Hz | Difficulty/exercise clamped comfort target | `PitchTargetZonePolicy` | Caution. Safer ranges, but exact Hz target badge increases pressure risk |
| Stability badge | `PitchStability` | enum | Pitch steadiness over recent window | recent-pitch stddev in `LiveMetricsService` | Yes, if described as stability not success |
| Health badge | `HealthIndicator` | enum | Lightweight strain/monitor state | pitch/intensity threshold proxy | Caution. Should be described as signal monitor, not full health diagnosis |
| Realtime feedback | `RealtimeFeedback` | text | Under/inside/over target zone or mic cue | `FeedbackService` + RESX | Mostly safe. Still includes raw Hz |
| Overall score | `OverallScore` | 0-100% | Composite score | `FemVoiceScore` live input | Caution. Live main-page inputs are simplified/proxy |
| Resonance score | `ResonanceScore` | 0-100 | Resonance/brightness proxy in live dashboard | fixed F1/F3 + estimated F2/spectral proxy | Not fully appropriate as direct clinical resonance score |
| Pitch score | `PitchScore` | 0-100 | Fit to target zone and pitch stability | `FemVoiceScore` | Caution. Should not dominate UI |
| Intonation score | `IntonationScore` | 0-100 | Variation/range proxy | pitch variance-derived | Caution. Live estimate is rough |
| Voice health score | `VoiceHealthScore` | 0-100 | Strain/health proxy | pitch/intensity-derived | Caution. Should avoid medical certainty |
| Coach explanation | `CoachExplanation` | text | SmartCoach explanation | `AdaptiveComfortZoneService.GenerateExplanation()` | Needs manual text audit per language |
| Session time | chart x-axis | seconds | elapsed recording time | `DateTime.Now - _chartSessionStartTime` | Yes |
| Progression percent | `ProgressionStatus.ProgressPercentage` | 0-100 | progress toward level | `ProgressionService` | Yes, but must not encourage overtraining |
| Streak/total sessions | DB/progression | count | training history | persisted sessions | Yes |
| Exercise text | `CurrentExerciseText` | localized text | current reading exercise | `ExerciseTextService` | Yes |
| StatusText | `StatusText` | localized text | mic/recording/session status | VM updates | Yes |

## 6. Localization / Text Audit

Main page localized text:

- `Main_TrainingForFeminineVoice`
- `Main_PitchGraph`
- `Main_Target`
- `Main_TimeSec`
- `Main_FrequencyHz`
- `Main_YourPitch`
- `Stability_*`
- `Health_*`
- `Dashboard_Resonance`
- `Dashboard_Pitch`
- `Dashboard_Intonation`
- `Dashboard_VoiceHealth`
- `Difficulty_*`
- `Main_Calendar`
- `Main_Statistics`
- `Main_ExerciseGuide`
- `Main_Analyzer`
- `Main_SmartCoach`
- `Main_Resonance`
- `Main_Progression`
- `Main_Analysis`
- `Main_Settings`
- `UI_StartSession`
- `UI_StopSession`
- `Main_YourProgress`
- `Main_CurrentLevel`
- `Main_SessionsToNextLevel`
- `Main_Streak`
- `Main_TotalSessions`
- `Main_RealTimeFeedback`
- `Main_Feedback`
- `Main_ExerciseText`
- `Realtime_PitchBelowZoneFormat`
- `Realtime_PitchInZoneFormat`
- `Realtime_PitchAboveZoneFormat`
- `Feedback_Realtime_NoVoice`
- `LiveFeedback_SpeakLouder`
- error/status strings used by `MainViewModel`

Hardcoded or semi-hardcoded main-page text:

- Window title: `FemVoice Studio`.
- Header: `FemVoice Studio`.
- Score header: `FemVoiceScore`.
- Target separators and unit: `:`, `-`, `Hz`.
- Mic emoji and icon glyphs.
- Debug info format includes `Pitch`, `Smoothed`, `RMS`, `Voiced`, `Stability`, `Hz`.

Clinical language review:

- Current realtime pitch messages are materially safer than old pitch-pressure copy.
- `Realtime_PitchBelowZoneFormat` says under the green zone, lower/darker, try slightly brighter placement only if comfortable.
- `Realtime_PitchInZoneFormat` explicitly includes comfort and resonance.
- `Realtime_PitchAboveZoneFormat` warns against pressure.
- The main dashboard still exposes exact Hz in multiple locations.
- Some older resources outside the main page still contain pitch-forward phrasing and should remain covered by policy/manual review.

## 7. Broken or Unclear Wiring

Potential issues:

1. Graph logic bypasses ViewModel.
   - File: `MainWindow.xaml.cs`
   - `RenderLatestPitchPoint()`, axis policy, line color, annotation update, and data history live in code-behind.
   - Some policy is testable via services, but graph orchestration is not MVVM.

2. Duplicate audio pipelines update the same properties.
   - File: `MainViewModel.cs`
   - `OnPitchUpdated()` and `OnPitchAnalyzed()` both set `CurrentPitch`, `SmoothedPitch`, stability/health, score, and feedback.
   - This can cause interleaving, hard-to-reproduce UI states, and mismatched target zones.

3. Realtime feedback target mismatch.
   - `OnPitchAnalyzed()` calls `GetRealtimeFeedback(result, ComfortZone.Min, ComfortZone.Max)`.
   - `OnPitchUpdated()` calls `GetRealtimeFeedback(..., ActivePitchTargetZone.Min, ActivePitchTargetZone.Max)`.
   - The visible target badge/graph uses `ActivePitchTargetZone`.

4. `PitchHistory` is maintained but main graph does not bind to it.
   - `OnSmoothedPitchUpdated()` calls `AddPitchDataPoint()`.
   - `MainWindow` uses its own `_pitchDataPoints` list.
   - This is duplicate chart state.

5. `_uiUpdateTimer` is currently mostly unused.
   - It starts/stops with recording.
   - `OnUiTimerTick()` has no active behavior.
   - This is not a functional bug, but it is unclear wiring.

6. Main score values use simplified proxy inputs.
   - File: `MainViewModel.CalculateLiveScore()`.
   - `AverageF1`, `AverageF3`, `SpectralCentroid`, `AverageF2`, resonance, intonation, and health are rough estimates.
   - Display labels do not tell the user they are estimates.

7. Health/stability badges update in code-behind named controls.
   - This works, but bypasses binding and makes localization/theme testing harder.

8. Error visibility binding may be suspect.
   - `ErrorMessage` is a string bound to `BoolToVisibilityConverter`.
   - If the converter only handles bool, error messages may not show. This needs converter verification.

9. Main page raw Hz display conflicts with broader "no number chasing" direction.
   - Target badge and axis are clear and technically useful.
   - For production, this needs either clinical justification or gentler visual framing.

## 8. Recommended Fixes

### Fix 1 - Align realtime feedback target source

- File: `FemVoiceStudio/ViewModels/MainViewModel.cs`
- Method/property: `OnPitchAnalyzed()`
- Problem: Uses `ComfortZone.Min/Max` while graph and target badge use `ActivePitchTargetZone`.
- Minimal safe fix: Use `ActivePitchTargetZone.Min/Max` for main-page realtime feedback, or clearly separate comfort zone vs active target zone in UI.
- Why it matters: Prevents the user from seeing a green zone but receiving text based on a different range.

### Fix 2 - Reduce duplicate pitch property writers

- File: `FemVoiceStudio/ViewModels/MainViewModel.cs`
- Method/property: `OnPitchUpdated()`, `OnPitchAnalyzed()`
- Problem: Two live pipelines write the same UI-bound pitch/feedback/score values.
- Minimal safe fix: Designate one pipeline as dashboard-live source and the other as session-summary source, or add arbitration so only one updates display feedback.
- Why it matters: Avoids inconsistent graph/text and improves release reliability.

### Fix 3 - Move graph state toward ViewModel or dedicated chart adapter

- File: `FemVoiceStudio/Views/MainWindow.xaml.cs`
- Method/property: `RenderLatestPitchPoint()`, `_pitchDataPoints`, `_chartUpdateTimer`
- Problem: Code-behind owns chart history and clinical color state.
- Minimal safe fix: Keep OxyPlot rendering in code-behind if needed, but move graph point model creation and status-color decision into a small testable service or ViewModel adapter.
- Why it matters: Makes production behavior testable without WPF.

### Fix 4 - Reframe raw Hz display

- File: `FemVoiceStudio/Views/MainWindow.xaml`
- Method/property: target badge and axis labels
- Problem: Exact `Target: 175 - 240 Hz` and `Frequency (Hz)` can encourage number chasing.
- Minimal safe fix: Keep raw Hz available only where needed, but label it as "comfort zone" and visually reduce precision/importance. Consider moving exact Hz to tooltip/settings/debug if clinical review requests it.
- Why it matters: Voice feminization training should prioritize comfort, resonance, and stability over chasing a number.

### Fix 5 - Mark main-page resonance as proxy or use real resonance engine

- File: `FemVoiceStudio/ViewModels/MainViewModel.cs`
- Method/property: `CalculateLiveScore()`, `CurrentResonance`
- Problem: Main dashboard resonance is estimated from intensity/spectral proxy and fixed formant placeholders.
- Minimal safe fix: Rename display copy to indicate "resonance proxy" or wire main dashboard to `ResonanceProxyEngine` if production wants a true resonance score.
- Why it matters: Avoids overstating clinical meaning of a derived estimate.

### Fix 6 - Clarify voice health score meaning

- File: `FemVoiceStudio/ViewModels/MainViewModel.cs`
- Method/property: `HealthIndicator`, `VoiceHealthScore`
- Problem: Main page uses simplified pitch/intensity thresholds, not full `VocalHealthSupervisor`.
- Minimal safe fix: Label it as "signal/strain monitor" or route main dashboard through the same health intelligence used by exercise flow.
- Why it matters: Prevents medical overclaiming and aligns with safety-first architecture.

### Fix 7 - Remove or document unused dashboard timer

- File: `FemVoiceStudio/ViewModels/MainViewModel.cs`
- Method/property: `_uiUpdateTimer`, `OnUiTimerTick()`
- Problem: Timer starts/stops but currently does no work.
- Minimal safe fix: Remove it if unused, or document the intended future periodic task.
- Why it matters: Reduces release confusion and timer-related debugging.

### Fix 8 - Verify `ErrorMessage` visibility converter

- File: `FemVoiceStudio/Views/MainWindow.xaml`
- Method/property: `Visibility="{Binding ErrorMessage, Converter={StaticResource BoolToVisibility}}"`
- Problem: Binding string into bool converter may fail depending on converter implementation.
- Minimal safe fix: Use `StringToVisibilityConverter` for `ErrorMessage`.
- Why it matters: Release blockers should be visible to the user.

### Fix 9 - Localize remaining hardcoded dashboard labels where appropriate

- File: `FemVoiceStudio/Views/MainWindow.xaml`
- Method/property: header, score label, units
- Problem: `FemVoiceScore` and `Hz` are hardcoded. Brand name can stay hardcoded, but score/unit labels should be deliberate.
- Minimal safe fix: Add RESX keys for score label and target unit template, or document why technical units remain invariant.
- Why it matters: Avoids language inconsistency and supports clinical copy review.

## Final Audit Conclusion

What the main page currently does:

- It is a dashboard-style training screen with a central pitch graph, difficulty selection, session start/stop, live feedback, score bars, progress summary, and exercise text.
- It starts two audio paths: event-driven `AudioAnalysisEngine` for live pitch and `AudioAnalyzerService` for session analysis/history.
- It saves a `TrainingSession` and evaluates progression on stop.

What the pitch graph represents:

- A stabilized visual trace of detected fundamental frequency in Hz over time.
- It is not normalized.
- It is not a full clinical resonance or health display.
- The green zone is the current active pitch target zone from `PitchTargetZonePolicy`, not necessarily a hard success requirement.

Whether shown values are clinically safe:

- Realtime feedback text is mostly safe and comfort-oriented.
- Target zones are safer than earlier ranges.
- The graph still has number-chasing risk because raw Hz is central and exact.
- Resonance/health/intonation scores on the main dashboard are rough live proxies and should not be presented as definitive clinical measurements.

Whether start/stop/session flow is correct:

- The basic start/stop flow is coherent and likely functional.
- The biggest architectural concern is duplicate audio pipelines updating the same UI values.
- There is no pause; stop completes and saves the session.

Must fix or decide before production:

- Align realtime feedback with the visible target zone.
- Decide whether main dashboard should remain raw-Hz-forward or be reframed as comfort-zone feedback.
- Clarify or improve main-page resonance/health proxy display.
- Reduce duplicate live pitch writers or document arbitration.
- Verify `ErrorMessage` visibility.
- Move more graph decision logic out of code-behind or into a testable adapter if this screen is release-critical.
