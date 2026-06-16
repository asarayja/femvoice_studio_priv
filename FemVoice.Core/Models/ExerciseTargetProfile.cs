using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Types of indicators exposed to the UI.
    /// Kept minimal and aligned with ExerciseDetailViewModel indicators.
    /// </summary>
    public enum IndicatorType
    {
        Resonance,
        Stability,
        Pitch,
        Hold,
        Shield,
        Airflow
    }

    /// <summary>
    /// Describes which metrics an exercise uses and what the adaptive target thresholds are.
    /// One profile is created per exercise type and passed to
    /// <see cref="FemVoiceStudio.Services.ExerciseIntelligenceCoordinator.SetExerciseContext"/>.
    ///
    /// All thresholds are user-adapted at runtime â€” the factory methods below supply
    /// safe defaults that the clinical engine should override with personalised values.
    /// </summary>
    public class ExerciseTargetProfile
    {
        // â”€â”€ Metric flags â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Whether this exercise evaluates resonance (formant position / spectral brightness).</summary>
        public bool UsesResonance { get; init; }

        /// <summary>Whether this exercise evaluates pitch relative to the user's comfort zone.</summary>
        public bool UsesPitch { get; init; }

        /// <summary>Whether this exercise evaluates vocal stability / consistency.</summary>
        public bool UsesStability { get; init; }

        /// <summary>Whether this exercise evaluates vocal intensity (used in e.g. straw phonation).</summary>
        public bool UsesIntensity { get; init; }

        // â”€â”€ Guidance / UI localisation keys (optional) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // These keys allow ExerciseDetailViewModel to display guidance text sourced
        // from the profile or copied from Exercise/ExerciseDefinition.
        public string? ClinicalPurposeKey         { get; init; }
        public string? PhysicalFocusKey           { get; init; }
        public string? CommonMistakesKey          { get; init; }
        public string? SafetyInfoKey              { get; init; }

        // Display metadata keys used by the UI
        public string? FeedbackModeKey            { get; init; }
        public string? ThresholdStrategyKey       { get; init; }
        public string? IndicatorPackageSummaryKey { get; init; }

        // â”€â”€ Pitch boundaries (Hz) â€” nullable; only set for pitch-focused exercises â”€â”€

        /// <summary>
        /// Lower bound of the acceptable pitch range in Hz.
        /// <c>null</c> when pitch is not a primary metric.
        /// Populated from <see cref="FemVoiceStudio.Services.ComfortZoneController"/> at runtime.
        /// </summary>
        public double? MinPitch { get; init; }

        /// <summary>
        /// Upper bound of the acceptable pitch range in Hz.
        /// <c>null</c> when pitch is not a primary metric.
        /// </summary>
        public double? MaxPitch { get; init; }

        // â”€â”€ Resonance target range (0â€“1 normalised score) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Minimum acceptable resonance score (0â€“1) for a successful hold.
        /// Adapted to the user's individual formant baseline.
        /// </summary>
        public double TargetResonanceMin { get; init; }

        /// <summary>
        /// Maximum resonance score (0â€“1) considered optimal.
        /// Values above this indicate over-exertion and trigger a coaching hint.
        /// </summary>
        public double TargetResonanceMax { get; init; }

        // â”€â”€ Hold parameters â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Number of consecutive seconds all conditions must be met to complete a hold.
        /// 0 means the exercise has no hold requirement.
        /// </summary>
        public double RequiredHoldSeconds { get; init; }

        /// <summary>
        /// Minimum stability score (0â€“1) required for the hold condition to be satisfied.
        /// Adapted per user based on demonstrated control level; never a fixed clinical constant.
        /// </summary>
        public double StabilityThreshold { get; init; }

        // â”€â”€ Validation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Validates that the profile is internally consistent.
        /// Throws <see cref="InvalidOperationException"/> if configuration is contradictory.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when thresholds are invalid.</exception>
        public void Validate()
        {
            if (TargetResonanceMax < TargetResonanceMin)
                throw new InvalidOperationException(
                    $"TargetResonanceMax ({TargetResonanceMax}) must be >= TargetResonanceMin ({TargetResonanceMin}).");

            if (MinPitch.HasValue && MaxPitch.HasValue && MaxPitch.Value <= MinPitch.Value)
                throw new InvalidOperationException(
                    $"MaxPitch ({MaxPitch}) must be > MinPitch ({MinPitch}) when both are specified.");

            if (RequiredHoldSeconds < 0)
                throw new InvalidOperationException("RequiredHoldSeconds must be >= 0.");

            if (StabilityThreshold is < 0 or > 1)
                throw new InvalidOperationException("StabilityThreshold must be in [0, 1].");

            if (TargetResonanceMin is < 0 or > 1 || TargetResonanceMax is < 0 or > 1)
                throw new InvalidOperationException("Resonance targets must be in [0, 1].");
        }

        // â”€â”€ Factory methods â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Returns a profile for a resonance-focused exercise.
        /// Primary metric: resonance score. Secondary: stability.
        /// Pitch is validated only as a safety boundary, not as a scoring metric.
        /// </summary>
        public static ExerciseTargetProfile ResonanceExercise(
            double targetResonanceMin  = 0.55,
            double targetResonanceMax  = 0.90,
            double stabilityThreshold  = 0.50,
            double requiredHoldSeconds = 3.0) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = true,
                UsesPitch           = false,
                UsesStability       = true,
                UsesIntensity       = false,
                TargetResonanceMin  = targetResonanceMin,
                TargetResonanceMax  = targetResonanceMax,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = requiredHoldSeconds
                ,
                ClinicalPurposeKey = "GuidancePurpose_ResonanceExercise",
                PhysicalFocusKey   = "GuidanceFocus_ResonanceExercise",
                CommonMistakesKey  = "GuidanceMistakes_ResonanceExercise",
                SafetyInfoKey      = "GuidanceSafety_ResonanceExercise",
                FeedbackModeKey    = "FeedbackMode_Resonance",
                ThresholdStrategyKey = "ThresholdStrategy_Adaptive",
                IndicatorPackageSummaryKey = "IndicatorPackage_Resonance"
            };

        /// <summary>
        /// Returns a profile for a pitch-focused exercise.
        /// Primary metric: normalised pitch (relative to comfort zone). Secondary: stability.
        /// Resonance is monitored but does not block progression.
        /// </summary>
        public static ExerciseTargetProfile PitchExercise(
            double? minPitch           = null,
            double? maxPitch           = null,
            double targetResonanceMin  = 0.30,
            double targetResonanceMax  = 1.00,
            double stabilityThreshold  = 0.45,
            double requiredHoldSeconds = 3.0) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = false,
                UsesPitch           = true,
                UsesStability       = true,
                UsesIntensity       = false,
                MinPitch            = minPitch,
                MaxPitch            = maxPitch,
                TargetResonanceMin  = targetResonanceMin,
                TargetResonanceMax  = targetResonanceMax,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = requiredHoldSeconds
                ,
                ClinicalPurposeKey = "GuidancePurpose_PitchExercise",
                PhysicalFocusKey   = "GuidanceFocus_PitchExercise",
                CommonMistakesKey  = "GuidanceMistakes_PitchExercise",
                SafetyInfoKey      = "GuidanceSafety_PitchExercise",
                FeedbackModeKey    = "FeedbackMode_Pitch",
                ThresholdStrategyKey = "ThresholdStrategy_ComfortZone",
                IndicatorPackageSummaryKey = "IndicatorPackage_Pitch"
            };

        /// <summary>
        /// Returns a profile for an intonation exercise.
        /// Primary metric: pitch-slope tracking. Secondary: range variability.
        /// No hard hold requirement â€” progression is continuous.
        /// </summary>
        public static ExerciseTargetProfile IntonationExercise(
            double? minPitch          = null,
            double? maxPitch          = null,
            double stabilityThreshold = 0.35) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = false,
                UsesPitch           = true,
                UsesStability       = true,
                UsesIntensity       = false,
                MinPitch            = minPitch,
                MaxPitch            = maxPitch,
                TargetResonanceMin  = 0,
                TargetResonanceMax  = 1,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = 0   // no hold â€” continuous tracking
                ,
                ClinicalPurposeKey = "GuidancePurpose_IntonationExercise",
                PhysicalFocusKey   = "GuidanceFocus_IntonationExercise",
                CommonMistakesKey  = "GuidanceMistakes_IntonationExercise",
                SafetyInfoKey      = "GuidanceSafety_IntonationExercise",
                FeedbackModeKey    = "FeedbackMode_Intonation",
                ThresholdStrategyKey = "ThresholdStrategy_Continuous",
                IndicatorPackageSummaryKey = "IndicatorPackage_Intonation"
            };

        /// <summary>
        /// Returns a profile for straw phonation.
        /// Primary metric: intensity + stability composite. Resonance is lightly monitored.
        /// </summary>
        public static ExerciseTargetProfile StrawPhonation(
            double targetResonanceMin  = 0.20,
            double targetResonanceMax  = 1.00,
            double stabilityThreshold  = 0.55,
            double requiredHoldSeconds = 5.0) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = true,
                UsesPitch           = false,
                UsesStability       = true,
                UsesIntensity       = true,
                TargetResonanceMin  = targetResonanceMin,
                TargetResonanceMax  = targetResonanceMax,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = requiredHoldSeconds
                ,
                ClinicalPurposeKey = "GuidancePurpose_StrawPhonation",
                PhysicalFocusKey   = "GuidanceFocus_StrawPhonation",
                CommonMistakesKey  = "GuidanceMistakes_StrawPhonation",
                SafetyInfoKey      = "GuidanceSafety_StrawPhonation",
                FeedbackModeKey    = "FeedbackMode_Straw",
                ThresholdStrategyKey = "ThresholdStrategy_Intensity",
                IndicatorPackageSummaryKey = "IndicatorPackage_StrawPhonation"
            };

        /// <summary>
        /// Returns a profile for the "Grunnleggende humming" exercise.
        /// Goal: establish forward resonance placement, reduce posterior resonance,
        /// and maintain stable airflow throughout the phonation.
        ///
        /// Clinical rationale: Humming with lip closure reduces the acoustic load on
        /// the larynx and channels proprioceptive feedback to the frontal resonators.
        /// Stability is co-monitored to discourage effortful compensatory strategies.
        /// Pitch is tracked only as a safety boundary via the comfort-zone controller â€”
        /// no Hz value is hardcoded.
        ///
        /// Hold hierarchy: humming (3 s) &lt; vowels (4 s) &lt; stability training (6 s).
        /// Glide-up has no hold requirement.
        /// </summary>
        /// <param name="targetResonanceMin">
        /// Minimum normalised resonance score (0â€“1) for a successful hold.
        /// Default 0.50 â€” moderate forward placement; adapted upward as the user progresses.
        /// </param>
        /// <param name="targetResonanceMax">
        /// Upper resonance bound (0â€“1) above which over-exertion coaching is triggered.
        /// Default 0.85 â€” leaves headroom without penalising good performance.
        /// </param>
        /// <param name="stabilityThreshold">
        /// Minimum stability score (0â€“1) required for the hold condition.
        /// Default 0.45 â€” lower than vowel exercises to accommodate the transition phase.
        /// </param>
        /// <param name="requiredHoldSeconds">
        /// Consecutive seconds all conditions must be met. Default 3.0 s.
        /// </param>
        public static ExerciseTargetProfile CreateResonanceHumming(
            double targetResonanceMin  = 0.50,
            double targetResonanceMax  = 0.85,
            double stabilityThreshold  = 0.45,
            double requiredHoldSeconds = 3.0) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = true,
                UsesPitch           = false,   // comfort-zone safety only â€” no Hz target
                UsesStability       = true,
                UsesIntensity       = false,
                TargetResonanceMin  = targetResonanceMin,
                TargetResonanceMax  = targetResonanceMax,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = requiredHoldSeconds
                ,
                ClinicalPurposeKey = "GuidancePurpose_ResonanceHumming",
                PhysicalFocusKey   = "GuidanceFocus_ResonanceHumming",
                CommonMistakesKey  = "GuidanceMistakes_ResonanceHumming",
                SafetyInfoKey      = "GuidanceSafety_ResonanceHumming",
                FeedbackModeKey    = "FeedbackMode_ResonanceHumming",
                ThresholdStrategyKey = "ThresholdStrategy_Adaptive",
                IndicatorPackageSummaryKey = "IndicatorPackage_ResonanceHumming"
            };

        /// <summary>
        /// Returns a profile for the "Vokallyder â€“ fremre resonans" exercise.
        /// Goal: transfer resonance control from humming to open vowels and connected speech.
        ///
        /// Clinical rationale: Opening the oral cavity reduces the back-pressure that
        /// assists forward placement during humming. The resonance target is therefore
        /// stricter than <see cref="CreateResonanceHumming"/> to ensure the user
        /// actively maintains frontal focus rather than relying on lip-closure
        /// proprioception. Stability threshold is raised to reflect the increased
        /// neuromuscular demand of consistent formant positioning during vowel
        /// articulation.
        /// </summary>
        /// <param name="targetResonanceMin">
        /// Default 0.58 â€” stricter than humming to drive active frontal focus.
        /// </param>
        /// <param name="targetResonanceMax">
        /// Default 0.92 â€” narrower ceiling; over-brightness indicates laryngeal tension.
        /// </param>
        /// <param name="stabilityThreshold">
        /// Default 0.55 â€” higher than humming; vowels demand more consistent control.
        /// </param>
        /// <param name="requiredHoldSeconds">
        /// Default 4.0 s â€” longer than humming to build sustained vowel production.
        /// </param>
        public static ExerciseTargetProfile CreateResonanceVowels(
            double targetResonanceMin  = 0.58,
            double targetResonanceMax  = 0.92,
            double stabilityThreshold  = 0.55,
            double requiredHoldSeconds = 4.0) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = true,
                UsesPitch           = false,   // comfort-zone safety only
                UsesStability       = true,
                UsesIntensity       = false,
                TargetResonanceMin  = targetResonanceMin,
                TargetResonanceMax  = targetResonanceMax,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = requiredHoldSeconds
                ,
                ClinicalPurposeKey = "GuidancePurpose_ResonanceVowels",
                PhysicalFocusKey   = "GuidanceFocus_ResonanceVowels",
                CommonMistakesKey  = "GuidanceMistakes_ResonanceVowels",
                SafetyInfoKey      = "GuidanceSafety_ResonanceVowels",
                FeedbackModeKey    = "FeedbackMode_ResonanceVowels",
                ThresholdStrategyKey = "ThresholdStrategy_Adaptive",
                IndicatorPackageSummaryKey = "IndicatorPackage_ResonanceVowels"
            };

        /// <summary>
        /// Returns a profile for the "Glide Up" exercise.
        /// Goal: develop flexible pitch increase without loss of resonance or increased
        /// laryngeal tension, coordinating pitch motor control with frontal resonance.
        ///
        /// Clinical rationale: Ascending pitch glides stress the cricothyroid muscle
        /// while the buccinator and soft-palate elevators must simultaneously maintain
        /// forward resonance placement. Monitoring pitch (primary, via adaptive
        /// comfort-zone) and resonance (secondary) ensures the user does not sacrifice
        /// voice quality for range extension.
        ///
        /// No hold requirement is set because the exercise is a continuous controlled
        /// movement â€” a static hold would penalise the glide itself.
        /// Pitch boundaries are left null and populated at runtime by
        /// <see cref="FemVoiceStudio.Services.ComfortZoneController"/>; no Hz value
        /// is hardcoded in the model.
        /// </summary>
        /// <param name="targetResonanceMin">
        /// Default 0.35 â€” permissive lower bound; resonance naturally dips mid-glide.
        /// </param>
        /// <param name="targetResonanceMax">
        /// Default 0.90 â€” detects over-brightening at high pitches.
        /// </param>
        /// <param name="stabilityThreshold">
        /// Default 0.40 â€” lower than static exercises; controlled movement introduces
        /// acceptable variability that should not block progression.
        /// </param>
        public static ExerciseTargetProfile CreateCoordinatedGlideUp(
            double targetResonanceMin = 0.35,
            double targetResonanceMax = 0.90,
            double stabilityThreshold = 0.40) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = true,    // secondary â€” resonance preservation during glide
                UsesPitch           = true,    // primary â€” adaptive comfort-zone boundaries
                UsesStability       = true,
                UsesIntensity       = false,
                MinPitch            = null,    // set by ComfortZoneController at runtime
                MaxPitch            = null,
                TargetResonanceMin  = targetResonanceMin,
                TargetResonanceMax  = targetResonanceMax,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = 0        // continuous movement â€” no static hold
                ,
                ClinicalPurposeKey = "GuidancePurpose_CoordinatedGlideUp",
                PhysicalFocusKey   = "GuidanceFocus_CoordinatedGlideUp",
                CommonMistakesKey  = "GuidanceMistakes_CoordinatedGlideUp",
                SafetyInfoKey      = "GuidanceSafety_CoordinatedGlideUp",
                FeedbackModeKey    = "FeedbackMode_Glide",
                ThresholdStrategyKey = "ThresholdStrategy_Continuous",
                IndicatorPackageSummaryKey = "IndicatorPackage_GlideUp"
            };

        /// <summary>
        /// Returns a profile for "Konsistens-trening" (stability training).
        /// Goal: build neuromuscular control and consistent voice production over an
        /// extended period, developing endurance without increased laryngeal load.
        ///
        /// Clinical rationale: Sustained stable phonation trains the intrinsic
        /// laryngeal muscles and respiratory support to maintain a target configuration
        /// with minimal conscious effort â€” the foundation of automatic voice use in
        /// daily speech. The stability threshold is the highest of all profiles to
        /// ensure genuine neuromuscular consolidation. Resonance is co-monitored as a
        /// secondary metric so the user does not achieve stability at the cost of
        /// regressing to posterior placement. Pitch is tracked only as a safety boundary.
        ///
        /// Hold hierarchy: humming (3 s) &lt; vowels (4 s) &lt; stability training (6 s).
        /// </summary>
        /// <param name="targetResonanceMin">
        /// Default 0.45 â€” secondary floor; lower than vowels because stability is primary.
        /// </param>
        /// <param name="targetResonanceMax">
        /// Default 0.88 â€” broad ceiling; resonance is monitored, not maximised here.
        /// </param>
        /// <param name="stabilityThreshold">
        /// Default 0.70 â€” highest of all profiles; reflects the endurance focus.
        /// </param>
        /// <param name="requiredHoldSeconds">
        /// Default 6.0 s â€” longest of all profiles; demands sustained consistent control.
        /// </param>
        public static ExerciseTargetProfile CreateStabilityTraining(
            double targetResonanceMin  = 0.45,
            double targetResonanceMax  = 0.88,
            double stabilityThreshold  = 0.70,
            double requiredHoldSeconds = 6.0) =>
            new ExerciseTargetProfile
            {
                UsesResonance       = true,    // secondary â€” prevents resonance regression
                UsesPitch           = false,   // comfort-zone safety only
                UsesStability       = true,    // primary metric
                UsesIntensity       = false,
                TargetResonanceMin  = targetResonanceMin,
                TargetResonanceMax  = targetResonanceMax,
                StabilityThreshold  = stabilityThreshold,
                RequiredHoldSeconds = requiredHoldSeconds
                ,
                ClinicalPurposeKey = "GuidancePurpose_StabilityTraining",
                PhysicalFocusKey   = "GuidanceFocus_StabilityTraining",
                CommonMistakesKey  = "GuidanceMistakes_StabilityTraining",
                SafetyInfoKey      = "GuidanceSafety_StabilityTraining",
                FeedbackModeKey    = "FeedbackMode_Stability",
                ThresholdStrategyKey = "ThresholdStrategy_Endurance",
                IndicatorPackageSummaryKey = "IndicatorPackage_Stability"
            };

        /// <summary>
        /// Read-only list of active indicators derived from boolean flags.
        /// Consumers may use this to drive typed indicator logic instead of checking multiple bools.
        /// </summary>
        public IReadOnlyList<IndicatorType> ActiveIndicators
        {
            get
            {
                var list = new List<IndicatorType>(6);
                if (UsesResonance) list.Add(IndicatorType.Resonance);
                if (UsesStability) list.Add(IndicatorType.Stability);
                if (UsesPitch)     list.Add(IndicatorType.Pitch);
                if (RequiredHoldSeconds > 0) list.Add(IndicatorType.Hold);
                if (true)          list.Add(IndicatorType.Shield); // Shield shown when profile applied
                if (UsesIntensity) list.Add(IndicatorType.Airflow);
                return list.AsReadOnly();
            }
        }

        /// <summary>
        /// Convenience descriptor that packages the profile's summary resource key with
        /// the typed indicator list. Consumers may use this to display a compact
        /// summary of which indicators are active for the current exercise.
        /// </summary>
        public IndicatorPackage IndicatorPackage => new IndicatorPackage(IndicatorPackageSummaryKey, ActiveIndicators);

    }
}

