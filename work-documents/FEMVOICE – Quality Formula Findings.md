# FEMVOICE - Quality Formula Findings

Status: 2026-06-06

Scope: audit of whether FemVoice over-rewards pitch compared with resonance, stability, comfort and safety.

Rule followed: audit only. No code changes.

Files inspected:

- `FemVoiceStudio/Services/FemVoiceScoreEngine.cs`
- `FemVoiceStudio/Audio/ResonanceProxyEngine.cs`
- `FemVoiceStudio/Services/ExerciseIntelligenceCoordinator.cs`
- `FemVoiceStudio/Models/ExerciseLiveState.cs`
- `FemVoiceStudio/ViewModels/ExerciseDetailViewModel.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Services/VocalHealthSupervisor.cs`
- `FemVoiceStudio/Services/ProgressionOrchestrator.cs`
- `FemVoiceStudio/Services/SessionAnalyticsStore.cs`

Files requested but not found as active source files:

- `DashboardViewModel.cs`
- `StrainDetectionPolicy.cs`
- `FatigueDetectionPolicy.cs`

The main dashboard uses `MainViewModel`, not `DashboardViewModel`. Strain and fatigue logic are implemented inside `VocalHealthSupervisor.cs`, not in separate policy files.

## Section 1 - Quality Formula Truth

### A. Main dashboard live score

Primary files:

- `FemVoiceStudio/Services/FemVoiceScore.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`

Formula:

```text
OverallScore =
  ResonanceScore * 0.45 +
  PitchScore * 0.30 +
  IntonationScore * 0.15 +
  VoiceHealthScore * 0.10
```

Inputs from `MainViewModel.CalculateLiveScore()`:

- `AveragePitch = SmoothedPitch`
- `MinPitch = SmoothedPitch`
- `MaxPitch = SmoothedPitch`
- `PitchVariation = _liveMetrics.GetPitchVariance()`
- `AverageF1 = 500`
- `AverageF2 = _liveMetrics.EstimateF2(result.RmsValue * 5000)`
- `AverageF3 = 2500`
- `SpectralCentroid = result.RmsValue * 5000`
- `IntonationRange = clamp(sqrt(pitchVariance) * 4, 0, 120)`
- `IntonationRiseScore = clamp(sqrt(pitchVariance), 0, 60)`
- `StrainLevel = 50` when `HealthIndicator == Warning`
- `StrainLevel = 80` when `HealthIndicator == Danger`
- `IntensityRms = result.RmsValue`
- `TargetMinPitch = ActivePitchTargetZone.Min`
- `TargetMaxPitch = ActivePitchTargetZone.Max`
- `DifficultyLevel = CurrentDifficulty`

Thresholds and penalties:

- `MaxSafePitch = 280`
- `StrainThreshold = 50`
- `CriticalStrainThreshold = 75`
- `IdealMinPitch = 165`
- `IdealMaxPitch = 255`
- `PitchVariationIdealMax = 25`
- `F1IdealMin = 400`
- `F1IdealMax = 700`
- `F2IdealMin = 1400`
- `F2IdealMax = 2200`
- `SpectralCentroidIdealMin = 2000`
- `IntonationRangeIdealMin = 30`
- `IntonationRangeIdealMax = 120`
- `StrainLevel >= 50` caps overall score at 60
- `StrainLevel >= 75` caps overall score at 40
- `AveragePitch > 280` creates high-pitch strain warning and health penalty
- high pitch above 200 with low resonance applies pitch penalty
- good pitch with poor resonance multiplies overall by 0.7
- high pitch with poor health multiplies overall by 0.8

### B. Adaptive score engine

File: `FemVoiceStudio/Services/FemVoiceScoreEngine.cs`

Formula:

```text
RawScore =
  ResonanceScore * 0.45 +
  PitchScore * 0.30 +
  StabilityScore * 0.15 +
  HealthModifier * 0.10
```

Additional logic:

- adaptive score normalizes raw score against user baseline
- exponential smoothing alpha: `0.3`
- baseline rolling window: `30` days
- plateau threshold: `14` days
- regression threshold: `10%`
- minimum data points for baseline: `3`
- unstable sessions can cap adaptive score

### C. Exercise coordinator `Quality`

File: `FemVoiceStudio/Services/ExerciseIntelligenceCoordinator.cs`

