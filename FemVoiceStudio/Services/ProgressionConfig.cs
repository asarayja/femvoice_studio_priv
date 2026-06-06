using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Configuration for the ProgressionEngine.
    /// All threshold values are configurable - no hardcoded magic numbers.
    /// Values can be adjusted based on user profile and clinical requirements.
    /// </summary>
    public class ProgressionConfig
    {
        #region Progression Gates (Hard Gates - Must ALL be met for pitch increase)
        
        /// <summary>
        /// Minimum resonance score required to open progression gate (0-100)
        /// Clinical principle: Resonance must support pitch
        /// </summary>
        public double MinimumResonanceScore { get; set; } = 60.0;
        
        /// <summary>
        /// Minimum voice health score required to open progression gate (0-100)
        /// Safety first: No progression if health is compromised
        /// </summary>
        public double MinimumVoiceHealthScore { get; set; } = 70.0;
        
        /// <summary>
        /// Minimum consecutive stable sessions required for progression
        /// Ensures stability before advancing
        /// </summary>
        public int MinimumConsecutiveStableSessions { get; set; } = 3;
        
        #endregion
        
        #region Pitch Progression Parameters
        
        /// <summary>
        /// Hz to increase pitch comfort zone per progression cycle (2-5 Hz typical)
        /// </summary>
        public double PitchIncreasePerCycle { get; set; } = 3.0;
        
        /// <summary>
        /// Minimum Hz increase per cycle
        /// </summary>
        public double MinPitchIncrease { get; set; } = 2.0;
        
        /// <summary>
        /// Maximum Hz increase per cycle
        /// </summary>
        public double MaxPitchIncrease { get; set; } = 5.0;
        
        /// <summary>
        /// Absolute minimum pitch target (Hz)
        /// </summary>
        public double AbsoluteMinPitchTarget { get; set; } = 165.0;
        
        /// <summary>
        /// Absolute maximum pitch target (Hz)
        /// </summary>
        public double AbsoluteMaxPitchTarget { get; set; } = 255.0;
        
        #endregion
        
        #region Resonance Refinement Parameters
        
        /// <summary>
        /// Additional resonance score target for refinement mode (+5-10 typical)
        /// </summary>
        public double ResonanceRefinementBonus { get; set; } = 8.0;
        
        /// <summary>
        /// Minimum resonance score for refinement focus
        /// </summary>
        public double ResonanceRefinementMinScore { get; set; } = 55.0;
        
        #endregion
        
        #region Prosody Expansion Parameters
        
        /// <summary>
        /// Percentage to expand prosody range per cycle (10-30% typical)
        /// </summary>
        public double ProsodyExpansionPercentPerCycle { get; set; } = 15.0;
        
        /// <summary>
        /// Minimum prosody expansion percentage
        /// </summary>
        public double MinProsodyExpansion { get; set; } = 10.0;
        
        /// <summary>
        /// Maximum prosody expansion percentage
        /// </summary>
        public double MaxProsodyExpansion { get; set; } = 30.0;
        
        #endregion
        
        #region Periodization Settings
        
        /// <summary>
        /// Standard cycle: active sessions before consolidation
        /// </summary>
        public int StandardActiveSessions { get; set; } = 3;
        
        /// <summary>
        /// Standard cycle: consolidation sessions after active
        /// </summary>
        public int StandardConsolidationSessions { get; set; } = 1;
        
        /// <summary>
        /// High fatigue cycle: active sessions
        /// </summary>
        public int HighFatigueActiveSessions { get; set; } = 2;
        
        /// <summary>
        /// High fatigue cycle: recovery sessions
        /// </summary>
        public int HighFatigueRecoverySessions { get; set; } = 2;
        
        /// <summary>
        /// Minimum sessions needed after strain before progression
        /// </summary>
        public int MinRecoverySessionsAfterStrain { get; set; } = 2;
        
        /// <summary>
        /// Voice health threshold for high fatigue mode (0-100)
        /// </summary>
        public double HighFatigueHealthThreshold { get; set; } = 75.0;
        
        #endregion
        
        #region Regression Detection Thresholds
        
        /// <summary>
        /// Voice health decline percentage to trigger regression (over 3 sessions)
        /// </summary>
        public double HealthDeclinePercentThreshold { get; set; } = 15.0;
        
        /// <summary>
        /// Consecutive unstable sessions to trigger regression
        /// </summary>
        public int UnstableSessionsForRegression { get; set; } = 2;
        
        /// <summary>
        /// Resonance score drop to trigger regression detection (points)
        /// </summary>
        public double ResonanceDriftThreshold { get; set; } = 10.0;
        
        /// <summary>
        /// Number of sessions without progression to consider plateau
        /// </summary>
        public int PlateauSessionThreshold { get; set; } = 6;
        
        /// <summary>
        /// Pitch zone decrease percentage on regression
        /// </summary>
        public double RegressionPitchDecreasePercent { get; set; } = 5.0;
        
        #endregion
        
        #region Fatigue Management
        
        /// <summary>
        /// Fatigue accumulation rate per session (0-100)
        /// </summary>
        public double FatigueAccumulationRate { get; set; } = 15.0;
        
        /// <summary>
        /// Fatigue recovery rate per rest day (0-100)
        /// </summary>
        public double FatigueRecoveryRate { get; set; } = 25.0;
        
        /// <summary>
        /// Maximum fatigue before mandatory recovery (0-100)
        /// </summary>
        public double MaxFatigueBeforeRecovery { get; set; } = 80.0;
        
        #endregion
        
        #region Baseline Establishment
        
        /// <summary>
        /// Minimum sessions to establish baseline (3-5 typical)
        /// </summary>
        public int MinBaselineSessions { get; set; } = 3;
        
        /// <summary>
        /// Maximum sessions to establish baseline
        /// </summary>
        public int MaxBaselineSessions { get; set; } = 5;
        
        /// <summary>
        /// Baseline establishment window in days
        /// </summary>
        public int BaselineWindowDays { get; set; } = 14;
        
        #endregion
        
        #region Milestone Thresholds
        
        /// <summary>
        /// Days of stable resonance for milestone
        /// </summary>
        public int ResonanceStabilityDays { get; set; } = 14;
        
        /// <summary>
        /// Minimum resonance score for milestone
        /// </summary>
        public double ResonanceScoreMilestoneThreshold { get; set; } = 80.0;
        
        /// <summary>
        /// Pitch increase (Hz) for milestone
        /// </summary>
        public double PitchIncreaseMilestoneThreshold { get; set; } = 20.0;
        
        /// <summary>
        /// Prosody expansion percentage for milestone
        /// </summary>
        public double ProsodyExpansionMilestoneThreshold { get; set; } = 30.0;
        
        /// <summary>
        /// Health score threshold for milestone
        /// </summary>
        public double HealthScoreMilestoneThreshold { get; set; } = 85.0;
        
        /// <summary>
        /// Health score days threshold for milestone
        /// </summary>
        public int HealthScoreDaysThreshold { get; set; } = 7;
        
        /// <summary>
        /// Combined score threshold (all scores must exceed)
        /// </summary>
        public double AllScoresThreshold { get; set; } = 70.0;
        
        /// <summary>
        /// Strain-free sessions for milestone
        /// </summary>
        public int StrainFreeSessionsThresholdLow { get; set; } = 10;
        public int StrainFreeSessionsThresholdHigh { get; set; } = 50;
        
        #endregion
        
        #region Stability Detection
        
        /// <summary>
        /// Pitch variation threshold for stable classification (Hz)
        /// </summary>
        public double PitchVariationStableThreshold { get; set; } = 25.0;
        
        /// <summary>
        /// Resonance stability threshold (0-100)
        /// </summary>
        public double ResonanceStabilityThreshold { get; set; } = 70.0;
        
        #endregion
        
        /// <summary>
        /// Creates default configuration
        /// </summary>
        public static ProgressionConfig CreateDefault()
        {
            return new ProgressionConfig();
        }
        
        /// <summary>
        /// Creates configuration for beginners (more conservative)
        /// </summary>
        public static ProgressionConfig CreateForBeginner()
        {
            return new ProgressionConfig
            {
                MinimumResonanceScore = 55.0,
                MinimumVoiceHealthScore = 75.0,
                MinimumConsecutiveStableSessions = 4,
                PitchIncreasePerCycle = 2.0,
                StandardActiveSessions = 2,
                StandardConsolidationSessions = 2,
                MinBaselineSessions = 5,
                MaxBaselineSessions = 7
            };
        }
        
        /// <summary>
        /// Creates configuration for advanced users (more aggressive)
        /// </summary>
        public static ProgressionConfig CreateForAdvanced()
        {
            return new ProgressionConfig
            {
                MinimumResonanceScore = 65.0,
                MinimumVoiceHealthScore = 70.0,
                MinimumConsecutiveStableSessions = 2,
                PitchIncreasePerCycle = 5.0,
                StandardActiveSessions = 4,
                StandardConsolidationSessions = 1,
                ResonanceRefinementBonus = 10.0
            };
        }
    }
    
    /// <summary>
    /// Result of a progression evaluation
    /// </summary>
    public class ProgressionEvaluationResult
    {
        public ProgressionDecision Decision { get; set; }
        public ProgressionMode NewMode { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public ProgressionGateStatus GateStatus { get; set; } = new();
        public PeriodizationCycle CurrentCycle { get; set; }
        public SessionType CurrentSessionType { get; set; }
        
        /// <summary>
        /// Proposed changes to target zones
        /// </summary>
        public double? ProposedPitchMinChange { get; set; }
        public double? ProposedPitchMaxChange { get; set; }
        public double? ProposedProsodyChange { get; set; }
        
        /// <summary>
        /// Milestones achieved in this evaluation
        /// </summary>
        public System.Collections.Generic.List<ProgressionMilestone> NewMilestones { get; set; } = new();
        
        /// <summary>
        /// Warnings or concerns
        /// </summary>
        public System.Collections.Generic.List<string> Warnings { get; set; } = new();
    }
}
