using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using Range = FemVoiceStudio.Models.Range;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service for adaptive comfort zone calculation.
    /// Adjusts target zones based on user baseline and session stability.
    /// </summary>
    public class AdaptiveComfortZoneService
    {
        private readonly SmartCoachEngine _smartCoach;
        
        // Base comfort zone values (defaults)
        private const double DefaultMinPitch = 165;
        private const double DefaultMaxPitch = 255;
        private const double DefaultOptimalPitch = 200;
        
        // Adaptive ranges
        private const double MinComfortRange = 30;   // Minimum range width
        private const double MaxComfortRange = 60;    // Maximum range width
        private const double RangeExpansionRate = 5;  // Hz per stable session
        
        // Progressive session parameters
        private const double ProgressivePitchStep = 5;  // Hz increase per progressive session
        private const double MaintenancePitchReduction = 10;  // Hz reduction for maintenance
        private const double RecoveryPitchReduction = 30;    // Hz reduction for recovery
        
        // Requirements for pitch progression
        private const double MinResonanceForProgression = 60;
        private const double MinHealthForProgression = 70;
        private const int MinStableSessionsForProgression = 3;
        
        /// <summary>
        /// Constructor with dependency injection (recommended)
        /// </summary>
        public AdaptiveComfortZoneService(SmartCoachEngine smartCoach)
        {
            _smartCoach = smartCoach ?? throw new ArgumentNullException(nameof(smartCoach));
        }
        
        /// <summary>
        /// Calculate adaptive comfort zone based on user baseline
        /// </summary>
        public Range CalculateComfortZone(int userId = 1, SessionType sessionType = SessionType.Progressive)
        {
            var baseline = _smartCoach.GetOrCalculateBaseline(userId);
            
            double minPitch, maxPitch, optimalPitch;
            
            if (baseline == null || baseline.ConfidenceLevel == "low")
            {
                // No baseline - use defaults
                return GetDefaultComfortZone(sessionType);
            }
            
            // Calculate based on session type
            switch (sessionType)
            {
                case SessionType.Recovery:
                    minPitch = Math.Max(140, baseline.BaselinePitch - RecoveryPitchReduction);
                    // Ensure recovery sessions reduce the maximum pitch below baseline
                    maxPitch = Math.Min(baseline.BaselinePitch - 1, minPitch + MinComfortRange);
                    optimalPitch = (minPitch + maxPitch) / 2;
                    break;
                    
                case SessionType.Maintenance:
                    minPitch = Math.Max(150, baseline.BaselinePitch - MaintenancePitchReduction);
                    maxPitch = minPitch + MinComfortRange;
                    optimalPitch = (minPitch + maxPitch) / 2;
                    break;
                    
                case SessionType.Progressive:
                default:
                    // Progressive: expand based on stability
                    var stabilityFactor = CalculateStabilityFactor(userId);
                    
                    // Calculate range based on baseline and stability
                    double range = Math.Min(MaxComfortRange, MinComfortRange + stabilityFactor * RangeExpansionRate);
                    
                    minPitch = baseline.BaselinePitch - 10;  // Slightly below baseline
                    maxPitch = minPitch + range;
                    
                    // Cap at safe maximum
                    if (maxPitch > 280)
                    {
                        maxPitch = 280;
                        minPitch = maxPitch - range;
                    }
                    
                    optimalPitch = (minPitch + maxPitch) / 2;
                    break;
            }
            
            return new Range(minPitch, maxPitch, optimalPitch);
        }
        
        /// <summary>
        /// Get default comfort zone for new users
        /// </summary>
        private Range GetDefaultComfortZone(SessionType sessionType)
        {
            return sessionType switch
            {
                SessionType.Recovery => new Range(140, 180, 160),
                SessionType.Maintenance => new Range(155, 195, 175),
                _ => new Range(DefaultMinPitch, DefaultMaxPitch, DefaultOptimalPitch)
            };
        }
        
        /// <summary>
        /// Calculate stability factor based on recent sessions
        /// </summary>
        private double CalculateStabilityFactor(int userId)
        {
            // Get recent sessions for stability analysis
            // This would normally come from database - simplified here
            return 1.0; // Base stability factor
        }
        
        /// <summary>
        /// Determine if user can progress to higher pitch targets
        /// </summary>
        public bool CanProgress(int userId)
        {
            var baseline = _smartCoach.GetOrCalculateBaseline(userId);
            
            if (baseline == null)
                return false;
            
            // Check resonance requirement
            if (baseline.BaselineResonanceScore < MinResonanceForProgression)
                return false;
            
            // Check health requirement (would come from health monitoring)
            // For now, assume health is OK if no recent issues
            
            // Check stability requirement - need stable sessions
            var recentSessions = GetRecentSessionScores(userId, MinStableSessionsForProgression);
            if (recentSessions.Count < MinStableSessionsForProgression)
                return false;
            
            // Check if recent sessions are stable
            double avgStability = recentSessions.Average(s => s.PitchScore);
            return avgStability > 60;
        }
        
        /// <summary>
        /// Get recommended session type based on user state
        /// </summary>
        public SessionType GetRecommendedSessionType(int userId)
        {
            var baseline = _smartCoach.GetOrCalculateBaseline(userId);
            
            if (baseline == null)
                return SessionType.Progressive;
            
            // Check for recent health issues using weekly progress as a proxy
            // (SmartCoachEngine aggregates health monitoring; using weekly progress
            // allows us to infer significant strain without direct DB access).
            // KANONISK ukestart (søndag) — ikke rullerende vindu: Today.AddDays(-6)
            // ga en NY WeekStart-verdi per dag, og siden CalculateWeeklyProgress
            // persisterer raden (UPDATE-på-WeekStart matchet aldri) fikk SmartCoach-
            // historikken én duplikatrad per treningsdag — «samme uke 4 ganger».
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var weekProgress = _smartCoach.CalculateWeeklyProgress(weekStart, userId);
            if (weekProgress.HealthScore < 60)
                return SessionType.Recovery;
            if (weekProgress.HealthScore < 80)
                return SessionType.Maintenance;
            
            // Check if ready for progression
            if (CanProgress(userId))
                return SessionType.Progressive;
            
            return SessionType.Maintenance;
        }
        
        /// <summary>
        /// Generate SmartCoach explanation based on current state
        /// </summary>
        public string GenerateExplanation(CoachExplanationContext context)
        {
            // Analyze what affects score most
            double resonanceWeight = GetScoreComponentWeight(context.ResonanceScore);
            double pitchWeight = GetScoreComponentWeight(context.PitchScore);
            double healthWeight = GetScoreComponentWeight(context.VoiceHealthScore);
            double intonationWeight = GetScoreComponentWeight(context.IntonationScore);
            
            // Priority ordering
            var priorities = new List<(string component, double weight)>
            {
                ("resonance", resonanceWeight),
                ("pitch", pitchWeight),
                ("health", healthWeight),
                ("intonation", intonationWeight)
            };
            priorities.Sort((a, b) => b.weight.CompareTo(a.weight));
            
            // Generate explanation based on lowest-scoring component
            string lowestComponent = priorities[0].component;
            
            return lowestComponent switch
            {
                "resonance" when context.CurrentResonance < context.CurrentPitch / 2 =>
                    LocalizationService.Instance["AdaptiveComfort_ResonanceBelowPitch"],
                "resonance" =>
                    LocalizationService.Instance["AdaptiveComfort_ResonanceImprove"],
                "pitch" when context.Stability == StabilityState.Stable || context.Stability == StabilityState.VeryStable =>
                    LocalizationService.Instance["AdaptiveComfort_PitchStableIntonation"],
                "pitch" =>
                    LocalizationService.Instance["AdaptiveComfort_PitchVariationHigh"],
                "health" when context.Health == HealthState.Warning || context.Health == HealthState.Danger =>
                    LocalizationService.Instance["AdaptiveComfort_HealthStrain"],
                "health" =>
                    LocalizationService.Instance["AdaptiveComfort_HealthMonitor"],
                "intonation" =>
                    LocalizationService.Instance["AdaptiveComfort_IntonationVariation"],
                _ => LocalizationService.Instance["AdaptiveComfort_Default"]
            };
        }
        
        /// <summary>
        /// Calculate weight for score component (inverse - lower score = higher weight)
        /// </summary>
        private double GetScoreComponentWeight(double score)
        {
            return 100 - score;
        }
        
        /// <summary>
        /// Get recent session scores (simplified - would come from database)
        /// </summary>
        private List<FemVoiceScoreResult> GetRecentSessionScores(int userId, int count)
        {
            // Would normally fetch from database
            return new List<FemVoiceScoreResult>();
        }
        
        /// <summary>
        /// Get recent health issues (simplified - would come from database)
        /// </summary>
        // NOTE: Recent health issues are analyzed via SmartCoachEngine aggregation
        // methods (CalculateWeeklyProgress) to avoid duplicating DB access here.
    }
}