Formula:

```text
score = 0

if UsesResonance:
  +0.5 if PrimaryMetricScore is within TargetResonanceMin/Max
  +0.3 if PrimaryMetricScore is near TargetResonanceMin*0.8 to TargetResonanceMax*1.2

if UsesStability:
  +0.3 if StabilityScore >= StabilityThreshold
  +0.2 if StabilityScore >= StabilityThreshold * 0.8

if UsesPitch && IsInComfortZone:
  +0.2

if IsSafetyLocked:
  Quality = Poor
else:
  score >= 0.8 => Excellent
  score >= 0.6 => Good
  score >= 0.4 => Improving
  else         => Poor
```

Important source detail:

- for resonance exercises, `PrimaryMetricScore = resonanceScore`
- for pitch exercises without resonance, `PrimaryMetricScore = NormalizePitch(pitch, pitchMin, pitchMax)`
- `NormalizePitch = clamp((pitch - min) / (max - min), 0, 1)`

### D. Exercise UI `DisplayQuality`

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

### Quality Score Breakdown Table

| Component | Weight | Source |
| --------- | ------ | ------ |
| Pitch | Dashboard: 30%. Coordinator quality: 20% comfort-zone bonus. DisplayQuality: can become 65% when pitch is primary metric. | `FemVoiceScore.cs`, `FemVoiceScoreEngine.cs`, `ExerciseIntelligenceCoordinator.cs`, `ExerciseDetailViewModel.cs` |
| Resonance | Dashboard score: 45%. Coordinator quality: up to 50%. Progression composite: 45%. | `FemVoiceScore.cs`, `FemVoiceScoreEngine.cs`, `ExerciseIntelligenceCoordinator.cs`, `ProgressionOrchestrator.cs` |
| Stability | Adaptive score: 15%. Coordinator quality: up to 30%. DisplayQuality: 35%. Progression composite: 35%. | `FemVoiceScoreEngine.cs`, `ExerciseIntelligenceCoordinator.cs`, `ExerciseDetailViewModel.cs`, `ProgressionOrchestrator.cs` |
| Comfort Zone | Coordinator quality: required for pitch contribution. Health strain: comfort breaches after 3 contribute +0.30 strain score. Dashboard: visible graph zone and target feedback, but not a hard global score gate. | `ExerciseIntelligenceCoordinator.cs`, `VocalHealthSupervisor.cs`, `MainViewModel.cs`, `MainWindow.xaml.cs` |
| Safety | Coordinator: safety lock forces `Quality = Poor`; DisplayQuality safety lock forces `Poor`; Health supervisor can restrict/lock; dashboard score only caps when warning/danger strain is present. | `ExerciseIntelligenceCoordinator.cs`, `ExerciseDetailViewModel.cs`, `VocalHealthSupervisor.cs`, `FemVoiceScore.cs`, `MainViewModel.cs` |

## Section 2 - Pitch Dependency Analysis

Answer: YES.

Severity: Critical.

A user can improve quality primarily by increasing pitch in these paths:

1. Pitch-primary exercise display quality

Affected files:

- `FemVoiceStudio/Services/ExerciseIntelligenceCoordinator.cs`
- `FemVoiceStudio/ViewModels/ExerciseDetailViewModel.cs`

Path:

```text
pitch -> NormalizePitch(pitch, min, max)
      -> PrimaryMetricScore
      -> DisplayQuality composite = 0.65 * PrimaryMetricScore + 0.35 * StabilityScore
```

Problem:

Inside the target pitch zone, a higher pitch maps to a higher normalized value. This means display quality can improve by moving upward inside the pitch zone, even if the clinically safer target is comfortable and stable production rather than maximum pitch.

2. Main dashboard pitch score

Affected files:

- `FemVoiceStudio/Services/FemVoiceScore.cs`
- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Views/MainWindow.xaml`

Path:

```text
SmoothedPitch -> FemVoiceScoreInput.AveragePitch
              -> PitchScore
              -> OverallScore 30% contribution
              -> visible dashboard score
