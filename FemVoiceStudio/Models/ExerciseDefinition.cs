using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Brukernivå basert på erfaring og prestasjon
    /// </summary>
    public enum UserLevel
    {
        Nybegynner = 1,
        Middels    = 2,
        Avansert   = 3
    }

    /// <summary>
    /// Målområde for en stemmeparameter.
    /// Internal engine use only — never serialised to display text.
    /// </summary>
    public class TargetRange
    {
        public double Min { get; set; }
        public double Max { get; set; }

        public TargetRange() { }
        public TargetRange(double min, double max) { Min = min; Max = max; }

        public bool IsInRange(double value)         => value >= Min && value <= Max;
        public double DistanceFromRange(double value)
        {
            if (IsInRange(value)) return 0;
            return value < Min ? Min - value : value - Max;
        }
    }

    /// <summary>
    /// Skaleringsfaktorer for ulike brukernivåer
    /// </summary>
    public class LevelScalingFactors
    {
        public double PitchToleranceMultiplier      { get; set; } = 1.0;
        public double ResonanceToleranceMultiplier  { get; set; } = 1.0;
        public double StabilityThresholdMultiplier  { get; set; } = 1.0;
        public double FeedbackIntervalSeconds       { get; set; } = 5.0;
        public double HintDetailLevel               { get; set; } = 1.0;

        public static LevelScalingFactors ForNybegynner() => new LevelScalingFactors
        {
            PitchToleranceMultiplier     = 2.0,
            ResonanceToleranceMultiplier = 2.0,
            StabilityThresholdMultiplier = 2.0,
            FeedbackIntervalSeconds      = 5.0,
            HintDetailLevel              = 1.0
        };

        public static LevelScalingFactors ForMiddels() => new LevelScalingFactors
        {
            PitchToleranceMultiplier     = 1.0,
            ResonanceToleranceMultiplier = 1.0,
            StabilityThresholdMultiplier = 1.0,
            FeedbackIntervalSeconds      = 10.0,
            HintDetailLevel              = 0.7
        };

        public static LevelScalingFactors ForAvansert() => new LevelScalingFactors
        {
            PitchToleranceMultiplier     = 0.5,
            ResonanceToleranceMultiplier = 0.5,
            StabilityThresholdMultiplier = 0.75,
            FeedbackIntervalSeconds      = 999.0,
            HintDetailLevel              = 0.3
        };
    }

    /// <summary>
    /// Helsebeskyttelsesterskler for en øvelse
    /// </summary>
    public class HealthProtectionThresholds
    {
        public double MaxJitterPercent          { get; set; } = 2.5;
        public double MaxShimmerPercent         { get; set; } = 4.0;
        public double MaxAmplitudeSpikePercent  { get; set; } = 20.0;
        public double PitchPressThresholdHz     { get; set; } = 20.0;
        public int    ConsecutiveWarningLimit   { get; set; } = 3;

        public static HealthProtectionThresholds Default() => new HealthProtectionThresholds
        {
            MaxJitterPercent         = 2.5,
            MaxShimmerPercent        = 4.0,
            MaxAmplitudeSpikePercent = 20.0,
            PitchPressThresholdHz    = 20.0,
            ConsecutiveWarningLimit  = 3
        };
    }

    /// <summary>
    /// Tilbakemeldingsregler for en parameter.
    /// Keys reference LocalizationService — never hardcoded display strings.
    /// </summary>
    public class FeedbackRule
    {
        /// <summary>Parameter identifier: "pitch", "resonance", "stability", "intonation".</summary>
        public string Parameter  { get; set; } = "";
        /// <summary>Localisation key for correct-state feedback.</summary>
        public string CorrectKey { get; set; } = "";
        /// <summary>Localisation key for adjust-state feedback.</summary>
        public string AdjustKey  { get; set; } = "";
        /// <summary>Localisation key for stop / safety feedback.</summary>
        public string StopKey    { get; set; } = "";
    }

    /// <summary>
    /// Datastruktur som beskriver en øvelse med målparametere og toleranser.
    ///
    /// VIKTIG: Ingen Hz-verdier eller rå numeriske parametere eksponeres mot UI.
    /// All display-tekst routes via LocalizationService-nøkler.
    /// </summary>
    public class ExerciseDefinition
    {
        public int    ExerciseId { get; set; }
        public string Name       { get; set; } = "";

        // ── Engine-interne målparametere (aldri vist direkte i UI) ────────────
        public TargetRange TargetPitchRange { get; set; } = new(165, 220);
        public TargetRange TargetF1Range    { get; set; } = new(300, 1000);
        public TargetRange TargetF2Range    { get; set; } = new(1800, 2500);
        public double      TargetF3Min      { get; set; } = 2700;

        public double StabilityThresholdPercent      { get; set; } = 2.0;
        public double StrainLimitShimmerPercent       { get; set; } = 4.0;
        public double StrainLimitAmplitudeSpikePercent{ get; set; } = 20.0;

        // ── Nivåbaserte skaleringsfaktorer ────────────────────────────────────
        public LevelScalingFactors NybegynnerFactors { get; set; } = LevelScalingFactors.ForNybegynner();
        public LevelScalingFactors MiddelsFactors    { get; set; } = LevelScalingFactors.ForMiddels();
        public LevelScalingFactors AvansertFactors   { get; set; } = LevelScalingFactors.ForAvansert();

        // ── Tilbakemeldingsregler ─────────────────────────────────────────────
        public List<FeedbackRule> FeedbackRules { get; set; } = new();

        // ── Helsebeskyttelse ──────────────────────────────────────────────────
        public HealthProtectionThresholds HealthThresholds { get; set; } = HealthProtectionThresholds.Default();

        // ── Intonasjonskrav ───────────────────────────────────────────────────
        public bool         RequiresIntonation { get; set; }
        public TargetRange? IntonationRange    { get; set; }

        // ── Målkategori ───────────────────────────────────────────────────────
        public GoalCategory GoalCategory { get; set; } = GoalCategory.Pitch;

        // ── Guidance localisation keys (NY) ───────────────────────────────────
        //    Refererer til nøkler i ExerciseTargetProfile som hentes av
        //    ExerciseDetailViewModel og vises i ExerciseGuidancePanel.
        //    Ingen Hz-verdier eller råtall er tillatt her.

        /// <summary>
        /// Localisation key for the clinical purpose description.
        /// Mirrors the key used in the corresponding ExerciseTargetProfile.
        /// Example: "Guidance_Humming_ClinicalPurpose"
        /// </summary>
        public string? ClinicalPurposeKey { get; set; }

        /// <summary>
        /// Localisation key for the physical/anatomical focus.
        /// Example: "Guidance_Humming_PhysicalFocus"
        /// </summary>
        public string? PhysicalFocusKey { get; set; }

        /// <summary>
        /// Localisation key listing common errors for this exercise type.
        /// Example: "Guidance_Humming_CommonMistakes"
        /// </summary>
        public string? CommonMistakesKey { get; set; }

        // ── Helper methods (uendret logikk) ──────────────────────────────────

        public LevelScalingFactors GetScalingFactors(UserLevel level) => level switch
        {
            UserLevel.Nybegynner => NybegynnerFactors,
            UserLevel.Middels    => MiddelsFactors,
            UserLevel.Avansert   => AvansertFactors,
            _                    => MiddelsFactors
        };

        public double GetEffectivePitchTolerance(UserLevel level)
        {
            var baseTolerance = (TargetPitchRange.Max - TargetPitchRange.Min) / 2;
            return baseTolerance * GetScalingFactors(level).PitchToleranceMultiplier;
        }

        public double GetEffectiveResonanceToleranceF2(UserLevel level)
        {
            var baseTolerance = (TargetF2Range.Max - TargetF2Range.Min) / 2;
            return baseTolerance * GetScalingFactors(level).ResonanceToleranceMultiplier;
        }

        public double GetEffectiveStabilityThreshold(UserLevel level)
            => StabilityThresholdPercent * GetScalingFactors(level).StabilityThresholdMultiplier;

        /// <summary>
        /// Opprett ExerciseDefinition fra Exercise-objekt.
        /// Kopierer guidance-nøkler fra Exercise slik at ExerciseDetailViewModel
        /// alltid finner dem på ExerciseTargetProfile (via factory) ELLER direkte
        /// her — én enkelt kilde til sannhet per nøkkel.
        /// </summary>
        public static ExerciseDefinition FromExercise(Exercise exercise)
        {
            var definition = new ExerciseDefinition
            {
                ExerciseId         = exercise.ExerciseId,
                Name               = exercise.Name,
                TargetPitchRange   = new TargetRange(exercise.TargetPitchMin, exercise.TargetPitchMax),
                GoalCategory       = exercise.Goal,
                RequiresIntonation = exercise.Goal == GoalCategory.Intonation
                                     || exercise.Goal == GoalCategory.Combined,
                // Guidance keys flow from Exercise model (set in seed / migration)
                ClinicalPurposeKey = exercise.ClinicalPurposeKey,
                PhysicalFocusKey   = exercise.PhysicalFocusKey,
                CommonMistakesKey  = exercise.CommonMistakesKey
            };

            // REMOVED: Hz-range string formatting — no display text built here.
            // Resonance ranges are engine-internal and set by profile factory.
            switch (exercise.Goal)
            {
                case GoalCategory.Resonance:
                    definition.TargetF1Range = new TargetRange(400, 800);
                    definition.TargetF2Range = new TargetRange(1800, 2600);
                    break;
                case GoalCategory.Pitch:
                    definition.TargetF1Range = new TargetRange(300, 700);
                    definition.TargetF2Range = new TargetRange(1200, 1800);
                    break;
                case GoalCategory.Combined:
                    definition.TargetF1Range = new TargetRange(350, 750);
                    definition.TargetF2Range = new TargetRange(1500, 2200);
                    break;
                default:
                    definition.TargetF1Range = new TargetRange(300, 800);
                    definition.TargetF2Range = new TargetRange(1400, 2200);
                    break;
            }

            return definition;
        }
    }
}
