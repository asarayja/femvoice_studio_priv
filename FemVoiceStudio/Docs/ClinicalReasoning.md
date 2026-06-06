# Clinical Reasoning — ExerciseIntelligenceCoordinator

## Adaptive Threshold Evaluation

The coordinator evaluates exercise correctness against user-adapted thresholds rather than fixed clinical targets. Resonance score ranges are derived from the individual's formant baseline (F1, F2, F3 positions and spectral centroid) as tracked by `FemVoiceScoreEngine` over a 30-day rolling window. Stability thresholds similarly adapt to the user's demonstrated control level: a beginner may pass with a 0.35 stability score while an advanced user requires 0.65. This prevents the system from either discouraging novices with unattainable targets or under-challenging experienced practitioners.

## Exercise-Specific Metric Priority

Different vocal tasks recruit different physiological mechanisms. Resonance exercises train forward oral resonance through tongue position and vocal tract shaping — formant position is the clinically meaningful signal here, so it becomes the primary metric. Pitch exercises address laryngeal control and must be evaluated relative to the user's safety zone to avoid strain. Intonation exercises require dynamic pitch modulation, so slope tracking replaces static scoring. Straw phonation reduces supraglottic tension through flow resistance; intensity and consistency are the relevant signals, with resonance playing a supporting role only.

## Safety Integration

When `HealthScore < 70` or `ComfortZoneController` signals a lock, the coordinator freezes hold-progress accumulation and suppresses score rewards. This prevents reinforcement of strain-inducing behaviour while keeping real-time feedback active so the user can observe their voice recovering. Coaching messages at `Warning` severity are rate-limited to avoid alarm fatigue.

## Individual Adaptation

All thresholds evolve as `FemVoiceScoreEngine` updates the user's baseline. Plateau detection after 14 stable days triggers coaching strategy changes via `SmartCoachEngine`. Regression detection protects against overtraining by tightening safety boundaries automatically.