```

Problem:

The score formula penalizes pitch above the target and poor resonance, so it is not pure "higher is better." However, a user below the pitch zone can improve score by increasing pitch, and the UI makes that movement visually prominent.

Conclusion:

Pitch does not mathematically dominate all scoring, but pitch can dominate the user's strategy because the most visible live feedback is pitch movement.

## Section 3 - Resonance Importance Analysis

Quality: High.

Evidence:

- `FemVoiceScore` gives resonance 45%, the largest dashboard component.
- `ExerciseIntelligenceCoordinator` gives resonance up to 0.5 of quality score.
- `DisplayQuality` uses primary metric at 65%; for resonance exercises that makes resonance dominant in displayed quality.

Progression: Dominant.

Evidence:

- `ProgressionOrchestrator.CompositeScore()` uses:

```text
ResonanceQualityIndex * 0.45 +
StabilityConsistency * 0.35 +
HoldCompletionRate * 0.20
```

- `BuildProgressionDecision()` checks resonance before stability, hold length and pitch comfort.
- Pitch comfort is scaled only after resonance/stability/hold checks pass.

Mastery: Moderate.

Evidence:

- `ExerciseDetailViewModel.ComputeMasteryFromProgress()` uses completed session count and `_averageSessionScore`.
- It does not directly inspect resonance.
- If average score came from resonance-heavy systems, resonance influences mastery indirectly.
- If average score came from pitch/proxy-heavy systems, resonance can be diluted.

Dashboard feedback: Moderate.

Evidence:

- Dashboard displays `ResonanceScore` in the score panel.
- But dashboard resonance is not sourced from `ResonanceProxyEngine`; it uses fixed F1/F3 and RMS-derived F2/spectral proxy in `MainViewModel`.

Overall resonance verdict:

Resonance is high/dominant in the architecture, but only moderate on the main dashboard because the visible resonance value is proxy-based and less prominent than the pitch graph.

## Section 4 - Stability Influence

Score:

- `FemVoiceScoreEngine`: stability is 15% in adaptive raw score.
- `FemVoiceScore`: pitch variation affects `PitchScore`; intonation is also variance-derived in dashboard live input.
- `ExerciseIntelligenceCoordinator`: stability contributes up to 0.3 to `Quality`.
- `ExerciseDetailViewModel`: stability is 35% of `DisplayQuality`.

Mastery:

- Mastery does not directly require stability.
- `ComputeMasteryFromProgress()` uses completed sessions and average score only.
- Therefore stability influences mastery only through upstream average score.

Progression:

- `ProgressionOrchestrator`: stability is 35% of composite.
- `MinimumStableStability = 0.60`.
- If average stability is below threshold, progression suggests `ScaleStability()` before pitch comfort.

Coach feedback:

- `ExerciseIntelligenceCoordinator` publishes `STABILITY_LOW` when `UsesStability` and secondary metric is below `StabilityThreshold`.
- `VocalHealthSupervisor` uses stability drop/drift as strain and fatigue evidence:
  - `StabilityDropForStrain = 0.18`
  - `StabilityDriftForFatigue = 0.09`
  - micro alpha `0.35`
  - meso alpha `0.08`

Verdict:

Stability influence is clinically strong in exercise/progression/health. It is weaker in mastery because mastery lacks explicit stability gating.

## Section 5 - Comfort Zone Protection

Can users receive Excellent Quality while outside comfort zone?

Answer: NO for coordinator `Quality` in pitch-using profiles, but YES risk for displayed/indirect states depending on profile type and dashboard.

Details:

- In `ExerciseIntelligenceCoordinator`, if `UsesPitch` is true, `EvaluateHoldCondition()` requires `isInComfortZone`.
- Pitch contribution to coordinator quality is added only when `IsInComfortZone`.
- Safety lock forces `Quality = Poor`.
- For non-pitch profiles, `IsInComfortZone` is effectively not the governing metric, so "outside pitch comfort zone" is not applicable.
- Dashboard `OverallScore` is not hard-gated by comfort zone. It can remain high outside visible pitch zone if resonance proxy, intonation and health scores remain high.

Can users receive High Mastery while outside comfort zone?

Answer: YES.

Severity: Critical.

Reason:

`ExerciseDetailViewModel.ComputeMasteryFromProgress()` uses:

```text
completedSessions < 3 => Beginner
averageSessionScore < 60 => Developing
averageSessionScore < 80 => Stable
else => Mastered
```

There is no direct comfort-zone, safety-event or stability gate in the mastery formula. If average session score is high due to upstream scoring, mastery can become high without directly checking comfort-zone history.

Can users receive Progression Rewards while outside comfort zone?

Answer: Mostly NO in the current orchestrator, but not fully impossible.

Evidence:

- `ProgressionOrchestrator` uses resonance/stability/hold composite, not pitch comfort, for progression readiness.
- Safety events and fatigue can trigger regression/pause.
- Pitch comfort is scaled last.

Risk:

The composite does not directly include `AveragePitchComfort`. If a session summary has good resonance/stability/hold but poor pitch comfort without safety/fatigue events, progression may still proceed in non-pitch dimensions.

Severity: High.

Recommended architectural fix:

Add explicit comfort/safety gates to mastery and progression decisions, not just score averages.

## Section 6 - Strain Reward Analysis

Can users improve score while increasing strain risk?

Answer: YES.

Severity: Critical.

Evidence:

1. Dashboard resonance proxy can improve with RMS-derived inputs.

Affected files:

- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Services/FemVoiceScore.cs`

