# FEMVOICE - Clinical Alignment Findings

Status: 2026-06-06

Scope: Priority 1 audit of scoring, dashboard, pitch graph, resonance visibility, vocal weight coverage, comfort-zone validation and score-chasing risk.

Rule followed: no code changes. This document reports findings only.

Clinical basis:

- Modern transfeminine voice training should not treat pitch alone as feminization.
- Resonance, vocal weight, stability, comfort and sustainability must be considered alongside pitch.
- Feedback must not reward strain, excessive effort or unsafe target chasing.
- ASHA describes gender-affirming voice work as a combination of pitch, resonance, voice quality, intonation, vocal health and client-specific goals, and warns against overemphasis on speaking fundamental frequency.
- UCSF describes voice feminization as involving pitch, resonance, intonation and intensity, with safe and efficient production.

Primary files inspected:

- `FemVoiceStudio/Services/FemVoiceScore.cs`
- `FemVoiceStudio/Services/FemVoiceScoreEngine.cs`
- `FemVoiceStudio/Services/ExerciseIntelligenceCoordinator.cs`
- `FemVoiceStudio/ViewModels/ExerciseDetailViewModel.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Views/MainWindow.xaml`
- `FemVoiceStudio/Views/MainWindow.xaml.cs`
- `FemVoiceStudio/Services/FeedbackService.cs`
- `FemVoiceStudio/Services/ExerciseFeedbackEngine.cs`
- `FemVoiceStudio/Audio/ResonanceProxyEngine.cs`
- `FemVoiceStudio/Services/ComfortZoneController.cs`
- `FemVoiceStudio/Models/ExerciseLiveState.cs`

## Section 1 - Quality Formula Audit

### Main dashboard score formula

File: `FemVoiceStudio/Services/FemVoiceScore.cs`

Formula:

```text
OverallScore =
  ResonanceScore * 0.45 +
  PitchScore * 0.30 +
  IntonationScore * 0.15 +
  VoiceHealthScore * 0.10
```

Inputs:

- `AveragePitch`
- `PitchVariation`
- `AverageF1`
- `AverageF2`
- `AverageF3`
- `SpectralCentroid`
- `IntonationRange`
- `IntonationRiseScore`
- `StrainLevel`
- `IntensityRms`
- `TargetMinPitch`
- `TargetMaxPitch`
- `DifficultyLevel`
- optional `ResonanceScore`
- optional `VoiceHealthScore`

Safety modifiers:

- `StrainLevel >= 50` caps overall at 60.
- `StrainLevel >= 75` caps overall at 40.
- `AveragePitch > 280` creates high-pitch strain warning and health penalty.
- `PitchScore > 60 && ResonanceScore < 40` multiplies overall by 0.7.
- `VoiceHealthScore < 60 && AveragePitch > 220` multiplies overall by 0.8.

Dashboard live input source:

File: `FemVoiceStudio/ViewModels/MainViewModel.cs`

The live dashboard builds `FemVoiceScoreInput` with:

- fixed `AverageF1 = 500`
- fixed `AverageF3 = 2500`
- `AverageF2 = EstimateF2(result.RmsValue * 5000)`
- `SpectralCentroid = result.RmsValue * 5000`
- `StrainLevel = 50` only for `HealthState.Warning`, `80` only for `HealthState.Danger`
- target zone from `ActivePitchTargetZone`

Finding: the dashboard score formula is clinically reasonable on paper because resonance has the largest weight. The live dashboard implementation weakens that safety because resonance is estimated from RMS-derived proxy values rather than the full resonance engine.

### Exercise live `Quality`

File: `FemVoiceStudio/Services/ExerciseIntelligenceCoordinator.cs`

Formula:

```text
score = 0

if UsesResonance:
  +0.5 when primaryMetric is inside TargetResonanceMin/Max
  +0.3 when near target window

if UsesStability:
  +0.3 when StabilityScore >= StabilityThreshold
  +0.2 when near stability threshold

if UsesPitch && IsInComfortZone:
  +0.2

Quality:
  score >= 0.8 => Excellent
  score >= 0.6 => Good
  score >= 0.4 => Improving
  otherwise   => Poor

if safety locked:
  Quality = Poor
```

Clinical validation:

- Good: pitch alone cannot produce `Good` or `Excellent` in the coordinator formula, because pitch contributes only 0.2.
- Good: safety lock forces `Poor`.
- Good: hold progress freezes during safety lock.
- Good: resonance and stability dominate exercise quality.

### Exercise displayed `DisplayQuality`

File: `FemVoiceStudio/ViewModels/ExerciseDetailViewModel.cs`

Formula:

```text
composite = clamp(0.65 * PrimaryMetricScore + 0.35 * StabilityScore, 0, 1)

if IsSafetyLocked:
  DisplayQuality = Poor
else if composite >= 0.90:
  Excellent
else if composite >= 0.75:
  VeryGood
else if composite >= 0.55:
  Good
else if composite >= 0.35:
  Fair
else:
  Poor
```

Critical issue:

When the active profile is pitch-primary and does not use resonance, `ExerciseIntelligenceCoordinator` sets:

```text
PrimaryMetricScore = NormalizePitch(pitch, pitchMin, pitchMax)
```

That means a pitch value closer to the top of the zone produces a higher `PrimaryMetricScore`. Since `DisplayQuality` weights `PrimaryMetricScore` at 65%, a user can improve displayed quality by moving higher within the pitch zone in pitch-primary exercises.

Answer to required questions:

| Question | Answer | Severity |
| -------- | ------ | -------- |
| Can a user increase Quality by increasing pitch alone? | Coordinator `Quality`: mostly no. Exercise `DisplayQuality`: yes in pitch-primary profiles. Dashboard `OverallScore`: yes if pitch moves into target zone. | Critical |
| Can a user increase Quality by increasing effort? | Dashboard live resonance can rise with RMS-derived `AverageF2`/`SpectralCentroid`, so effort/loudness may improve proxy resonance before health penalties trigger. | Critical |
| Can a user increase Quality while leaving comfort zone? | Exercise pitch profiles block comfort-zone quality, but dashboard `OverallScore` can remain high outside the visible target if resonance proxy and other scores are high. | High |
| Can a user increase Quality while straining? | Severe strain is capped, but mild/moderate effort may still be rewarded if it improves proxy resonance/pitch before warning thresholds. | High |

## Section 2 - Pitch Overweight Audit

### Main scoring

`FemVoiceScore` weights:

- resonance: 45%
- pitch: 30%
- intonation: 15%
- voice health: 10%

The weighting itself does not make pitch dominant. Resonance is weighted higher than pitch.

Issue: the dashboard implementation makes pitch more psychologically dominant than the formula implies because:

- the largest live visual is the pitch graph
- the graph uses raw Hz
- the target badge shows exact Hz
- realtime feedback includes current Hz and target Hz
- the score panel gives pitch equal visual space beside resonance

Severity: High.

Affected files:

- `FemVoiceStudio/Views/MainWindow.xaml`
- `FemVoiceStudio/Views/MainWindow.xaml.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Services/FeedbackService.cs`

Why this conflicts with best practice:

Transfeminine voice development should not train "higher pitch equals better." Even when score math gives resonance more weight, the UI can still teach pitch chasing if the most active visual element is raw Hz.

Recommended architectural fix:

Route the main dashboard through a clinical display model that presents pitch as "inside/near/outside comfortable zone" and makes resonance/stability/comfort equally or more prominent than raw Hz.

## Section 3 - Pitch Graph Audit

Current graph truth:

- `MainWindow.xaml.cs` owns the OxyPlot `PlotModel`.
- `RenderLatestPitchPoint()` plots pitch as raw Hz over session time.
- `PitchTraceStabilizer` filters spikes/harmonics before display.
- `PitchChartAxisRangeCalculator` adjusts y-axis to visible pitch and target zone.
- Green annotation represents the active pitch target zone.
- Line color becomes green inside zone, yellow outside zone, red when health warning/danger is present.

Clinical message taught by the graph:

The intended message is "stable comfortable target range is better." The practical visual message can still become "put the line in the green Hz band."

Critical finding:

The graph still encourages pitch chasing risk because it combines:

- exact Hz y-axis
- exact Hz target badge
- a green target band
- moving live line
- realtime text with numeric Hz

This is not automatically unsafe, but it is unsafe as a primary training surface for a transfeminine user who may be motivated to push pitch upward.

Severity: Critical.

Affected files:

- `FemVoiceStudio/Views/MainWindow.xaml`
- `FemVoiceStudio/Views/MainWindow.xaml.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Services/FeedbackService.cs`

Recommended architectural fix:

Keep raw Hz available for advanced/debug/analyzer contexts, but make the default main graph communicate zone status, comfort and stability first. The main dashboard should not make exact Hz the primary success object.

## Section 4 - Resonance Audit

Positive findings:

- `FemVoiceScore` weights resonance at 45%, higher than pitch.
- `ExerciseIntelligenceCoordinator` can make resonance the primary metric.
- `ResonanceProxyEngine` emits formant/resonance data with F1/F2/F3, spectral centroid and stability.
- `AnalyzerWindow` uses spectrogram resonance intelligence.
- `ProgressionOrchestrator` documentation says resonance is prioritized before pitch comfort.

