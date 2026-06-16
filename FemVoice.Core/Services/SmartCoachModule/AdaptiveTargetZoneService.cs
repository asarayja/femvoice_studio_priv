using System;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.SmartCoachModule
{
    /// <summary>
    /// Adaptive Target Zone Service - dynamically adjusts exercise target zones based on user progression.
    /// Implements clinical principle: Maximum 5 Hz pitch increase per session, 2 Hz/week default progression.
    /// </summary>
    public class AdaptiveTargetZoneService
    {
        #region Constants

        // Progression limits (from clinical principles)
        public const double MaxPitchIncreasePerSession = 5.0;   // Hz
        public const double DefaultWeeklyProgression = 2.0;     // Hz per week
        public const double FastProgression = 3.0;              // Hz per week if &gt;10% improvement
        public const double SlowProgression = 1.0;             // Hz per week if stagnating
        
        // Session requirements
        public const int MinSessionsForProgression = 3;
        public const int ConsecutiveGoodSessionsForIncrease = 3;
        
        // Pitch targets
        public const double MinSafePitch = 140.0;
        public const double MaxSafePitch = 280.0;
        
        // Resonance: Don't increase pitch if resonance is poor
        public const double ResonanceThresholdForPitchIncrease = 70.0;

        #endregion

        private readonly IDatabaseService _database;
        private readonly SmartCoachEngine _smartCoachEngine;

        public AdaptiveTargetZoneService(IDatabaseService database)
        {
            _database = database;
            _smartCoachEngine = new SmartCoachEngine(database);
        }

        /// <summary>
        /// Get adapted exercise definition with current target zones.
        /// </summary>
        public ExerciseDefinition GetAdaptedDefinition(ExerciseDefinition definition, int userId = 1)
        {
            var baseline = _smartCoachEngine.GetOrCalculateBaseline(userId);
            var recentSessions = _database.GetRecentSessions(10, userId);
            
            // Start with original definition
            var adapted = CloneDefinition(definition);
            
            // Adjust pitch target based on progression
            var pitchAdjustment = CalculatePitchAdjustment(baseline, recentSessions);
            adapted.TargetPitchRange = new TargetRange(
                Math.Max(MinSafePitch, definition.TargetPitchRange.Min + pitchAdjustment),
                Math.Max(MinSafePitch + 10, Math.Min(MaxSafePitch, definition.TargetPitchRange.Max + pitchAdjustment))
            );
            
            // Adjust tolerances based on user level
            var userLevel = GetUserLevel(recentSessions);
            ApplyLevelTolerances(adapted, userLevel);
            
            return adapted;
        }

        /// <summary>
        /// Calculate pitch adjustment based on progression rate.
        /// </summary>
        private double CalculatePitchAdjustment(SmartCoachBaseline? baseline, System.Collections.Generic.List<TrainingSession> recentSessions)
        {
            if (baseline == null || recentSessions.Count < MinSessionsForProgression)
            {
                return 0; // No adjustment for new users
            }

            // Check resonance first - don't increase pitch if resonance is poor
            if (baseline.BaselineResonanceScore < ResonanceThresholdForPitchIncrease)
            {
                return 0; // Prioritize resonance improvement
            }

            // Check if user has been scoring well consistently
            int goodSessions = 0;
            foreach (var session in recentSessions.Take(ConsecutiveGoodSessionsForIncrease))
            {
                if (session.OverallScore >= 70)
                    goodSessions++;
            }

            if (goodSessions >= ConsecutiveGoodSessionsForIncrease)
            {
                // Good progress - can increase pitch target
                double weeklyRate = DetermineWeeklyProgressionRate(recentSessions);
                double sessionIncrease = weeklyRate / 3.0; // Assuming ~3 sessions per week
                return Math.Min(MaxPitchIncreasePerSession, sessionIncrease);
            }

            return 0; // No increase
        }

        /// <summary>
        /// Determine weekly progression rate based on recent improvement.
        /// </summary>
        private double DetermineWeeklyProgressionRate(System.Collections.Generic.List<TrainingSession> recentSessions)
        {
            if (recentSessions.Count < 2)
                return DefaultWeeklyProgression;

            // Calculate improvement from first half to second half of recent sessions
            int half = recentSessions.Count / 2;
            var firstHalf = recentSessions.Take(half).ToList();
            var secondHalf = recentSessions.Skip(half).Take(half).ToList();

            double firstAvg = firstHalf.Average(s => s.OverallScore);
            double secondAvg = secondHalf.Average(s => s.OverallScore);
            double improvement = secondAvg - firstAvg;

            if (improvement > 10)
                return FastProgression;
            if (improvement > 0)
                return DefaultWeeklyProgression;
            
            return SlowProgression; // Stagnating
        }

        /// <summary>
        /// Determine user level based on session history.
        /// </summary>
        private UserLevel GetUserLevel(System.Collections.Generic.List<TrainingSession> sessions)
        {
            if (sessions.Count == 0)
                return UserLevel.Nybegynner;

            var avgScore = sessions.Average(s => s.OverallScore);
            var totalSessions = sessions.Count;

            if (totalSessions <= 10 || avgScore < 60)
                return UserLevel.Nybegynner;
            if (totalSessions <= 30 || avgScore < 80)
                return UserLevel.Middels;

            return UserLevel.Avansert;
        }

        /// <summary>
        /// Apply level-specific tolerances to definition.
        /// </summary>
        private void ApplyLevelTolerances(ExerciseDefinition definition, UserLevel level)
        {
            var factors = level switch
            {
                UserLevel.Nybegynner => LevelScalingFactors.ForNybegynner(),
                UserLevel.Middels => LevelScalingFactors.ForMiddels(),
                UserLevel.Avansert => LevelScalingFactors.ForAvansert(),
                _ => LevelScalingFactors.ForMiddels()
            };

            // Apply tolerance multiplier to pitch range
            double tolerance = (definition.TargetPitchRange.Max - definition.TargetPitchRange.Min) * 
                              (factors.PitchToleranceMultiplier - 1) / 2;
            
            definition.TargetPitchRange = new TargetRange(
                definition.TargetPitchRange.Min - tolerance,
                definition.TargetPitchRange.Max + tolerance
            );
        }

        /// <summary>
        /// Temporarily lower pitch target after health warning.
        /// </summary>
        public void TemporarilyLowerPitch(ExerciseDefinition definition, double hz = 10)
        {
            definition.TargetPitchRange = new TargetRange(
                Math.Max(MinSafePitch, definition.TargetPitchRange.Min - hz),
                Math.Max(MinSafePitch + 10, definition.TargetPitchRange.Max - hz)
            );
        }

        /// <summary>
        /// Reset temporary pitch adjustment.
        /// </summary>
        public void ResetTemporaryAdjustment(ExerciseDefinition definition, double originalMin, double originalMax)
        {
            definition.TargetPitchRange = new TargetRange(originalMin, originalMax);
        }

        /// <summary>
        /// Clone exercise definition.
        /// </summary>
        private ExerciseDefinition CloneDefinition(ExerciseDefinition original)
        {
            return new ExerciseDefinition
            {
                ExerciseId = original.ExerciseId,
                Name = original.Name,
                TargetPitchRange = new TargetRange(original.TargetPitchRange.Min, original.TargetPitchRange.Max),
                TargetF1Range = new TargetRange(original.TargetF1Range.Min, original.TargetF1Range.Max),
                TargetF2Range = new TargetRange(original.TargetF2Range.Min, original.TargetF2Range.Max),
                TargetF3Min = original.TargetF3Min,
                StabilityThresholdPercent = original.StabilityThresholdPercent,
                StrainLimitShimmerPercent = original.StrainLimitShimmerPercent,
                StrainLimitAmplitudeSpikePercent = original.StrainLimitAmplitudeSpikePercent,
                NybegynnerFactors = original.NybegynnerFactors,
                MiddelsFactors = original.MiddelsFactors,
                AvansertFactors = original.AvansertFactors,
                FeedbackRules = original.FeedbackRules,
                HealthThresholds = original.HealthThresholds,
                RequiresIntonation = original.RequiresIntonation,
                IntonationRange = original.IntonationRange,
                GoalCategory = original.GoalCategory
            };
        }
    }
}