Issue:

`AverageF2` and `SpectralCentroid` in dashboard live scoring are derived from `result.RmsValue * 5000`. Increased loudness/effort can therefore improve the apparent resonance/brightness input before `HealthIndicator` crosses warning/danger thresholds.

2. Dashboard health penalties trigger late.

Affected files:

- `FemVoiceStudio/ViewModels/MainViewModel.cs`
- `FemVoiceStudio/Services/FemVoiceScore.cs`

Issue:

`StrainLevel` passed into `FemVoiceScoreInput` is zero unless health state is `Warning` or `Danger`. Mild effort increase can improve pitch/resonance proxy without immediate score cap.

Can users improve quality while increasing strain risk?

Answer: YES in dashboard/proxy score paths; mostly NO in exercise safety-lock paths.

Can users improve mastery while increasing strain risk?

Answer: YES indirectly.

Reason:

Mastery reads completed sessions and average score, not direct strain/safety history.

Recommended architectural fix:

Any score or mastery path should use health/safety as a hard gate, not only a small weighted component or late penalty. Proxy resonance should not be allowed to rise solely from louder signal energy.

## Section 7 - Missing Vocal Weight Review

Current status:

FemVoice does not explicitly model:

- vocal weight
- vocal thickness
- vocal mass perception

Related but not equivalent signals exist:

- RMS/intensity
- spectral centroid/brightness
- jitter/shimmer/HNR
- strain level
- resonance/formant estimates
- airflow/intensity exercise profiles

Impact:

Without a vocal weight dimension, the system may overinterpret pitch and resonance as the main acoustic path to feminization. Modern transfeminine voice work commonly treats vocal weight/lightness as a separate dimension from pitch and resonance. Missing this dimension may cause the app to miss cases where pitch and resonance look acceptable but the voice remains heavy/pressed, or where a user becomes breathy/strained while trying to sound lighter.

Severity: Medium-High.

Do not implement yet:

Future support should be designed carefully as a confidence-gated, baseline-relative voice-quality dimension. It should not be a simplistic "lighter is always better" score.

## Section 8 - Clinical Alignment Rating

| Area | Score / 10 |
| ---- | ---------- |
| Pitch Usage | 6 |
| Resonance Usage | 7 |
| Stability Usage | 8 |
| Comfort Protection | 7 |
| Health Protection | 8 |
| Feminization Alignment | 7 |

## Final Findings

FemVoice does not over-reward pitch in every formula. The core architecture often weights resonance and stability correctly. The problem is that several visible or downstream paths can still reward pitch or effort in ways that conflict with modern transfeminine voice training principles.

Critical conflicts:

1. Pitch-primary `DisplayQuality` can reward higher pitch inside the pitch zone because normalized pitch is used as the primary metric.
2. Main dashboard can reward increased vocal effort through RMS-derived resonance proxy values.
3. Mastery can become high from average score and session count without direct comfort-zone or strain-history gates.
4. Dashboard score is not hard-gated by comfort-zone status.

Recommended next step:

Before code changes, define the intended clinical scoring contract:

- pitch should mean comfortable zone match, not upward movement
- resonance should come from resonance/formant engine, not RMS proxy
- stability and comfort should gate quality/mastery
- safety should be a hard gate for quality, score and progression
- vocal weight should be planned as a separate future dimension