Problem:

Main dashboard `ResonanceScore` does not use `ResonanceProxyEngine`. It is built from fixed formant placeholders and RMS-derived estimates:

- `AverageF1 = 500`
- `AverageF3 = 2500`
- `AverageF2 = EstimateF2(result.RmsValue * 5000)`
- `SpectralCentroid = result.RmsValue * 5000`

Severity: Critical.

Affected files:

- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Services/FemVoiceScore.cs`
- `FemVoiceStudio/Audio/ResonanceProxyEngine.cs`
- `FemVoiceStudio/Views/MainWindow.xaml`

Why this conflicts with best practice:

Resonance is one of the core dimensions of transfeminine voice training. Showing a proxy value as `ResonanceScore` can mislead the user into believing loudness/signal energy is forward resonance.

Recommended architectural fix:

Use the same `ResonanceProxyEngine`/formant path for dashboard resonance or rename the live dashboard value as signal/brightness guidance until true resonance is wired in.

## Section 5 - Missing Vocal Weight Audit

Current status:

FemVoice has several related signals:

- intensity/RMS
- spectral centroid/brightness
- HNR/jitter/shimmer in `VoiceMetricsCalculator`
- strain detectors
- airflow/intensity exercise profiles
- resonance/formant mapping

But there is no explicit model for:

- vocal weight
- vocal thickness
- perceived vocal fold mass
- light/heavy voice quality as a separate dimension from pitch and resonance

Severity: Medium-High.

Affected files/areas:

- `FemVoiceStudio/Services/FemVoiceScore.cs`
- `FemVoiceStudio/Services/FemVoiceScoreEngine.cs`
- `FemVoiceStudio/Audio/VoiceMetricsCalculator.cs`
- `FemVoiceStudio/Models/ExerciseTargetProfile.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`

Why this conflicts with best practice:

Modern transfeminine training often separates pitch, resonance/size, vocal weight and dynamic/prosodic control. Without a vocal weight dimension, the system may over-rely on pitch/resonance proxies and miss a major perceptual factor.

Recommended architectural fix:

Add a future `VocalWeightEstimate` or `VoiceQualityDimension` model as a separate normalized metric. It should be confidence-gated and not treated as a medical diagnosis. It should likely use combinations of spectral tilt, harmonic structure, intensity, HNR/jitter/shimmer and user baseline, then expose "lighter/heavier/pressed" guidance cautiously.

## Section 6 - Comfort Zone Validation

Positive findings:

- `ExerciseIntelligenceCoordinator` blocks pitch comfort when outside zone.
- `Quality` becomes `Poor` when safety locked.
- `HoldProgress` freezes during safety lock.
- `ComfortZoneController` supports safety locks after strain incidents.
- Main target policy caps advanced upper zone at 240 Hz.

Issues:

### Issue 1 - Dashboard has two pitch-zone sources

Files:

- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Views/MainWindow.xaml.cs`

`OnPitchUpdated()` uses `ActivePitchTargetZone` for realtime feedback. `OnPitchAnalyzed()` uses `ComfortZone`. The graph annotation uses `ActivePitchTargetZone`.

Severity: High.

Why this conflicts with best practice:

The user can receive feedback that does not match the visible green zone. In voice training this is more than a UX bug: inconsistent guidance can lead a user to push or compensate.

Recommended architectural fix:

Create one dashboard-facing `ActiveTrainingZoneState` and require graph, realtime feedback, score input and pitch history to read from it.

### Issue 2 - Dashboard scores can stay high outside comfort zone

Files:

- `FemVoiceStudio/Services/FemVoiceScore.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`

Because pitch is only 30% of the composite, a user outside the visible zone can still receive a respectable score if proxy resonance, intonation and health remain high.

Severity: High.

Recommended architectural fix:

When the visible training context is pitch-zone based, outside-zone state should cap dashboard score or convert score language to "not ready / outside comfort" rather than allowing high overall percentages.

## Section 7 - Score Chasing Risk Audit

### Quality

Risk:

- Exercise `DisplayQuality` can be increased by higher normalized pitch in pitch-primary profiles.
- Dashboard `OverallScore` is a visible percentage.

Severity: Critical for pitch-primary `DisplayQuality`; High for dashboard score.

Recommended architectural fix:

Make pitch-primary display quality use distance-to-comfort-center or inside-zone stability rather than `NormalizePitch` where higher pitch means higher metric.

### Mastery

File: `FemVoiceStudio/ViewModels/ExerciseDetailViewModel.cs`

Current rule:

```text
if completedSessions < 3 => Beginner
else if averageSessionScore < 60 => Developing
else if averageSessionScore < 80 => Stable
else => Mastered
```

Risk:

Mastery is based on average score and session count. If upstream scores contain pitch/proxy resonance bias, mastery can inherit that bias.

Severity: Medium-High.

Recommended architectural fix:

Mastery should require health-safe sessions, resonance/stability gates and no repeated comfort-zone warnings, not just average score.

### Session progress

Risk:

Main dashboard progression status and post-session score can reinforce "get the number high" behavior.

Severity: Medium.

Recommended architectural fix:

Phrase progress as consistency, comfort, recovery and stable resonance development. Avoid making percentage score the dominant reinforcement.

### Dashboard metrics

Risk:

Four equal-width bars visually imply equal interpretive certainty:

- Resonance
- Pitch
- Intonation
- Voice Health

But the dashboard resonance and health values are proxies/simplified indicators.

Severity: High.

Recommended architectural fix:

Dashboard should distinguish measured values from estimated guidance. Resonance and health should either use authoritative engines or be visually labeled as approximate.

## Priority Findings Table

| Severity | Issue | Affected files | Why it matters clinically | Recommended architectural fix |
| -------- | ----- | -------------- | ------------------------- | ----------------------------- |
| Critical | Pitch-primary `DisplayQuality` can reward higher pitch inside the zone because `PrimaryMetricScore = NormalizePitch(...)` and display quality weights primary at 65%. | `ExerciseIntelligenceCoordinator.cs`, `ExerciseDetailViewModel.cs` | Can teach "higher within the zone is better" instead of "comfortable stable target range is better." | Replace pitch-primary display metric with center-distance/zone-quality/stability composite. |
| Critical | Dashboard pitch graph remains raw-Hz dominant. | `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainViewModel.cs`, `FeedbackService.cs` | Most salient UI element can train pitch chasing. | Make dashboard graph zone/comfort/stability-first; keep raw Hz secondary or advanced. |
| Critical | Dashboard resonance score is proxy-based from RMS-derived F2/centroid and fixed formants. | `MainViewModel.cs`, `FemVoiceScore.cs` | Can mislead user into treating loudness/signal energy as resonance. | Wire dashboard to `ResonanceProxyEngine` or relabel as approximate brightness/signal guidance. |
| Critical | Effort/loudness may improve dashboard resonance proxy before health penalties trigger. | `MainViewModel.cs`, `FemVoiceScore.cs` | Can reward extra vocal effort, conflicting with safe sustainable production. | Decouple resonance from RMS-only proxy and gate with strain/comfort/confidence. |
| High | `ActivePitchTargetZone` and `ComfortZone` are separate dashboard sources. | `MainViewModel.cs`, `MainWindow.xaml.cs` | Text and graph can disagree, causing unsafe compensation. | Use one dashboard-facing training zone state. |
| High | Dashboard overall score can remain high outside pitch comfort zone. | `FemVoiceScore.cs`, `MainViewModel.cs` | User may learn that score matters more than comfort-zone status. | Cap or reclassify dashboard score when outside visible training zone. |
| High | Score bars visually overstate certainty of proxy metrics. | `MainWindow.xaml` | User may trust approximate values as clinical measurements. | Separate authoritative metrics from guidance estimates in UI/viewmodel. |
| Medium-High | Vocal weight is not modeled as a separate dimension. | scoring, metrics and exercise profile layers | Missing a major modern transfeminine voice dimension. | Add future confidence-gated vocal weight/voice quality dimension. |
| Medium-High | Mastery inherits upstream score bias. | `ExerciseDetailViewModel.cs`, progress services | Mastery could reward score optimization rather than safe habitual skill. | Gate mastery on health-safe consistency, resonance/stability and low warning history. |
| Medium | Post-session `FeedbackService` remains pitch/prosody-heavy. | `FeedbackService.cs` | Session summary can reinforce pitch and numeric goals. | Route post-session summary through resonance/health/progression-aware feedback pipeline. |

## Final Priority 1 Verdict

FemVoice is directionally aligned with transfeminine voice training principles, especially in the Exercise Guide architecture and health/progression layers. However, Priority 1 clinical alignment is not complete.

Blocking issues before production release:

1. Pitch-primary display quality must stop rewarding higher normalized pitch.
2. Main dashboard resonance must stop appearing as authoritative while it is RMS/proxy-based.
3. Main pitch graph must stop acting as the dominant training surface for raw Hz.
4. Dashboard target-zone state must be unified.
5. Dashboard score must be capped or reframed when comfort/safety conditions are not met.

Recommended next phase:

Fix the Critical items first, then rerun this audit before moving to non-critical clinical enhancements such as vocal weight modeling.

